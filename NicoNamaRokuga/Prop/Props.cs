using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

using SunokoLibrary.Application;

namespace NicoNamaRokuga.Prop
{

    public enum IsLogin { always, none, };
    public enum LoginMethod { login, cookie, };
    public enum QTypes { super_high, high, normal, low, super_low, };
    public enum Protocol { hls, rtmp, };
    public enum UseExternal { native, ext1, ext2, ext3, };

    public class Props
    {
        //定数設定
        public static readonly string UserAgent = "Mozilla/5.0 (" + Ver.GetAssemblyName() + "; " + Ver.Version + ")";
        //public static readonly string UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36";
        public static readonly string NicoDomain = "https://www.nicovideo.jp/";

        public static readonly string NicoLiveUrl = "https://live2.nicovideo.jp/watch/";
        public static readonly string NicoCommUrl = "https://com.nicovideo.jp/community/";
        public static readonly string NicoChannelUrl = "https://ch.nicovideo.jp/";
        public static readonly string NicoUserUrl = "https://www.nicovideo.jp/user/";

        public static readonly string NicoLoginUrl = "https://secure.nicovideo.jp/secure/login?site=niconico";
        public static readonly string NicoGetPlayerStatus = "https://ow.live.nicovideo.jp/api/getplayerstatus?v=";
        public static readonly string NicoWayBackKey = "https://live.nicovideo.jp/api/getwaybackkey";
        //public static readonly string NicoCasApi = "https://api.cas.nicovideo.jp/v1/services/live/programs/";

        public static readonly string TIMESHIFT = "(TS)";

        public static readonly IDictionary<string, string> WsHeaderStream =
            new Dictionary<string, string>()
            {{"Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits"}};

        //public static readonly IDictionary<string, string> WsHeaderStream =
        //    new Dictionary<string, string>()
        //    {{"User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36"},
        //     {"Origin", "https://live2.nicovideo.jp"},
        //     {"Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits"}};

        public static readonly IDictionary<string, string> WsHeaderComment =
            new Dictionary<string, string>()
            {{"Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits"},
             {"Sec-WebSocket-Protocol", "msg.nicovideo.jp#json"}};

        public static readonly string[] Quality =
            { "3Mbps (suoer_high)","2Mbps (high)", "1Mbps (normal)", "384Kbps (low)", "192Kbps (super_low)" };

        public static readonly string Commnet_SeetNo = "/hb ifseetno";

        public static readonly string[][] ReplaceWords =
            {
            new[] {"?PID?","lv1234567","ProgramId。lv1234567のような文字列。"},
            new[] {"?UNAME?","ユーザ名","ユーザ名。公式の場合、official"},
            new[] {"?UID?","ユーザID","ユーザID。nicovideo.jp/user/に続く数字の列。公式の場合、official"},
            new[] {"?CNAME?","コミュニティ名","コミュニティ名。公式の場合、official"},
            new[] {"?CID?","コミュニティID","コミュニティID。co1234のような文字列。公式の場合、official"},
            new[] {"?TITLE?","放送タイトル","放送タイトル。"},
            new[] {"?YEAR?","2019","年4桁(開演時刻)"},
            new[] {"?MONTH?","09","月2桁(開演時刻)"},
            new[] {"?DAY?","01","日2桁(開演時刻)"},
            new[] {"?DAY8?","20190901","年4桁,月2桁,日2桁"},
            new[] {"?DAY6?","190901","年2桁,月2桁,日2桁"},
            new[] {"?HOUR?","18","時2桁"},
            new[] {"?MINUTE?","31","分2桁"},
            new[] {"?SECOND?","02","秒2桁"},
            new[] {"?TIME6?","183102","時2桁,分2桁,秒2桁"},
            new[] {"?TIME4?","3102","時2桁,分2桁"}
            //new[] {"", "",""}
            };

        public static readonly IDictionary<string, string> PropLists =
            new Dictionary<string, string>()
        {
            // "community"
            {"comId", "community.id"}, // "co\d+"
            // "program"
            {"beginTime", "program.beginTime"}, // integer
            {"description", "program.description"}, // 放送説明
            {"endTime", "program.endTime"}, // integer
            {"isFollowerOnly", "program.isFollowerOnly"}, // bool
            {"isPrivate", "program.isPrivate"}, // bool
            {"mediaServerType","program.mediaServerType"}, // "DMC"
            {"nicoliveProgramId", "program.nicoliveProgramId"}, // "lv\d+"
            {"openTime", "program.openTime"}, // integer
            {"providerType", "program.providerType"}, // "community"
            {"status", "program.status"}, //
            {"userName", "program.supplier.name"}, // ユーザ名
            {"userPageUrl", "program.supplier.pageUrl"}, // "https://www.nicovideo.jp/user/\d+"
            {"title", "program.title"}, // title
            {"vposBaseTime", "program.vposBaseTime"}, // integer
            // "site"
            {"serverTime", "site.serverTime"}, // integer
            // "socialGroup"
            {"socDescription", "socialGroup.description"}, // コミュ説明
            {"socId", "socialGroup.id"}, // "co\d+" or "ch\d+"
            {"socLevel", "socialGroup.level"}, // integer
            {"socName", "socialGroup.name"}, // community name
            {"socType", "socialGroup.type"}, // "community"
            // "user"
            {"accountType", "user.accountType"}, // "premium"
            {"isLoggedIn", "user.isLoggedIn"}, // bool
        };

