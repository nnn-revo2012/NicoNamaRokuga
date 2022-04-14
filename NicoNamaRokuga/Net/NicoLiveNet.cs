using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Xml;
using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SunokoLibrary.Application;

using NicoNamaRokuga.Prop;

namespace NicoNamaRokuga.Net
{

    static class TimeoutExtention
    {
        public static async Task Timeout(this Task task, int timeout)
        {
            var delay = Task.Delay(timeout);
            if (await Task.WhenAny(task, delay) == delay)
            {
                throw new TimeoutException();
            }
        }

        public static async Task<T> Timeout<T>(this Task<T> task, int timeout)
        {
            await ((Task)task).Timeout(timeout);
            return await task;
        }
    }

    public class NicoLiveNet : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private WebClientEx _wc = null;

        private class WebClientEx : WebClient
        {
            public CookieContainer cookieContainer = new CookieContainer();
            public int timeout;

            protected override WebRequest GetWebRequest(Uri address)
            {
                var wr = base.GetWebRequest(address);

                HttpWebRequest hwr = wr as HttpWebRequest;
                if (hwr != null)
                {
                    hwr.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; //圧縮を有効化
                    hwr.CookieContainer = cookieContainer; //Cookie
                    hwr.Timeout = timeout;
                }
                return wr;
            }
        }

        //Debug
        public bool IsDebug { get; set; }

        public bool IsLoginStatus { get; private set; }

        public NicoLiveNet()
        {
            IsDebug = false;

            IsLoginStatus = false;

            var wc = new WebClientEx();
            _wc = wc;

            _wc.Encoding = Encoding.UTF8;
            _wc.Proxy = null;
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
            _wc.timeout = 30000;
        }

        ~NicoLiveNet()
        {
            this.Dispose();
        }


        public IList<KeyValuePair<string, string>> GetCookieList()
        {
            var result = new Dictionary<string, string>();
            var cc = _wc.cookieContainer;

            foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                result.Add(ck.Name.ToString(), ck.Value.ToString());

            return result.ToList();
        }

        public CookieContainer GetCookieContainer()
        {
            return _wc.cookieContainer;
        }

        //*************** URL系 *******************

        //放送URLから放送IDをゲット(lv00000000000)
        public static string GetLiveID(string liveUrl)
        {
            var stmp = Regex.Match(liveUrl, "(lv[0-9]+)").Groups[1].Value;
            if (string.IsNullOrEmpty(stmp)) stmp = null;
            return stmp;
        }

        //放送IDから放送URLをゲット
        public static string GetNicoPageUrl(string liveID)
        {
            if (string.IsNullOrEmpty(liveID)) return null;
            return Props.NicoLiveUrl + liveID;
        }

        //*************** HTTP系 *******************

        //ニコニコにログイン
        public async Task<bool> LoginNico(string mail, string pass)
        {

            var flag = false;
            try {
                var ps = new NameValueCollection();
                //ログイン認証(POST)
                ps.Add("mail_tel", mail);
                ps.Add("password", pass);

                byte[] resArray = await _wc.UploadValuesTaskAsync(Props.NicoLoginUrl, ps).Timeout(_wc.timeout);
                string ttt = _wc.ResponseHeaders.Get("x-niconico-authflag");
                int authflg;
                if (int.TryParse(ttt, out authflg))
                {
                    //ヘッダーに x-niconico-authflag があれば正常にログイン
                    if (authflg > 0)
                    {
                        flag = true;
                        IsLoginStatus = true;
                    }
                }
                else
                {
                    //エラー
                }
                IsLoginStatus = true;

                if (IsDebug)
                {
                    //responseヘッダーの数と内容を表示
                    var strtmp = string.Format("Login Headers: {0}\r\n\r\n", _wc.ResponseHeaders.Count);
                    for (int i = 0; i < _wc.ResponseHeaders.Count; i++)
                        strtmp += string.Format("{0}: {1}\r\n", _wc.ResponseHeaders.GetKey(i),
                            _wc.ResponseHeaders.Get(i));
                    MessageBox.Show(strtmp);
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(LoginNico), Ex);
                return flag;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(LoginNico), Ex);
                return flag;
            }

