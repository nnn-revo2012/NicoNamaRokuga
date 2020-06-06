using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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

        public static Props props;                   //設定

        private static bool IsBatchMode { get; set; } //引数指定で実行か？

        private NicoLiveNet _nLiveNet = null;         //WebClient
        private NicoNetStream _nNetStream = null;     //WebSocket(Stream)
        private NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        private CommentControl _cCtrl = null;         //コメント情報
        private ExecProcess _eProcess = null;         //Process
        private RecHtml _rHtml = null;                //RecHtml
        private NicoDb _ndb = null;                   //NicoDb

        private BroadCastInfo bci = null;             //ストリームサーバー情報
        private CommentInfo cmi = null;               //コメントサーバー情報
        private ExecPsInfo epi = null;                //実行／保存ファイル情報

        private string liveId = null;

        private volatile bool start_flg = false;
        private RetryInfo _ri = null;                 //リトライ情報

        private string accountdbfile;
        private readonly object lockObject = new object();  //情報表示用
        private readonly object lockObject2 = new object(); //実行ファイルのログ用
        private string LogFile;
        private string LogFile2;

        public Form1(string[] args)
        {
            InitializeComponent();
            this.Text = Ver.GetFullVersion();
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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //設定データー読み込み
            accountdbfile = Path.Combine(Props.GetSettingDirectory(), "account.db");
            props = new Props();
            props.LoadData(accountdbfile);
            ClearHosoData();

            if (IsBatchMode) button1.PerformClick();
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
                    if (_cCtrl != null)
                    {
                        _cCtrl = null;
                    }
                    if (_ndb != null)
                    {
                        _ndb.Dispose();
                    }
                    AddLog("中断しました。", 1);
                    EnableButton(true);
                    start_flg = false;
                    return;
                }

                LogFile = null;
                LogFile2 = null;

                //ニコ生に接続
                ClearHosoData();
                ClearLog();

                //フォルダやファイルのチェック
                var save_dir = String.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments): props.SaveDir;
                if (!Directory.Exists(save_dir))
                {
                    AddLog("保存フォルダーが存在しません。", 2);
                    return;
                }

                var exec_file = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                exec_file = GetExecFile(exec_file);
                if (props.UseExternal != UseExternal.native)
                    if (!File.Exists(exec_file))
                    {
                        AddLog("実行ファイルがありません。", 2);
                        return;
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

#if TEST01
                string alias = "nico_01";
                string user = "aaa@aaa.com"; string pass = "vvvvv";
                string session = ""; string secure = "";
                using (var ddd = new Prop.Account("D:\\home\\tmp\\account.db"))
                {
                    ddd.CreateDbAccount();
                    if (ddd.WriteDbUser(alias, props.UserID, props.Password))
                        AddLog("メールID書き込みOK", 1);
                    if (ddd.ReadDbUser(alias, out user, out pass))
                        AddLog("user: " + user + " pass: " + pass, 1);
                    //if (ddd.WriteDbSession(alias, "ffffffffffff", "nnnnnnnnnnnn"))
                    //    AddLog("session書き込みOK", 1);
                    //if (ddd.ReadDbSession(alias, out session, out secure))
                    //    AddLog("session: " + session + " secure: " + secure, 1);
                }
                return;
#endif
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

                if (props.IsLogin == IsLogin.always)
                {
                    //ニコニコにログイン
                    switch (props.LoginMethod.ToString())
                    {
                        case "login":
                            using (var db = new Prop.Account(accountdbfile))
                            {
                                var alias = "nico_01";
                                string user = null;string pass = null;
                                if (!_nLiveNet.IsLoginStatus)
                                {
                                    if (db.GetSession(alias, _nLiveNet.GetCookieContainer()))
                                    {
                                        //ニコニコにアクセスする
                                        if (await _nLiveNet.IsLoginNicoAsync())
                                        {
                                            //ログインしていればOK
                                            AddLog("Logged in", 1);
                                            break;
                                        }
                                    }
                                    //ログイン処理
                                    AddLog("ログイン開始", 1);
                                    if (!db.ReadDbUser(alias, out user, out pass))
                                    {
                                        AddLog("Login Failed", 1);
                                        return;
                                    }
                                    if (!(await _nLiveNet.LoginNico(props.UserID, props.Password)))
                                    {
                                        AddLog("Login Failed", 1);
                                        return;
                                    }
                                    else
                                    {
                                        AddLog("Login OK", 1);
                                        db.SetSession(alias, _nLiveNet.GetCookieContainer());
                                    }
                                }
                                else
                                {
                                    AddLog("Logged in", 1);
                                }
                            }
                            break;
                        case "cookie":
                            //ブラウザのCookie読み込み処理
                            if (props.SelectedCookie != null)
                                AddLog(string.Format("Cookie: {0} {1}", props.SelectedCookie.BrowserName, props.SelectedCookie.ProfileName), 1);
                            if (!(await _nLiveNet.SetNicoCookie(props.SelectedCookie)))
                            {
                                AddLog("Cookie読み込み失敗", 1);
                                return;
                            }
                            AddLog("Cookie読み込みOK", 1);
                            if (!await _nLiveNet.IsLoginNicoAsync())
                            {
                                AddLog("ブラウザでログインし直してください", 1);
                                return;
                            }
                            break;
                    }
                }
                else
                {
                    AddLog("ログインなし", 1);
                }

                //番組情報を取得する
                bci = await _nLiveNet.GetNicoPageAsync(liveId);
                if (bci.Status != "ok")
                {
                    AddLog("放送情報が取得できませんでした。", 1);
                    AddLog("Status: " + bci.Error, 1);
                    return;
                }
                AddLog("Account: " + bci.AccountType, 1);
                var ws_ver = Regex.Match(bci.WsUrl, @"/wsapi/([^/]*)/").Groups[1].Value;
                if (ws_ver == "v1")
                    AddLog("WebSocket v1", 1);
                else if (ws_ver == "v2")
                    AddLog("WebSocket v2", 1);
                else
                    AddLog("WebSocket不明", 1);

                //ＴＳ開始時間
                int ii;
                if (int.TryParse(textBox2.Text, out ii))
                    bci.StartTs_Time = ii;

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
                epi.Sdir = string.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                epi.Exec = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                epi.Arg = props.ExecCommand[Props.ParseProtocol(props.Protocol.ToString())];
                epi.Sfile = bci.SetRecFileFormat(props.SaveFile);
                epi.Sfolder = bci.SetRecFolderFormat(props.SaveFolder);
                epi.Protocol = props.Protocol.ToString();
                epi.Seq = 0;
                ExecPsInfo.MakeRecDir(epi);

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                {
                    var file = ExecPsInfo.GetSaveFileSqlite3(epi);
                    if (bci.IsTimeShift()) file += Props.TIMESHIFT;
                    file += ".sqlite3";
                    epi.SaveFile = file;
                    _ndb = new NicoDb(this, epi.SaveFile);
                    _ndb.CreateDbAll();

                    _ndb.WriteDbKvsProps(bci.Data_Props);
                }

                //コメント情報
                if (props.IsComment)
                {
                    cmi = new CommentInfo(bci.User_Id);
                    cmi.OpenTime = bci.Open_Time;
                    cmi.BeginTime = bci.Begin_Time;
                    cmi.EndTime = bci.End_Time;
                    if (bci.IsTimeShift())
                        _cCtrl = new CommentControl();
                    else
                        _cCtrl = null;
                    _nNetComment = new NicoNetComment(this, bci, cmi, _nLiveNet, _ndb, _cCtrl);
                }
                var ri = new RetryInfo();
                _ri = ri;
                _ri.Count = props.Retry;

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                    _rHtml = new RecHtml(this, bci, _nNetComment, _nLiveNet.GetCookieContainer(), _ndb, _ri);
                else
                    _eProcess = new ExecProcess(this, bci, _nNetComment, _ri);
                _nNetStream = new NicoNetStream(this, bci, cmi, epi, _nNetComment, _eProcess, _rHtml, _ri);

                AddLog("broadcastId: " + bci.BcId, 9);
                AddLog("webSocketUrl: " + bci.WsUrl, 9);

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
                    if (_ndb != null)
                    {
                        _ndb.Dispose();
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
                            if (props.IsComment) _nNetComment = new NicoNetComment(this, bci, cmi, _nLiveNet, _ndb, _cCtrl);
                            if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                                _rHtml = new RecHtml(this, bci, _nNetComment, _nLiveNet.GetCookieContainer(), _ndb, _ri);
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
                if (!props.IsVideo && WsCommentStatus == 1)
                    ExecStatus = 1;
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
                    if (_cCtrl != null)
                    {
                        _cCtrl = null;
                    }
                    if (_ndb != null)
                    {
                        _ndb.Dispose();
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
            if (_cCtrl != null)
            {
                _cCtrl = null;
            }
            _ndb?.Dispose();

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
                LogFile = null;
                LogFile2 = null;

                using (var fo2 = new Form2(this, accountdbfile))
                {
                    fo2.ShowDialog();
                }
            }
            catch (Exception Ex)
            {
                AddLog("オプションメニューが開けませんでした。\r\n" + Ex.Message, 2);
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            try
            {
                LogFile = null;
                LogFile2 = null;

                for (int i = 0; i < files.Length; i++)
                {
                    StartExtract(files[i]);
                }
            }
            catch (Exception Ex)
            {
                if (_cCtrl != null)
                {
                    _cCtrl = null;
                }
                if (_ndb != null)
                {
                    _ndb.Dispose();
                }
                AddLog("ドラッグ＆ドロップできません。\r\n" + Ex.Message, 2);
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
    }
}