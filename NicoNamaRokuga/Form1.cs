using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Message;
using NicoNamaRokuga.Proc;
using NicoNamaRokuga.Rec;

namespace NicoNamaRokuga
{
    public partial class Form1 : Form
    {

        public static Props props;                   //設定

        private static bool IsBatchMode { get; set; } //引数指定で実行か？
        //0処理待ち 1録画準備 2録画中 3再接続 4中断 5変換処理中 9終了
        private volatile bool start_flg = false;
        private static int ProgramStatus { get; set; } //プログラム状態
        private int nicoRecordMode = -1;
        //-1:未処理 0:リアルタイム録画 1タイムシフト 2追っかけ再生

        //dispose するもの
        private NicoNetStream _nns = null;     //WebSocket(Stream)
        private NicoMessage _nms = null;       //MessageServer
        private ExecProcess _eProcess = null;  //Process
        private RecHtml _rHtml = null;         //RecHtml
        private NicoDb _ndb = null;            //NicoDb

        private CookieContainer cookiecontainer = null;
        private NicoLiveNet _nln = null;       //WebClient
        private BroadCastInfo bci = null;      //ストリームサーバー情報
        private ExecPsInfo epi = null;         //実行／保存ファイル情報

        private string liveId = null;

        private string accountdbfile;
        private readonly object lockObject = new object();  //情報表示用
        private readonly object lockObject2 = new object(); //実行ファイルのログ用
        private string LogFile;
        private string LogFile2;
        private string LogFile3;

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
                    if (_nns != null)   //
                    {
                        _nns.Close();
                    }
                    if (_nms != null)   //MessageServer
                    {
                        Task.Run(() => _nms.Disconnect());
                        _nms.Dispose();
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
                LogFile3 = null;

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
                LogFile3 = Props.GetDataPropsfile(save_dir, liveId);

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
            cookiecontainer = new CookieContainer();

            RetryInfo rti = null;                 //リトライ情報

            _nln = new NicoLiveNet();
            try
            {
                if (props.IsLogin == IsLogin.always)
                {
                    bool flag = false;
                    //ニコニコにログイン
                    switch (props.LoginMethod.ToString())
                    {
                        case "login":
                            using (var db = new Prop.Account(accountdbfile))
                            {
                                var alias = "nico_01";
                                string user = null; string pass = null;
                                if (!_nln.IsLoginStatus)
                                {
                                    if (db.GetSession(alias, cookiecontainer))
                                    {
                                        //ニコニコにアクセスする
                                        (flag, _, _) = await _nln.IsLoginNicoAsync(cookiecontainer);
                                        if (flag)
                                        {
                                            //ログインしていればOK
                                            AddLog("Already logged in", 1);
                                            break;
                                        }
                                    }
                                    //ログイン処理
                                    AddLog("ログイン開始", 1);
                                    if (!db.ReadDbUser(alias, out user, out pass))
                                    {
                                        AddLog("Login Failed: can't read user or pass", 1);
                                        return;
                                    }
                                    (flag, _, _) = await _nln.LoginNico(cookiecontainer, props.UserID, props.Password);
                                    if (!flag)
                                    {
                                        AddLog("Login Failed: login error", 1);
                                        return;
                                    }
                                    else
                                    {
                                        AddLog("Login OK", 1);
                                        db.SetSession(alias, cookiecontainer);
                                    }
                                }
                                else
                                {
                                    AddLog("Already logged in", 1);
                                }
                            }
                            break;
                        case "cookie":
                            //ブラウザのCookie読み込み処理
                            if (props.SelectedCookie != null)
                                AddLog(string.Format("Cookie: {0} {1}", props.SelectedCookie.BrowserName, props.SelectedCookie.ProfileName), 1);
                            (cookiecontainer, flag) = await _nln.SetNicoCookie(props.SelectedCookie);
                            if (!flag)
                            {
                                AddLog("Cookie読み込み失敗", 1);
                                return;
                            }
                            AddLog("Cookie読み込みOK", 1);
                            (flag, _, _) = await _nln.IsLoginNicoAsync(cookiecontainer);
                            if (!flag)
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
                string err;
                int neterr;
                (bci, err, neterr) = await _nln.GetNicoPageAsync(cookiecontainer, liveId);
                if (!string.IsNullOrEmpty(err))
                {
                    AddLog("放送情報が取得できませんでした。", 1);
                    AddLog("Status: " + err, 1);
                    return;
                }
                nicoRecordMode = bci.IsTimeShift() ? 1 : 0;
                AddDataProps(bci.Data_Props);
                AddLog("Account: " + bci.AccountType, 1);
                var ws_ver = Regex.Match(bci.WsUrl, @"/wsapi/([^/]*)/").Groups[1].Value;
                AddLog("Connect WebSocket", 1);

                //ＴＳ開始時間
                int ii;
                if (int.TryParse(textBox2.Text, out ii))
                    bci.StartTs_Time = ii;

                if (props.Protocol == Protocol.rtmp)
                    props.Protocol = Protocol.hls;

                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sdir = string.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                epi.Exec = GetExecFile(props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())]);
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
                    _nms = new NicoMessage(this, bci, _nln, _ndb);
                }

                var ri = new RetryInfo();
                rti = ri;
                rti.Count = props.Retry;

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                    _rHtml = new RecHtml(this, bci, _nms, cookiecontainer, _ndb, rti);
                else
                    _eProcess = new ExecProcess(this, bci, _nms, rti);
                _nns = new NicoNetStream(this, bci, epi, _nms, _eProcess, cookiecontainer, _rHtml, rti);

                AddLog("webSocketUrl: " + bci.WsUrl, 9);
                AddLog("frontendId: " + bci.FrontEndId, 9);
                //bci.FrontEndId = "90";

                //放送情報を表示
                DispHosoData(bci);

                //WebSocket接続開始
                _nns.Connect();

                //1秒おきに状態を調べて処理する
                start_flg = true;
                while (start_flg == true)
                {
                    await CheckStatus(rti);
                    await Task.Delay(1000);
                }

            }
            catch (Exception Ex)
            {
                AddLog(nameof(StartRec) + "() Error: \r\n" + Ex.Message, 2);
            }
        }

