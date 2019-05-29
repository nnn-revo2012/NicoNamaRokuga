using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Xml;
using System.IO;
using System.Web;
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
        public string Start_Time { set; get; }
        public string End_Time { set; get; }

        public GetPlayerStatusInfo()
        {
            this.Status = null;
            this.Error = null;
        }
    }

    public class TemplateInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }

        public string LiveId { set; get; }
        public string Title { set; get; }
        public string Description { set; get; }
        public string Provider_Type { set; get; }
        public string Provider_Name { set; get; }
        public string Provider_Id { set; get; }
        public string Community_Title { set; get; }
        public string Community_Id { set; get; }
        public string Community_Thumbnail { set; get; }
        public string Start_Time { set; get; }
        public string End_Time { set; get; }

        private static Regex RgxChNo = new Regex("/([^/]+)$", RegexOptions.Compiled);

        public TemplateInfo(string liveid, string provider_type)
        {
            this.LiveId = liveid;
            this.Provider_Type = provider_type;
            this.Status = null;
            this.Error = null;
        }
        //Urlの最後のスラッシュ以降の文字列を取得
        public static string GetChNo(string url)
        {
            return RgxChNo.Match(url).Groups[1].Value;
        }
        //指定フォーマットに基づいて録画ファイル名を作る
        public string SetRecFile(string recfile)
        {
            var result = string.Empty;

            result = string.Format(recfile,
                this.LiveId, this.Title, this.Provider_Name, this.Community_Id, this.Community_Title);

            //時間情報付加
            var date = DateTime.Now;
            result = result.Replace("{Y}", date.ToString("yyyy"));
            result = result.Replace("{y}", date.ToString("yy"));
            result = result.Replace("{M}", date.ToString("MM"));
            result = result.Replace("{D}", date.ToString("dd"));
            result = result.Replace("{W}", date.ToString("ddd"));
            result = result.Replace("{h}", date.ToString("HH"));
            result = result.Replace("{m}", date.ToString("mm"));
            result = result.Replace("{s}", date.ToString("ss"));

            result = result.Replace("\\", "￥");
            result = result.Replace("/", "／");
            result = result.Replace(":", "：");
            result = result.Replace("*", "＊");
            result = result.Replace("??", "？");
            result = result.Replace("?", "？");
            result = result.Replace("\"", "”");
            result = result.Replace("<", "＜");
            result = result.Replace(">", "＞");
            result = result.Replace("|", "｜");
            result = result.Replace("+", "＋");
            result = result.Replace(" ", "");
            result = result.Replace("　", "");
            result = result.Replace("\u3000", "");

            return result;
        }

    }


    public class NicoLiveNet : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private WebClientEx _wc = null;

        private class WebClientEx : WebClient
        {
            public CookieContainer cookieContainer = new CookieContainer();

            protected override WebRequest GetWebRequest(Uri address)
            {
                var wr = base.GetWebRequest(address);

                HttpWebRequest hwr = wr as HttpWebRequest;
                if (hwr != null)
                {
                    hwr.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; //圧縮を有効化
                    hwr.CookieContainer = cookieContainer; //Cookie
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
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
        }

        ~NicoLiveNet()
        {
            this.Dispose();
        }


        public List<KeyValuePair<string, string>> GetCookieList()
        {
            var result = new Dictionary<string, string>();
            var cc = _wc.cookieContainer;

            foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                result.Add(ck.Name.ToString(), ck.Value.ToString());

            return result.ToList();
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

        //放送IDからプレイヤーAPIをゲット
        public static string GetAPIUrl(string liveID)
        {
            if (string.IsNullOrEmpty(liveID)) return null;
            //return Props.NicoAPIUrl + liveID + "/player";
            return "";
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

                byte[] resArray = await _wc.UploadValuesTaskAsync(Props.NicoLoginUrl, ps);
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
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                MessageBox.Show("LoginNico() Error: \r\n" + Ex.Message);
                return false;	
            }

            return flag;

        }

        /*
        //ニコニコをログアウト
        public async Task<bool> LogoutNico()
        {

            var flag = false;
            try {
                using (var response = await _wc.("http://live.nicovideo.jp/logout"))
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        flag = true;
                        IsLoginStatus = false;
                    }else
                    {
                        //何らかのエラー
                    }
                    if (IsDebug)
                    {
                        //responseヘッダーの数と内容を表示
                        var strtmp = string.Format("Logout Headers: {0}\r\n\r\n", response.Headers.Count());
                        foreach (var item in response.Headers)
                        strtmp += string.Format("{0}: {1}\r\n", item.Key, item.Value.First());
                        MessageBox.Show(strtmp);
                    }
                 }
            }catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                MessageBox.Show("LogoutNico() Error: \r\n" + Ex.Message);
                return false;
            }

            return flag;
        }
*/

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
                var xhtml =  await _wc.DownloadStringTaskAsync(stmp);
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
                    gpsi.Start_Time = nodes.Item(0)["start_time"].InnerText;
                    gpsi.End_Time = nodes.Item(0)["end_time"].InnerText;
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

            } catch (Exception Ex) //タイムアウトなど
            {
                MessageBox.Show("GetPlayerStatusAsync() Error: \r\n" + Ex.Message);
                return gpsi;
            }

            gpsi.Error = "";
            return gpsi;
        }

        //新配信API(player)を実行
        public async Task<BroadCastInfo> GetPlayerAPIAsync(string nicoUrl)
        {
            var bci = new BroadCastInfo(null, null, null, null);
            bci.Status = "fail";
            bci.Error = "PARAMERROR";

            try
            {
                var stmp = GetLiveID(nicoUrl);
                if (string.IsNullOrEmpty(stmp)) return bci;

                bci.LiveId = stmp;
                //stmp = Props.NicoAPIUrl + stmp + "/player";
                var hs = await _wc.DownloadStringTaskAsync(stmp);
                if (string.IsNullOrEmpty(hs)) return bci;

                var des = JObject.Parse(hs);
                JToken jtkn;
                if (des.TryGetValue("errorCode", out jtkn))
                {
                    bci.Error = jtkn.ToString();
                    return bci;
                }

                bci.WsUrl = des["webSocketUrl"].ToString()
                          + des["broadcastId"].ToString()
                          + "?audience_token=" + des["audienceToken"].ToString();

                bci.AuTkn = des["audienceToken"].ToString();
                bci.BcId = des["broadcastId"].ToString();
                bci.Status = "ok";
                bci.Error = "";

            }catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                MessageBox.Show("GetPlayerAPIAsync() Error: \r\n" + Ex.Message);
                return bci;
            }

            return bci;
        }

        //生放送ページから放送情報を取得
        public async Task<TemplateInfo> GetTemplateAPIAsync(string nicoUrl)
        {
            var tpi = new TemplateInfo(null, null);
            tpi.Status = "fail";
            tpi.Error = "PARAMERROR";

            try
            {
                var liveid = GetLiveID(nicoUrl);
                if (string.IsNullOrEmpty(liveid)) return tpi;

                //var hs = await client.GetStringAsync(Props.NicoCasApi + liveid);
                //var providertype = Regex.Match(hs, "\"programType\":\"([^\"]*)\"", RegexOptions.Compiled).Groups[1].Value;
                var providertype = "unama";
                tpi.Provider_Type = providertype;

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoLiveUrl + liveid);
                if (string.IsNullOrEmpty(hs)) return tpi;
                if (hs.IndexOf("window.NicoGoogleTagManagerDataLayer = [];") > 0)
                {
                    tpi.Error = "REQUIERED";
                    return tpi;
                }
                if (providertype != "cas")
                {
                    providertype = Regex.Match(hs, "\"content_type\":\"([^\"]*)\"", RegexOptions.Compiled).Groups[1].Value;
                    tpi.Provider_Type = providertype;
                }
                var ttt = WebUtility.HtmlDecode(Regex.Match(hs, "<script +id=\"embedded-data\" +data-props=\"([^\"]*)\"></script>", RegexOptions.Compiled).Groups[1].Value);
                //Clipboard.SetText(ttt);
                var dprops = JObject.Parse(ttt);
                //Clipboard.SetText(dprops.ToString());
                var dprogram = (JObject)dprops["program"];
                tpi.LiveId = dprogram["nicoliveProgramId"].ToString();
                tpi.Title = dprogram["title"].ToString();
                tpi.Description = dprogram["description"].ToString();
                tpi.Provider_Id = providertype;
                tpi.Provider_Name = "公式生放送";
                if (providertype == "cas")
                {
                    tpi.Community_Thumbnail = dprogram["thumbnail"]["imageUrl"].ToString();
                    tpi.Provider_Name = dprops["broadcaster"]["nickname"].ToString();
                    tpi.Provider_Id = dprops["broadcaster"]["id"].ToString();
                }
                else
                {
                    tpi.Community_Thumbnail = dprogram["thumbnail"]["small"].ToString();
                    JToken aaa;
                    if (dprogram.TryGetValue("supplier", out aaa))
                    {
                        tpi.Provider_Name = dprogram["supplier"]["name"].ToString();
                        if (providertype == "user")
                            tpi.Provider_Id = TemplateInfo.GetChNo(dprogram["supplier"]["pageUrl"].ToString());
                    }
                }
                //tpi.Community_Only = dprogram["isFollowerOnly"].ToString();
                tpi.Community_Id = providertype;
                tpi.Community_Title = "公式生放送";
                if (dprops["socialGroup"].Count() > 0)
                {
                    tpi.Community_Id = dprops["socialGroup"]["id"].ToString();
                    tpi.Community_Title = dprops["socialGroup"]["name"].ToString();
                    //tpi.Community_Thumbnail = dprops["socialGroup"]["thumbnailSmallImageUrl"].ToString();
                }
                tpi.Status = "ok";
            }
            catch (WebException Ex)
            {
                //
                tpi.Error = Ex.Status.ToString();
                return tpi;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                tpi.Error = Ex.Message;
                return tpi;
            }

            return tpi;
        }

        //生放送ページから放送情報を取得
        public async Task<BroadCastInfo> GetNicoPageAsync(string nicoUrl)
        {
            var bci = new BroadCastInfo(null, null, null, null);
            bci.Status = "fail";
            bci.Error = "PARAMERROR";

            try
            {
                var stmp = GetLiveID(nicoUrl);
                if (string.IsNullOrEmpty(stmp)) return null;

                bci.LiveId = stmp;
                stmp = Props.NicoLiveUrl + stmp;
                var hs = HttpUtility.HtmlDecode(await _wc.DownloadStringTaskAsync(stmp));
                if (string.IsNullOrEmpty(hs)) return bci;

                bci.WsUrl = Regex.Match(hs, @"""webSocketUrl"":""([^""]+)""").Groups[1].Value;
                bci.AuTkn = Regex.Match(hs, @"""audienceToken"":""([^""]+)""").Groups[1].Value; ;
                bci.BcId = Regex.Match(hs, @"""broadcastId"":""([^""]+)""").Groups[1].Value; ;
                bci.Status = "ok";
                bci.Error = "";

            }
            catch (WebException Ex)
            {
                //
                bci.Error = Ex.Status.ToString();
                return bci;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
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
                result = await _wc.DownloadStringTaskAsync(stmp);
                result = result.Split('=')[1];
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                return result;
            }
            return result;
        }

        //master.m3u8からplayer.m3u8のURLを取得
        public async Task<string> GetPlayerM3u8Async(string url, string referer)
        {
            var result = string.Empty;
            if (string.IsNullOrEmpty(url)) return result;

            try
            {
                var idx = url.IndexOf("master.m3u8");
                if (idx >= 0) result = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        if (!line.StartsWith("#")) result += line;
                    }
                }
            } catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                return result;
            }
            return result;
        }

        //*************** Cookie用 *******************

        //使えるブラウザー一覧を取得
        public static async Task<IList<string>> GetCookieBrowsers(bool flag)
        {
            var result = new List<string>();
            try
            {
                var importableBrowsers = await CookieGetters.Default.GetInstancesAsync(flag);

                //conbobox1 にブラウザ名を登録
                foreach (var ib in importableBrowsers)
                    result.Add(ib.SourceInfo.BrowserName);
            }catch (Exception Ex)
            {
                MessageBox.Show("GetCookieBrowsers Error: \r\n" + Ex.Message);
                return result;
            }
            return result;
        }

        //指定番号のCookieSourceInfoを取得
        public static async Task<CookieSourceInfo> GetCookieSource(bool flag, int index)
        {
            var importableBrowsers = await CookieGetters.Default.GetInstancesAsync(flag);

            if (index < 0 || index > importableBrowsers.Count() - 1) return null;
            return importableBrowsers[index].SourceInfo;
        }

        //指定番号のCookie情報を取得
        public static async Task<ICookieImporter> GetCookieGetter(bool flag, int index)
        {
            var importableBrowsers = await CookieGetters.Default.GetInstancesAsync(flag);

            if (index < 0 || index > importableBrowsers.Count()-1) return null;
            return importableBrowsers[index];
        }

        // 指定Cookie情報のブラウザーのニコニコのCookieを取得してセット
        public async Task<bool> SetNicoCookie(bool flag, CookieSourceInfo csi)
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
                _wc.cookieContainer.Add(targetUrl, result.Cookies);
                IsLoginStatus = true;

                if (IsDebug)
                {
                    var cc = _wc.cookieContainer;
                    Debug.WriteLine(string.Format("Cookie GetCookieHeader: \r\n{0}\r\n",
                        cc.GetCookieHeader(targetUrl)));
                }
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
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