        public bool IsDebug { get; set; }

        public IsLogin IsLogin { get; set; }
        public LoginMethod LoginMethod { get; set; }
        public string UserID { get; set; }
        public string Password { get; set; }
        public CookieSourceInfo SelectedCookie { get; set; }
        public bool IsAllCookie { get; set; }
        public string SaveDir { get; set; }
        public string SaveFolder { get; set; }
        public string SaveFile { get; set; }
        public QTypes QuarityType { get; set; }
        public Protocol Protocol { get; set; }
        public UseExternal UseExternal { get; set; }
        public string[] ExecFile { get; set; }
        public string[] ExecCommand { get; set; }
        public string[] BreakCommand { get; set; }
        public int Retry { get; set; }
        public int ReConnectTime1 { get; set; }
        public int ReConnectTime2 { get; set; }
        public int Timeout1 { get; set; }
        public int Timeout2 { get; set; }
        public bool IsLogging { get; set; }
        public bool IsComment { get; set; }
        public bool IsSeetNo { get; set; }
        public bool IsVideo { get; set; }


        public Props()
        {
            ExecFile = new string[2];
            ExecCommand = new string[2];
            BreakCommand = new string[2];
        }

        public static int ParseProtocol(string str)
        {
            return (int)(Protocol)Enum.Parse(typeof(Protocol), str);
        }

        public static bool IsProtocol(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;
            else
                return Enum.IsDefined(typeof(Protocol), str);
        }

        public static int ParseUseExternal(string str)
        {
            return (int)(UseExternal)Enum.Parse(typeof(UseExternal), str);
        }

        public static bool IsUseExternal(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;
            else
                return Enum.IsDefined(typeof(UseExternal), str);
        }

        public static int ParseQTypes(string str)
        {
            return (int)(QTypes)Enum.Parse(typeof(QTypes), str);
        }

        public static bool IsQTypes(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;
            else
                return Enum.IsDefined(typeof(QTypes), str);
        }

        public bool LoadData(string accountdbfile)
        {
            try
            {
                using (var db = new Account(accountdbfile))
                {
                    var alias = "nico_01";
                    string user = ""; string pass = "";
                    db.ReadDbUser(alias, out user, out pass);
                    this.UserID = user;
                    this.Password = pass;
                }
                this.IsLogin =
                    (IsLogin)Enum.Parse(typeof(IsLogin), Properties.Settings.Default.IsLogin);
                this.LoginMethod =
                    (LoginMethod)Enum.Parse(typeof(LoginMethod), Properties.Settings.Default.LoginMethod);
                this.SelectedCookie = Properties.Settings.Default.SelectedCookie;
                this.IsAllCookie = Properties.Settings.Default.IsAllCookie;
                this.SaveDir = Properties.Settings.Default.SaveDir;
                this.SaveFolder = Properties.Settings.Default.SaveFolder;
                this.SaveFile = Properties.Settings.Default.SaveFile;
                this.QuarityType =
                    (QTypes)Enum.Parse(typeof(QTypes), Properties.Settings.Default.QualityType);
                this.Protocol =
                    (Protocol)Enum.Parse(typeof(Protocol), Properties.Settings.Default.Protocol);
                this.UseExternal =
                    (UseExternal)Enum.Parse(typeof(UseExternal), Properties.Settings.Default.UseExternal);
                this.ExecFile = Properties.Settings.Default.ExecFile.Split(';');
                this.ExecCommand = Properties.Settings.Default.ExecCommand.Split(';');
                this.BreakCommand = Properties.Settings.Default.BreakCommand.Split(';');
                this.Retry = Properties.Settings.Default.Retry;
                this.ReConnectTime1 = Properties.Settings.Default.ReConnectTime1;
                this.ReConnectTime2 = Properties.Settings.Default.ReConnectTime2;
                this.Timeout1 = Properties.Settings.Default.Timeout1;
                this.Timeout2 = Properties.Settings.Default.Timeout2;
                this.IsLogging = Properties.Settings.Default.IsLogging;
                this.IsComment = Properties.Settings.Default.IsComment;
                this.IsSeetNo = Properties.Settings.Default.IsSeetNo;
                this.IsVideo = Properties.Settings.Default.IsVideo;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(LoadData), Ex);
                return false;
            }
            return true;
        }

