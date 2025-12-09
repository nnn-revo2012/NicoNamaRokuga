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

    public class NicoLiveNet
    {
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
        }

        private IList<KeyValuePair<string, string>> GetCookieList(WebClientEx wc)
        {
            var result = new Dictionary<string, string>();
            var cc = wc.cookieContainer;

            foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                result.Add(ck.Name.ToString(), ck.Value.ToString());

            return result.ToList();
        }

        private CookieContainer GetCookieContainer(WebClientEx wc)
        {
            return wc.cookieContainer;
        }

        private void SetCookieContainer(WebClientEx wc, CookieContainer cookie)
        {
            if (cookie != null)
                wc.cookieContainer = cookie;
            return;
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
        public async Task<(bool flag, string err, int neterr)> LoginNico(CookieContainer cookie, string mail, string pass)
        {
            bool flag = false;
            string err = null;
            int neterr = 0;

            var _wc = new WebClientEx();
            try
            {
                _wc.Encoding = Encoding.UTF8;
                _wc.Proxy = null;
                _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
                _wc.timeout = 30000;
                _wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
                SetCookieContainer(_wc, cookie);

                var ps = new NameValueCollection();
                //ログイン認証(POST)
                ps.Add("mail_tel", mail);
                ps.Add("password", pass);

                byte[] resArray = await _wc.UploadValuesTaskAsync(Props.NicoLoginUrl, ps).Timeout(_wc.timeout);
                var data = System.Text.Encoding.UTF8.GetString(resArray);
                flag = Regex.IsMatch(data, "user\\.login_status += +\\'login\\'", RegexOptions.Compiled) ? true : false;
                IsLoginStatus = flag;
                /*
                user.login_status = 'login';
                user.member_status = 'premium';
                user.ui_area = 'jp';
                user.ui_lang = 'ja-jp';
                */
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
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(LoginNico), Ex);
                err = Ex.Message;
            }
            finally
            {
                cookie = GetCookieContainer(_wc);
                _wc?.Dispose();
            }

            return (flag, err, neterr);
        }


        //ログインしているかどうか取得
        public async Task<(bool flag, string err, int neterr)> IsLoginNicoAsync(CookieContainer cookie)
        {
            bool flag = false;
            string err = null;
            int neterr = 0;

            var _wc = new WebClientEx();
            try
            {
                _wc.Encoding = Encoding.UTF8;
                _wc.Proxy = null;
                _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
                _wc.timeout = 30000;
                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");
                SetCookieContainer(_wc, cookie);

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoDomain).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(hs))
                {
                    _wc?.Dispose();
                    return (false, "result is null", neterr);
                }
                flag = Regex.IsMatch(hs, "user\\.login_status += +\\'login\\'", RegexOptions.Compiled) ? true : false;
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(IsLoginNicoAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(IsLoginNicoAsync), Ex);
                err = Ex.Message;
            }
            finally
            {
                _wc?.Dispose();
            }
            return (flag, err, neterr);
        }


        //生放送ページから放送情報を取得
        public async Task<(BroadCastInfo bci, string err, int neterr)> GetNicoPageAsync(CookieContainer cookie, string nicoUrl)
        {
            var bci = new BroadCastInfo(null, null, null, null);
            bci.Status = "";
            bci.Error = "";
            string err = null;
            int neterr = 0;

            var liveid = GetLiveID(nicoUrl);
            if (string.IsNullOrEmpty(liveid)) return (bci, "liveid is null", neterr);

            var _wc = new WebClientEx();
            try
            {

                string providertype;

                _wc.Encoding = Encoding.UTF8;
                _wc.Proxy = null;
                _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
                _wc.timeout = 30000;
                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");
                SetCookieContainer(_wc, cookie);

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoLiveUrl + liveid).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(hs))
                {
                    _wc?.Dispose();
                    return (bci, "result is null", neterr);
                }

                if (hs.IndexOf("window.NicoGoogleTagManagerDataLayer = [];") > 0)
                {
                    _wc?.Dispose();
                    return (bci, "Need login.", neterr);
                }

                bci.User_Id = Regex.Match(hs, "&quot;user_id&quot;:([^,]*),", RegexOptions.Compiled).Groups[1].Value;
                bci.AccountType = Regex.Match(hs, "&quot;member_status&quot;:&quot;([^\\&]*)&quot;", RegexOptions.Compiled).Groups[1].Value;
                providertype = Regex.Match(hs, "&quot;content_type&quot;:&quot;([^\\&]*)&quot;", RegexOptions.Compiled).Groups[1].Value;
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
                        err = "require_community_member";
                    } else if (status == "ENDED" && tsenabled == "true")
                    {
                        err = "notlogin or login premium account";
                    }
                    else
                    {
                        err = "program closed";
                    }
                    return (bci, err, neterr);
                }
                //bci.AuTkn = Regex.Match(ttt, @"""audienceToken"":""([^""]+)""").Groups[1].Value; ;
                bci.FrontEndId = Regex.Match(ttt, @"""frontendId"":(\d*)").Groups[1].Value; ;
                //Clipboard.SetText(ttt);
                var dprops = JObject.Parse(ttt);
                //Clipboard.SetText(dprops.ToString());
                var dprogram = (JObject)dprops["program"];
                bci.LiveId = dprogram["nicoliveProgramId"].ToString();
                bci.Title = dprogram["title"].ToString();
                bci.Description = dprogram["description"].ToString();
                bci.Provider_Type = providertype;
                JToken aaa;
                if (dprogram.TryGetValue("supplier", out aaa))
                {
                    bci.Provider_Name = dprogram["supplier"]["name"].ToString();
                    if (providertype == "user")
                        bci.Provider_Id = Props.GetChNo(dprogram["supplier"]["pageUrl"].ToString());
                    else
                        bci.Provider_Id = dprops["socialGroup"]["id"].ToString();
                }
                bci.FollowerOnly = (bool)dprogram["isFollowerOnly"];
                bci.Open_Time = (long)dprogram["openTime"];
                bci.Begin_Time = (long)dprogram["beginTime"];
                bci.End_Time = (long)dprogram["endTime"];
                bci.VposBase_Time = (long)dprogram["vposBaseTime"];
                bci.OnAirStatus = dprogram["status"].ToString();
                bci.Server_Time = (long)dprops["site"]["serverTime"];
                bci.StreamType = "dlive";
                if (providertype == "user")
                {
                    bci.Community_Id = "co0";
                    bci.Community_Title = "削除されたコミュニティ";
                    //bci.Community_Thumbnail = dprops["socialGroup"]["thumbnailSmallImageUrl"].ToString();
                }
                else
                {
                    bci.Community_Id = dprops["socialGroup"]["id"].ToString();
                    bci.Community_Title = dprops["socialGroup"]["name"].ToString();
                    bci.Community_Thumbnail = dprops["socialGroup"]["thumbnailSmallImageUrl"].ToString();
                }
                bci.Status = "ok";
                bci.Error = "";
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetNicoPageAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
                err = Ex.Message;
            }
            finally
            {
                _wc?.Dispose();
            }
            return (bci, err, neterr);
        }

        //ＴＳコメント取得用のwaybackkeyを取得
/*
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
*/

        //*************** Cookie用 *******************
        // 指定Cookie情報のブラウザーのニコニコのCookieを取得してセット
        public async Task<(CookieContainer cookies, bool)> SetNicoCookie(CookieSourceInfo csi)
        {
            CookieContainer cookies = new CookieContainer();
            try
            {
                if (csi == null) return (cookies, false);

                // ニコニコのCookieを取得
                var targetUrl = new Uri(Props.NicoDomain);
                var cookieGetter =
                    await CookieGetters.Default.GetInstanceAsync(csi, true);
                var result = await cookieGetter.GetCookiesAsync(targetUrl);
                if (result.Status != CookieImportState.Success) return (cookies, false);
                if (result.Cookies.Count <= 0) return (cookies, false);

                if (IsDebug)
                {
                    foreach (var ck in result.Cookies)
                        Debug.WriteLine(string.Format("result: \r\n{0}\r\n", ck));
                }

                // Cookieをセット
                foreach (Cookie ck in result.Cookies)
                    if (ck.Name == "user_session" || ck.Name == "user_session_secure")
                        cookies.Add(new Cookie(ck.Name, ck.Value, "/", ".nicovideo.jp"));
                    else if (ck.Name == "age_auth")
                        cookies.Add(ck);
                IsLoginStatus = true;

                if (IsDebug)
                {
                    var cc = cookies;
                    Debug.WriteLine(string.Format("Cookie GetCookieHeader: \r\n{0}\r\n",
                        cc.GetCookieHeader(targetUrl)));
                }
            }
            catch (Exception Ex) //エラー
            {
                DebugWrite.Writeln(nameof(SetNicoCookie), Ex);
                return (cookies, false);	
            }

            return (cookies, true);
        }

    }
}
