using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Proc;
using NicoNamaRokuga.Rec;


namespace NicoNamaRokuga
{
    public partial class Form1 : Form
    {

        public  static Props props;                   //設定

        private static bool IsBatchMode { get; set; } //引数指定で実行か？

        private NicoLiveNet _nLiveNet = null;         //WebClient
        private NicoNetStream _nNetStream = null;     //WebSocket(Stream)
        private NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        private ExecProcess _eProcess = null;         //Process
        private RecHtml _rHtml = null;                //RecHtml

        private BroadCastInfo bci = null;             //ストリームサーバー情報
        private CommentInfo cmi = null;               //コメントサーバー情報
        private ExecPsInfo epi = null;                //実行／保存ファイル情報

        private string liveId = null;

        private volatile bool start_flg = false;
        private RetryInfo _ri = null;

        private readonly object lockObject = new object();  //情報表示用
        private readonly object lockObject2 = new object(); //実行ファイルのログ用
        private string LogFile;
        private string LogFile2;

        public Form1(string[] args)
        {
            InitializeComponent();

            //設定データー読み込み
            props = new Props();
            props.LoadData();

            IsBatchMode = (args.Length > 0) ? true : false;

            if (IsBatchMode)
            {
                liveId = NicoLiveNet.GetLiveID(args[0]);
                if (string.IsNullOrEmpty(liveId))
                {
                    this.Close();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //中断処理
                if (button1.Text == "中断")
                {
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_eProcess != null)
                    {
                        _eProcess.BreakProcess(epi.BreakKey);
                    }
                    if (_nNetStream != null)
                    {
                        _nNetStream.Close();
                    }
                    if (_nNetComment != null)
                    {
                        _nNetComment.Close();
                    }
                    if (_nLiveNet != null)
                    {
                        _nLiveNet.Dispose();
                    }
                    AddLog("中断しました。", 1);
                    EnableButton(true);
                    start_flg = false;
                    return;
                }


                //ニコ生に接続
                ClearLog();

                //フォルダやファイルのチェック
                var save_dir = props.SaveDir;
                if (String.IsNullOrEmpty(save_dir))
                {
                    save_dir = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                if (!Directory.Exists(save_dir))
                {
                    AddLog("保存フォルダーが存在しません。", 2);
                    return;
                }
                var save_file = props.SaveFile;
                if (String.IsNullOrEmpty(save_file))
                {
                    save_file = Properties.Settings.Default.DefaultSaveFile;
                }

                var exec_file = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                if (String.IsNullOrEmpty(exec_file))
                {
                    exec_file = Properties.Settings.Default.DefaultExecFile;
                }
                exec_file = GetExecFile(exec_file);
                if (!File.Exists(exec_file))
                {
                    AddLog("実行ファイルがありません。", 2);
                    return;
                }
                var exec_command = props.ExecCommand[Props.ParseProtocol(props.Protocol.ToString())];
                if (String.IsNullOrEmpty(exec_command))
                {
                    exec_command = Properties.Settings.Default.DefaultExecCommand;
                }

                //放送ID
                if (!IsBatchMode)
                    liveId = NicoLiveNet.GetLiveID(textBox1.Text);
                if (string.IsNullOrEmpty(liveId))
                {
                    AddLog("放送URLまたは放送IDを指定してください。", 2);
                    textBox1.Text = string.Empty;
                    return;
                }

                LogFile = Props.GetLogfile(save_dir, liveId);
                LogFile2 = Props.GetExecLogfile(save_dir, liveId);

                AddLog("録画開始します。", 1);
                AddLog(string.Format("LiveID: {0}", liveId), 1);
                textBox1.Text = NicoLiveNet.GetNicoPageUrl(liveId);

                //録画開始
                Task.Run(() => StartRec());

            }
            catch (Exception Ex)
            {
                AddLog(nameof(button1_Click) + "() Error: \r\n" + Ex.Message, 2);
            }

        }

        public async void StartRec()
        {
            try
            {
                _nLiveNet = new NicoLiveNet();

                //if (_nLiveNet.IsLoginStatus == true)
                //    await _nLiveNet.LogoutNico();

                //将来的には放送が要ログインかチェック
                //要ログインで none ならば終了 それ以外ならログイン
                //var gpsi = await _nLiveNet.GetLoginStatusAsync(liveId);
                //if (gpsi.Status != "ok")
                //{
                //    AddLog("番組情報が取得できませんでした。\r\n");
                //    AddLog("GetLoginStatusAsync: " + gpsi.Error + "\r\n");
                //    return;
                //}

                //ニコニコにログイン
                switch (props.LoginMethod.ToString())
                {
                    case "login":
                        if (!_nLiveNet.IsLoginStatus)
                        {
                            if (!(await _nLiveNet.LoginNico(props.UserID, props.Password)))
                            {
                                AddLog("Login Failed", 1);
                                return;
                            }
                            AddLog("Login OK", 1);
                        }
                        break;
                    case "cookie":
                        //ブラウザのCookie読み込み処理
                        if (props.SelectedCookie != null)
                            AddLog(string.Format("Cookie: {0}", props.SelectedCookie.BrowserName), 1);
                        if (!(await _nLiveNet.SetNicoCookie(!props.IsAllCookie, props.SelectedCookie)))
                        {
                            AddLog("Cookie読み込み失敗", 1);
                            return;
                        }
                        AddLog("Cookie読み込みOK", 1);
                        break;
                }

                //番組情報を取得する(旧API)
                var gpsi = await _nLiveNet.GetPlayerStatusAsync(liveId);
                if (gpsi.Status != "ok")
                {
                    AddLog("番組情報が取得できませんでした。", 1);
                    AddLog("GetApiStatus: " + gpsi.Error, 1);
                    return;
                }
                AddLog(string.Format("Provider_Type: {0}", gpsi.Provider_Type), 1);

                //番組情報を取得する
                bci = await _nLiveNet.GetNicoPageAsync(liveId);
                if (bci.Status != "ok")
                {
                    AddLog("放送情報が取得できませんでした。", 1);
                    AddLog("Status: " + bci.Error, 1);
                    return;
                }

                if (props.Protocol == Protocol.rtmp)
                {
                    if (bci.IsTimeShift() || bci.Provider_Type != "official")
                    {
                        AddLog("RTMP録画は公式の生放送のみです。", 1);
                        return;

                    }
                }

                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sdir = props.SaveDir;
                epi.Exec = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                epi.Arg = props.ExecCommand[Props.ParseProtocol(props.Protocol.ToString())];
                epi.Sfile = bci.SetRecFile(props.SaveFile);
                epi.Protocol = props.Protocol.ToString();
                epi.Seq = 0;

                //コメント情報
                if (props.IsComment)
                {
                    cmi = new CommentInfo(bci.User_Id);
                    cmi.BeginTime = bci.Open_Time;
                    cmi.EndTime = bci.End_Time;
                    _nNetComment = new NicoNetComment(this, bci, cmi, _nLiveNet);
                }
                var ri = new RetryInfo();
                _ri = ri;
                _ri.Count = props.Retry;

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                    _rHtml = new RecHtml(this, bci, _nNetComment, _nLiveNet.GetCookieContainer(), _ri);
                else
                    _eProcess = new ExecProcess(this, bci, _nNetComment, _ri);
                _nNetStream = new NicoNetStream(this, bci, cmi, epi, _nNetComment, _eProcess, _rHtml, _ri);

                AddLog("wsUrl: " + bci.WsUrl, 9);
                AddLog("wsPermit: " + _nNetStream.GetPermit(bci.BcId, props.Protocol.ToString()), 9);

                //放送情報を表示
                DispHosoData(bci);

                //WebSocket接続開始
                _nNetStream.Connect();

                //1秒おきに状態を調べて処理する
                start_flg = true;
                while (start_flg == true)
                {
                    await CheckStatus();
                    await Task.Delay(1000);
                }

            }
            catch (Exception Ex)
            {
                AddLog(nameof(StartRec) + "() Error: \r\n" + Ex.Message, 2);
            }


        }

        private async Task CheckStatus()
        {
            var WsCommentStatus = -1;
            var ExecStatus = -1;
            if (props.IsComment) WsCommentStatus = _nNetComment.WsStatus;
            if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                ExecStatus = _rHtml.PsStatus;
            else
                ExecStatus = _eProcess.PsStatus;
            try
            {
                if (_nNetStream.WsStatus >= 2 || WsCommentStatus >= 2 || ExecStatus >= 2)
                {
                    //WebSocket再接続処理開始
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_eProcess != null)
                    {
                        _eProcess.BreakProcess(epi.BreakKey);
                    }
                    if (_nNetStream != null)
                    {
                        _nNetStream.Close();
                    }
                    if (_nNetComment != null)
                    {
                        _nNetComment.Close();
                    }
                    if (_ri.Count > 0)
                    {
                        if (_nNetStream.WsStatus == 5)
                        {
                            AddLog(props.ReConnectTime2 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime2 - 1));
                            var _bci = await _nLiveNet.GetNicoPageAsync(liveId);
                            if (_bci.Status != "ok")
                            {
                                AddLog("放送情報が取得できませんでした。", 1);
                                AddLog("Status: " + _bci.Error, 1);
                                _ri.Count--;
                                return;
                            }
                            if (bci.OnAirStatus == "ON_AIR" && _bci.OnAirStatus != "ON_AIR")
                            {
                                ExecStatus = 1;
                            }
                            else
                            {
                                bci.BcId = _bci.BcId;
                                bci.AuTkn = _bci.AuTkn;
                                bci.WsUrl = _bci.WsUrl;
                            }
                        }
                        else if (_nNetStream.WsStatus == 4)
                        {
                            AddLog(props.ReConnectTime2 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime2 - 1));
                        }
                        else if (_nNetStream.WsStatus == 3)
                        {
                            AddLog(props.ReConnectTime1 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime1 - 1));
                        }
                        if (ExecStatus != 1)
                        {
                            AddLog("再接続します。", 1);
                            if (props.IsComment) _nNetComment = new NicoNetComment(this, bci, cmi, _nLiveNet);
                            if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                                _rHtml = new RecHtml(this, bci, _nNetComment, _nLiveNet.GetCookieContainer(), _ri);
                            else
                                _eProcess = new ExecProcess(this, bci, _nNetComment, _ri);
                            _nNetStream = new NicoNetStream(this, bci, cmi, epi, _nNetComment, _eProcess, _rHtml, _ri);
                            _nNetStream.Connect();
                            _ri.Count--;
                        }
                    }
                    else
                    {
                        AddLog("リトライ終了します。", 1);
                        ExecStatus = 1;
                    }
                }
                if (_nNetStream.WsStatus == 1 || ExecStatus == 1) //終了処理
                {
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_eProcess != null)
                    {
                        _eProcess.BreakProcess(epi.BreakKey);
                    }
                    if (_nNetStream != null)
                    {
                        _nNetStream.Close();
                    }
                    if (_nNetComment != null)
                    {
                        _nNetComment.Close();
                    }
                    if (_nLiveNet != null)
                    {
                        _nLiveNet.Dispose();
                    }
                    AddLog("録画終了しました。", 1);
                    EnableButton(true);
                    start_flg = false;
                    return;
                }
            }
            catch (Exception Ex)
            {
                AddLog(nameof(CheckStatus) + "() Error: \r\n" + Ex.Message, 2);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_rHtml != null)
            {
                _rHtml.Dispose();
                _rHtml = null;
            }
            if (_eProcess != null)
            {
                _eProcess.Dispose();
                _eProcess = null;
            }
            _nNetStream?.Close();
            _nNetStream?.Dispose();
            _nNetComment?.Close();
            _nNetComment?.Dispose();

            _nLiveNet?.Dispose();
        }

        private void 録画フォルダーを開くOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(props.SaveDir))
            {
                Process.Start(props.SaveDir);
            }
            else
            {
                Process.Start(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            }
        }

        private void 終了XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void オプションOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                using (var fo2 = new Form2(this))
                {
                    fo2.ShowDialog();
                }
            }
            catch (Exception Ex)
            {
                AddLog("オプションメニューが開けませんでした。\r\n" + Ex.Message, 2);
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (IsBatchMode) button1.PerformClick();
        }

    }
}