            return flag;
        }

        //ログインしているかどうか取得
        public async Task<bool> IsLoginNicoAsync()
        {
            try
            {
                var hs = await _wc.DownloadStringTaskAsync(Props.NicoDomain).Timeout(_wc.timeout);
                return Regex.IsMatch(hs, "user\\.login_status += +\\'login\\'", RegexOptions.Compiled) ? true : false;
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(IsLoginNicoAsync), Ex);
                return false;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(IsLoginNicoAsync), Ex);
                return false;
            }
        }

        //生放送ページから放送情報を取得
        public async Task<BroadCastInfo> GetNicoPageAsync(string nicoUrl)
        {
            var bci = new BroadCastInfo(null, null, null, null);
            bci.Status = "fail";
            bci.Error = "notfound";

            try
            {
                var liveid = GetLiveID(nicoUrl);
                if (string.IsNullOrEmpty(liveid)) return bci;

                var providertype = "unama";
                bci.Provider_Type = providertype;

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoLiveUrl + liveid).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(hs)) return bci;
                if (hs.IndexOf("window.NicoGoogleTagManagerDataLayer = [];") > 0)
                {
                    bci.Error = "notlogin";
                    return bci;
                }
                bci.User_Id = Regex.Match(hs, "\"user_id\":([^,]*),", RegexOptions.Compiled).Groups[1].Value;
                bci.AccountType = Regex.Match(hs, "\"member_status\":\"([^,]*)\",", RegexOptions.Compiled).Groups[1].Value;
                providertype = Regex.Match(hs, "\"content_type\":\"([^\"]*)\"", RegexOptions.Compiled).Groups[1].Value;
                bci.Provider_Type = providertype;
                var ttt = WebUtility.HtmlDecode(Regex.Match(hs, "<script +id=\"embedded-data\" +data-props=\"([^\"]*)\"></script>", RegexOptions.Compiled).Groups[1].Value);
                bci.Data_Props = ttt;
                bci.WsUrl = Regex.Match(ttt, @"""webSocketUrl"":""([^""]+)""").Groups[1].Value;
                if (string.IsNullOrEmpty(bci.WsUrl))
                {
                    var tsenabled = Regex.Match(ttt, @"""isTimeshiftDownloadEnabled"":(\w+)").Groups[1].Value.ToLower();
                    var follower = Regex.Match(ttt, @"""isFollowerOnly"":(\w+)").Groups[1].Value.ToLower();
                    var status = Regex.Match(ttt, @"""status"":""(\w+)""").Groups[1].Value;
                    if ((status == "ON_AIR" && follower == "true")
                        || (status == "ENDED" && follower == "true" && tsenabled == "true"))
                    {
                        bci.Error = "require_community_member";
                    } else if (status == "ENDED" && tsenabled == "true")
                    {
                        bci.Error = "notlogin or login premium account";
                    }
                    else
                    {
                        bci.Error = "program closed";
                    }
                    return bci;
                }
                bci.AuTkn = Regex.Match(ttt, @"""audienceToken"":""([^""]+)""").Groups[1].Value; ;
                bci.FrontEndId = Regex.Match(ttt, @"""frontendId"":(\d*)").Groups[1].Value; ;
                //Clipboard.SetText(ttt);
                var dprops = JObject.Parse(ttt);
                //Clipboard.SetText(dprops.ToString());
                var dprogram = (JObject)dprops["program"];
                bci.LiveId = dprogram["nicoliveProgramId"].ToString();
                bci.Title = dprogram["title"].ToString();
                bci.Description = dprogram["description"].ToString();
                bci.Provider_Id = providertype;
                bci.Provider_Name = "公式生放送";
                bci.Community_Thumbnail = dprogram["thumbnail"]["small"].ToString();
                JToken aaa;
                if (dprogram.TryGetValue("supplier", out aaa))
                {
                    bci.Provider_Name = dprogram["supplier"]["name"].ToString();
                    if (providertype == "user")
                        bci.Provider_Id = Props.GetChNo(dprogram["supplier"]["pageUrl"].ToString());
                }
                bci.FollowerOnly = (bool)dprogram["isFollowerOnly"];
                bci.Open_Time = (long)dprogram["openTime"];
                bci.Begin_Time = (long)dprogram["beginTime"];
                bci.End_Time = (long)dprogram["endTime"];
                bci.OnAirStatus = dprogram["status"].ToString();
                bci.Server_Time = (long)dprops["site"]["serverTime"];

                bci.Community_Id = providertype;
                bci.Community_Title = "公式生放送";
                if (dprops["socialGroup"].Count() > 0)
                {
                    bci.Community_Id = dprops["socialGroup"]["id"].ToString();
                    bci.Community_Title = dprops["socialGroup"]["name"].ToString();
                    //bci.Community_Thumbnail = dprops["socialGroup"]["thumbnailSmallImageUrl"].ToString();
                }
                bci.Status = "ok";
                bci.Error = "";
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetNicoPageAsync), Ex);
                bci.Error = Ex.Status.ToString();
                return bci;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
                bci.Error = Ex.Message;
                return bci;
            }

            return bci;
        }

        //ＴＳコメント取得用のwaybackkeyを取得
        public async Task<string> GetWayBackKeyAsync(string thread_id)
        {
            var result = string.Empty;
            if (string.IsNullOrEmpty(thread_id)) return result;
            try
            {
                var stmp = Props.NicoWayBackKey + "?thread=" + thread_id;
                result = await _wc.DownloadStringTaskAsync(stmp).Timeout(_wc.timeout);
                result = result.Split('=')[1];
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetWayBackKeyAsync), Ex);
                return result;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetWayBackKeyAsync), Ex);
                return result;
            }

            return result;
        }


        //*************** Cookie用 *******************
        // 指定Cookie情報のブラウザーのニコニコのCookieを取得してセット
        public async Task<bool> SetNicoCookie(CookieSourceInfo csi)
        {
            try
            {
                if (csi == null) return false;

                // ニコニコのCookieを取得
                var targetUrl = new Uri(Props.NicoDomain);
                var cookieGetter =
                    await CookieGetters.Default.GetInstanceAsync(csi, true);
                var result = await cookieGetter.GetCookiesAsync(targetUrl);
                if (result.Status != CookieImportState.Success) return false;
                if (result.Cookies.Count <= 0) return false;

                if (IsDebug)
                {
                    foreach (var ck in result.Cookies)
                        Debug.WriteLine(string.Format("result: \r\n{0}\r\n", ck));
                }

                // Cookieをセット
                foreach (Cookie ck in result.Cookies)
                    if (ck.Name == "user_session" || ck.Name == "user_session_secure")
                        _wc.cookieContainer.Add(new Cookie(ck.Name, ck.Value, "/", ".nicovideo.jp"));
                    else if (ck.Name == "age_auth")
                        _wc.cookieContainer.Add(ck);
                IsLoginStatus = true;

                if (IsDebug)
                {
                    var cc = _wc.cookieContainer;
                    Debug.WriteLine(string.Format("Cookie GetCookieHeader: \r\n{0}\r\n",
                        cc.GetCookieHeader(targetUrl)));
                }
            }
            catch (Exception Ex) //エラー
            {
                DebugWrite.Writeln(nameof(SetNicoCookie), Ex);
                return false;	
            }

            return true;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _wc?.Dispose();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            //GC.SuppressFinalize(this);
        }

    }
}
