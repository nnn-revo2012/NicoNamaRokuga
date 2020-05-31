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
    public class GetPlayerStatusInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }

        public string Id { set; get; }
        public string Title { set; get; }
        public string User_Id { set; get; }
        public string Provider_Type { set; get; }
        public string Default_Community { set; get; }
        public string Is_Premium_Channel { set; get; } //チャンネルのみ
        public string Channel_Stream_Status { set; get; } //チャンネルのみ
        public string Owner_Id { set; get; }
        public string Owner_Name { set; get; } //ユーザーのみあり
        public long   Start_Time { set; get; }
        public long   End_Time { set; get; }

        public GetPlayerStatusInfo()
        {
            this.Status = null;
            this.Error = null;
        }
    }

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
                ps.Add("mail", mail);
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

        //GetPlayerStatusを実行
        public async Task<GetPlayerStatusInfo> GetPlayerStatusAsync(string nicoUrl)
        {

            var gpsi = new GetPlayerStatusInfo();
            gpsi.Status = "fail";
            gpsi.Error = "notfound";

            try
            {
                var stmp = GetLiveID(nicoUrl);
                if (string.IsNullOrEmpty(stmp)) return gpsi;

                stmp = Props.NicoGetPlayerStatus + stmp;
                var xhtml = await _wc.DownloadStringTaskAsync(stmp).Timeout(_wc.timeout);
                var doc = new XmlDocument();
                doc.LoadXml(xhtml);
                gpsi.Status = doc.DocumentElement.GetAttribute("status");
                if (gpsi.Status != "ok")
                {
                    //エラーメッセージを入れてリターン
                    gpsi.Error = doc.GetElementsByTagName("code").Item(0).InnerText;
                    return gpsi;
                }
                var nodes = doc.GetElementsByTagName("stream");
                if (nodes.Count > 0)
                {
                    gpsi.Id = nodes.Item(0)["id"].InnerText;
                    gpsi.Title = nodes.Item(0)["title"].InnerText;
                    gpsi.Provider_Type = nodes.Item(0)["provider_type"].InnerText;
                    gpsi.Default_Community = nodes.Item(0)["default_community"].InnerText;
                    gpsi.Owner_Id = nodes.Item(0)["owner_id"].InnerText;
                    gpsi.Owner_Name = nodes.Item(0)["owner_name"].InnerText;
                    gpsi.Start_Time = long.Parse(nodes.Item(0)["start_time"].InnerText);
                    gpsi.End_Time = long.Parse(nodes.Item(0)["end_time"].InnerText);
                    switch (gpsi.Provider_Type)
                    {
                        case "channel":
                            //gpsi.Is_Premium_Channel = nodes.Item(0)["is_premium_channel"].InnerText;
                            //gpsi.Channel_Stream_Status = nodes.Item(0)["channel_stream_status"].InnerText;
                            break;
                        case "official":
                            gpsi.Default_Community = gpsi.Provider_Type;
                            break;
                    }
                }
                nodes = doc.GetElementsByTagName("user");
                if (nodes.Count > 0)
                {
                    gpsi.User_Id = nodes.Item(0)["user_id"].InnerText;
                }

            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetPlayerStatusAsync), Ex);
                gpsi.Error = Ex.Status.ToString();
                return gpsi;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetPlayerStatusAsync), Ex);
                gpsi.Error = Ex.Message;
                return gpsi;
            }

            gpsi.Error = "";
            return gpsi;
        }

        public async Task<bool> IsLoginNicoAsync()
        {
            try
            {
                var hs = await _wc.DownloadStringTaskAsync(Props.NicoMyUrl).Timeout(_wc.timeout);
                var text = Regex.Match(hs, "login_status ?= ?\\'(login)\\';", RegexOptions.Compiled).Groups[1].Value;
                if (text == "login")
                    return true;
                else
                    return false;
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetNicoPageAsync), Ex);
                return false;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
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
                    bci.Error = "closed";
                    //< code > require_community_member </ code >
                    return bci;
                }
                bci.AuTkn = Regex.Match(ttt, @"""audienceToken"":""([^""]+)""").Groups[1].Value; ;
                bci.BcId = Regex.Match(ttt, @"""broadcastId"":""([^""]+)""").Groups[1].Value; ;
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
                _wc.cookieContainer.Add(result.Cookies);
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