        public bool SaveData(string accountdbfile, bool acc_flg)
        {
            try
            {
                Properties.Settings.Default.IsLogin = this.IsLogin.ToString().ToLower();
                Properties.Settings.Default.LoginMethod = this.LoginMethod.ToString().ToLower();
                Properties.Settings.Default.SelectedCookie = this.SelectedCookie;
                Properties.Settings.Default.IsAllCookie = this.IsAllCookie;
                Properties.Settings.Default.SaveDir = this.SaveDir;
                Properties.Settings.Default.SaveFolder = this.SaveFolder;
                Properties.Settings.Default.SaveFile = this.SaveFile;
                Properties.Settings.Default.QualityType = this.QuarityType.ToString().ToLower();
                Properties.Settings.Default.Protocol = this.Protocol.ToString().ToLower();
                Properties.Settings.Default.UseExternal = this.UseExternal.ToString().ToLower();
                Properties.Settings.Default.ExecFile = String.Join(";", this.ExecFile);
                Properties.Settings.Default.ExecCommand = String.Join(";", this.ExecCommand);
                Properties.Settings.Default.BreakCommand = String.Join(";", this.BreakCommand);
                Properties.Settings.Default.Retry = this.Retry;
                Properties.Settings.Default.ReConnectTime1 = this.ReConnectTime1;
                Properties.Settings.Default.ReConnectTime2 = this.ReConnectTime2;
                Properties.Settings.Default.Timeout1 = this.Timeout1;
                Properties.Settings.Default.Timeout2 = this.Timeout2;
                Properties.Settings.Default.IsLogging = this.IsLogging;
                Properties.Settings.Default.IsComment = this.IsComment;
                Properties.Settings.Default.IsSeetNo = this.IsSeetNo;
                Properties.Settings.Default.IsVideo = this.IsVideo;
                Properties.Settings.Default.Save();
                if (acc_flg == true)
                {
                    using (var db = new Account(accountdbfile))
                    {
                        var alias = "nico_01";
                        db.CreateDbAccount();
                        db.WriteDbUser(alias, this.UserID, this.Password);
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(SaveData), Ex);
                return false;
            }
            return true;
        }

        public bool ReloadData(string accountdbfile)
        {
            Properties.Settings.Default.Reload();
            return this.LoadData(accountdbfile);
        }

        public bool ResetData(string accountdbfile)
        {
            var result = false;
            Properties.Settings.Default.Reset();
            result = this.LoadData(accountdbfile);
            this.UserID = "";
            this.Password = "";
            return result;
        }

        //設定ファイルの場所をGet
        public static string GetSettingDirectory()
        {
            //設定ファイルの場所
            var config = ConfigurationManager.OpenExeConfiguration(
                ConfigurationUserLevel.PerUserRoamingAndLocal);
            return Path.GetDirectoryName(config.FilePath);
        }

        //アプリケーションの場所をGet
        public static string GetApplicationDirectory()
        {
            //アプリケーションの場所
            var tmp = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(tmp);
        }

        //ログファイル名をGet
        public static string GetLogfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        //実行ログファイル名をGet
        public static string GetExecLogfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_exec_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        //data-propsファイル名をGet
        public static string GetDataPropsfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_data-props_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        public static string GetDirSepString()
        {
            return Path.DirectorySeparatorChar.ToString();
        }

        public static string GetLiveUrl(string liveid)
        {
            return NicoLiveUrl + liveid;
        }

        public static string GetChannelUrl(string channelid)
        {
            return NicoChannelUrl + channelid;
        }

        public static string GetCommUrl(string channelid)
        {
            return NicoCommUrl + channelid;
        }

        public static string GetUserUrl(string userid)
        {
            return NicoUserUrl + userid;
        }

        public static string GetProviderType(string type)
        {
            var result = "？？";
            switch (type)
            {
                case "community":
                    result = "コミュニティ";
                    break;
                case "user":
                    result = "コミュニティ";
                    break;
                case "channel":
                    result = "チャンネル";
                    break;
                case "official":
                    result = "公式生放送";
                    break;
                case "cas":
                    result = "実験放送";
                    break;
                default:
                    break;
            }
            return result;
        }

        private static readonly Regex RgxChNo = new Regex("/([^/]+)$", RegexOptions.Compiled);
        public static string GetChNo(string url)
        {
            return RgxChNo.Match(url).Groups[1].Value;
        }

        public static long GetUnixTime() {
            return (long)(((TimeSpan)(DateTime.Now - new DateTime(1970, 1, 1))).TotalSeconds);
        }

        public static DateTime GetUnixToDateTime(long unix) {
            DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return UNIX_EPOCH.AddSeconds(unix).ToLocalTime();
        }

        public static long GetLongParse(string ttt)
        {
            double dd = -1.0D;
            double.TryParse(ttt, out dd);
            return (long )dd;

        }

    }
}