        private async Task CheckStatus(RetryInfo rti)
        {
            var MessageStatus = -1;
            var ExecStatus = -1;
            string err;
            int neterr;

            if (props.IsComment) MessageStatus = _nms.MessageStatus;
            if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                ExecStatus = _rHtml.PsStatus;
            else
                ExecStatus = _eProcess.PsStatus;
            try
            {
                if (_nns.WsStatus >= 2 || MessageStatus >= 2 || ExecStatus >= 2)
                {
                    //WebSocket再接続処理開始
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_nns != null)
                    {
                        _nns.Close();
                    }
                    if (_nms != null)
                    {
                        await _nms.Disconnect();
                        _nms.Dispose();
                    }
                    if (_ndb != null)
                    {
                        _ndb.Dispose();
                    }
                    if (rti.Count > 0)
                    {
                        if (_nns.WsStatus == 5)
                        {
                            AddLog(props.ReConnectTime2 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime2 - 1));
                            (bci, err, neterr)  = await _nln.GetNicoPageAsync(cookiecontainer, liveId);
                            if (string.IsNullOrEmpty(err))
                            {
                                AddLog("放送情報が取得できませんでした。", 1);
                                AddLog("Status: " + err, 1);
                                rti.Count--;
                                return;
                            }
                        }
                        else if (_nns.WsStatus == 4)
                        {
                            AddLog(props.ReConnectTime2 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime2 - 1));
                        }
                        else if (_nns.WsStatus == 3)
                        {
                            AddLog(props.ReConnectTime1 - 1 + "秒停止します。", 1);
                            await Task.Delay(TimeSpan.FromSeconds(props.ReConnectTime1 - 1));
                        }
                        if (ExecStatus != 1)
                        {
                            AddLog("再接続します。", 1);
                            if (props.IsComment) _nms = new NicoMessage(this, bci, _nln, _ndb);
                            if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                                _rHtml = new RecHtml(this, bci, _nms, cookiecontainer, _ndb, rti);
                            else
                                _eProcess = new ExecProcess(this, bci, _nms, rti);
                            _nns = new NicoNetStream(this, bci, epi, _nms, _eProcess, null, _rHtml, rti);
                            _nns.Connect();
                            rti.Count--;
                        }
                    }
                    else
                    {
                        AddLog("リトライ終了します。", 1);
                        ExecStatus = 1;
                    }
                }
                if (!props.IsVideo && MessageStatus == 1)
                    ExecStatus = 1;
                if (_nns.WsStatus == 1 || ExecStatus == 1) //終了処理
                {
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_nns != null)
                    {
                        _nns.Close();
                    }
                    if (_nms != null)
                    {
                        await _nms.Disconnect();
                        _nms.Dispose();
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
            if (_nns != null)
            {
                _nns?.Close();
                _nns?.Dispose();
                _nns = null;
            }
            if (_nms != null)
            {
                Task.Run(() => _nms.Disconnect());
                _nms?.Dispose();
                _nms = null;
            }
            if (_ndb != null)
            {
                _ndb?.Dispose();
                _ndb = null;
            }
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

        private async void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            try
            {
                ClearHosoData();
                ClearLog();

                var exec_file = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                exec_file = GetExecFile(exec_file);
                if (!File.Exists(exec_file))
                {
                    AddLog("実行ファイルがありません。", 2);
                    return;
                }

                var save_dir = String.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                if (!Directory.Exists(save_dir))
                {
                    AddLog("保存フォルダーが存在しません。", 2);
                    return;
                }

                LogFile = Props.GetLogfile(save_dir, "conv");
                LogFile2 = Props.GetExecLogfile(save_dir, "conv");
                LogFile3 = null;

                for (int i = 0; i < files.Length; i++)
                {
                    AddLog("出力開始します。", 1);
                    await Task.Run(() => StartExtract(files[i]));
                }
            }
            catch (Exception Ex)
            {
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