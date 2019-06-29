using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net.Http;
using System.Net;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Proc;

namespace NicoNamaRokuga.Net
{
    public class BroadCastInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }

        public string LiveId { set; get; }
        public string BcId { set; get; }
        public string AuTkn { set; get; }
        public string WsUrl { set; get; }
        public string Title { set; get; }
        public string Description { set; get; }
        public string Provider_Type { set; get; }
        public string Provider_Name { set; get; }
        public string Provider_Id { set; get; }
        public string Community_Title { set; get; }
        public string Community_Id { set; get; }
        public string Community_Thumbnail { set; get; }
        public bool   FollowerOnly { set; get; }
        public string Open_Time { set; get; }
        public string Begin_Time { set; get; }
        public string VposBase_Time { set; get; }
        public string End_Time { set; get; }
        public string OnAirStatus { set; get; }
        public string User_Id { set; get; }

        public BroadCastInfo(string liveid, string bcid, string autkn, string wsurl)
        {
            this.LiveId = liveid;
            this.BcId = bcid;
            this.AuTkn = autkn;
            this.WsUrl = wsurl;
            this.Status = null;
            this.Error = null;
        }

        public bool IsTimeShift()
        {
            return (WsUrl.IndexOf(Props.TIMESHIFT) > 0) ? true : false;
        }

        private static readonly Regex RgxChNo = new Regex("/([^/]+)$", RegexOptions.Compiled);
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


    public class NicoNetStream : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        //WebSocket
        private WebSocket _ws = null;
        private bool _wsconnect = false;
        private int _wsStatus = -1;

        private BroadCastInfo _bci = null;
        private CommentInfo _cmi = null;
        private ExecPsInfo _epi = null;

        System.Threading.Timer _watchTimer;

        private Form1 _form = null;

        //放送情報

        public NicoNetStream(Form1 fo, BroadCastInfo bci, CommentInfo cmi, ExecPsInfo epi)
        {
            IsDebug = false;

            _ws = null;
            _wsconnect = false;
            _wsStatus = -1;

            this._form = fo;
            this._bci = bci;
            this._cmi = cmi;
            this._epi = epi;
        }

        ~NicoNetStream()
        {
            this.Dispose();
        }

        /// サーバーへ接続する
        public void Connect()
        {

            string ttt;

            if (_ws == null)
            {
                _form.AddLog("サーバー接続を開始します。", 1);
                _wsconnect = false;
                //var clist = _form._nLiveNet.GetCookieList();
                var _websocket = new WebSocket(_bci.WsUrl, "", null, Props.WsHeaderStream.ToList(), Props.UserAgent, "", WebSocketVersion.Rfc6455, null, System.Security.Authentication.SslProtocols.Tls12, 0);
                _ws = _websocket;
            }

            /// 文字列受信
            _ws.MessageReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Message))
                    return;

                _form.AddLog(e.Message, 9);

                var jmes = JObject.Parse(e.Message);
                JToken jtkn;
                if (!jmes.TryGetValue("type", out jtkn))
                {
                    _form.AddLog("type Error: " + e.Message, 9);
                    return;
                }
                if (jtkn.ToString() == "ping")
                {
                    ttt = SendPong();
                    //_form.AddLog(ttt + "\r\n");
                    return;
                }
                if (jtkn.ToString() == "error") return;


                    if (!jmes.TryGetValue("body", out jtkn))
                {
                    _form.AddLog("body Error: " + e.Message, 9);
                    return;
                }

                switch (jtkn["command"].ToString())
                {
                    case "permit":
                        if (_wsStatus == 1)
                            _wsStatus = 2; //permit
                        break;
                    case "schedule":
                        break;
                    case "currentstream":
                        if (_wsStatus == 2)
                        {
                            //画質の配列
                            var qts = (JArray)jtkn["currentStream"]["qualityTypes"];
                            _form.AddLog("QuarityTypes: [" + string.Join(" ", qts) + "]", 9);
                            _wsStatus = 3;
                            //画質一覧から画質を選ぶ
                            var selqu = SelectQuality(Form1.props.QuarityType.ToString(), qts);
                            if (string.IsNullOrEmpty(selqu))
                                selqu = jtkn["currentStream"]["quality"].ToString();
                            _form.AddLog("Select: " + selqu, 9);
                            _epi.Quality = selqu;
                            ttt = SendGetStream(selqu, _epi.Protocol);
                            _form.AddLog(ttt, 9);
                        }
                        else
                        {
                            ttt = jtkn["currentStream"]["quality"].ToString();
                            _form.AddLog("Quarity: " + ttt, 9);
                            _form.DispQuality(ttt);
                            //ffmpegを実行する
                            ttt = (_epi.Protocol == "rtmp") ?
                                jtkn["currentStream"]["uri"].ToString() + "/" + jtkn["currentStream"]["name"].ToString() :
                                jtkn["currentStream"]["uri"].ToString();
                            _form.AddLog("Playerm3m8: " + ttt, 9);
                            _epi.SaveFile = ExecPsInfo.GetSaveFileNum(_epi);
                            if (Form1.props.IsComment)
                                _cmi.SaveFile = _epi.SaveFile + _epi.Xml;
                            var argument = ExecPsInfo.SetOption(_epi, ttt);
                            _form._eProcess.ExecPs(_epi.Exec, argument);
                        }
                        break;
                    case "currentroom":
                        //コメントサーバー接続設定
                        if (Form1.props.IsComment)
                        {
                            ttt = jtkn["room"]["messageServerUri"].ToString();
                            _cmi.WsUrl = ttt;
                            _form.AddLog("CommentServer: " + ttt, 9);
                            ttt = jtkn["room"]["threadId"].ToString();
                            _cmi.ThreadId = ttt;
                            _form._nNetComment.Connect(_cmi.WsUrl);
                            if (Form1.IsTimeShift)
                            {
                                while (Form1.WsStatus[1] != 0) ;
                                _form._nNetComment.StartGetTSComment();
                            }
                        }
                        break;
                    case "watchinginterval":
                        //タイマーをスタートする
                        ttt = jtkn["params"][0].ToString();
                        StartWatchTimer(TimeSpan.FromSeconds(double.Parse(ttt)), _bci.BcId);
                        _form.AddLog("HeartBeatStart: " + ttt, 9);
                        break;
                    case "disconnect":
                        //切断
                        var par = (JArray)jtkn["params"];
                        ttt = par[1].ToString();
                        _form.AddLog("Disconnect: " + ttt, 9);
                        if (ttt == "TAKEOVER") //追い出し
                            Form1.WsStatus[0] = 1; //再接続あり
                        else if (ttt == "END_PROGRAM") //放送終了
                            Form1.WsStatus[0] = 2; //再接続なし
                        break;
                    default:
                        break;
                }
            };

            /// サーバー接続完了
            _ws.Opened += (s, e) =>
            {
                _form.AddLog("サーバーに接続しました。", 1);
                _form.EnableButton(false);
                if (_wsconnect == false)
                {
                    _wsconnect = true;
                    Form1.WsStatus[0] = 0; //接続中
                    ttt = SendVersion("leo");
                    _form.AddLog(ttt, 9);
                    ttt = SendGetPermit(_bci.BcId, _epi.Protocol);
                    _form.AddLog(ttt, 9);
                    _wsStatus = 1; //getpermit送信
                }
            };

            /// 接続断の発生
            _ws.Error += (s, e) =>
            {
                Form1.WsStatus[0] = 2; //再接続なし
                _wsconnect = false;
                _ws.Dispose();
                _ws = null;
                _form.AddLog("サーバーから切断されました。", 1);
            };

            /// サーバー切断完了
            _ws.Closed += (s, e) =>
            {
                StopWatchTimer();   //タイマー終了
                if (Form1.WsStatus[0] == 0)
                    Form1.WsStatus[0] = 2; //再接続なし
                _wsconnect = false;
                _ws.Dispose();
                _ws = null;
                _form.AddLog("サーバーから切断しました。", 1);
            };

            /// サーバー接続開始
            _ws.Open();

        }

        /// サーバーから切断する
        public void Close()
        {
            _ws?.Close();
        }

        /// 接続する
        public void ReSendGetStream(string quality, string protocol)
        {
            var ttt = SendGetStream(quality, protocol);
            //_form.AddLog(ttt + "\r\n");

        }

        private string SelectQuality(string quality, JArray qtypes)
        {
            string result = null;
            if (string.IsNullOrEmpty(quality) || qtypes.Count() <= 0) return result;

            for (var i = qtypes.Count() - 1; i >= 0; i--)
            {
                if (Props.IsQTypes(qtypes[i].ToString()))
                {
                    result = qtypes[i].ToString();
                    if (result == quality) break;
                }
            }
            return result;
        }

        public string GetPermit(string bcId, string protocol)
        {
            //getpermit
            var s = @"{""type"":""watch"",""body"":{""command"":""getpermit"",""requirement"":{""broadcastId"":""%%bcId%%"","
                  + @"""route"":"""",""stream"":{""protocol"":""%%proto%%"",""requireNewStream"":true,"
                  + @"""priorStreamQuality"":""low"",""isLowLatency"":false},""room"":{""isCommentable"":true,""protocol"":""webSocket""}}}}";
            s = s.Replace("%%bcId%%", bcId);
            s = s.Replace("%%proto%%", protocol);
            return s;
        }

        private string SendGetPermit(string bcId, string protocol)
        {
            var s = GetPermit(bcId, protocol);
            _ws.Send(s);
            return s;
        }

        public string SendGetStream(string quality, string protocol)
        {
            //getstream
            var s = @"{""type"":""watch"",""body"":{""command"":""getstream"",""requirement"":"
                  + @"{""protocol"":""%%proto%%"",""quality"":""%%quality%%""}}}";
            s = s.Replace("%%quality%%", quality);
            s = s.Replace("%%proto%%", protocol);
            _ws.Send(s);
            return s;
        }

        private string SendInterval(string bcId)
        {
            //interval
            var s = @"{""type"":""watch"",""body"":{""command"":""watching"",""params"":[""%%bcId%%"",""-1"",""0""]}}";
            s = s.Replace("%%bcId%%", bcId);
            _ws?.Send(s);
            return s;
        }

        public string SendVersion(string version)
        {
            var s = @"{""type"":""watch"",""body"":{""command"":""playerversion"",""params"":[""%%version%%""]}}";
            s = s.Replace("%%version%%", version);
            _ws.Send(s);
            return s;
        }

        private string SendPong()
        {
            var s = @"{""type"":""pong"",""body"":{}}";
            _ws?.Send(s);
            return s;
        }

        private void StartWatchTimer(TimeSpan time, string bcId)
        {
            _watchTimer = new System.Threading.Timer(_ =>
            {
                var s = SendInterval(bcId);
                _form.AddLog(s, 9);
            }
            , null, time, time);
        }

        private void StopWatchTimer()
        {
            _watchTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _watchTimer?.Dispose();
            _watchTimer = null;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _ws?.Dispose();
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

