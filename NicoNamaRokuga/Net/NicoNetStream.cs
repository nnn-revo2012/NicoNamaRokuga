using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Proc;
using NicoNamaRokuga.Net;
using NicoNamaRokuga.Rec;
using NicoNamaRokuga.Message;

namespace NicoNamaRokuga.Net
{
    public class BroadCastInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }

        public string LiveId { set; get; }
        public string AuTkn { set; get; }
        public string WsUrl { set; get; }
        public string FrontEndId { set; get; }
        public string MessageUrl { set; get; }

        public string Title { set; get; }
        public string Description { set; get; }
        public string Provider_Type { set; get; }
        public string Provider_Name { set; get; }
        public string Provider_Id { set; get; }
        public string Community_Title { set; get; }
        public string Community_Id { set; get; }
        public string Community_Thumbnail { set; get; }
        public bool   FollowerOnly { set; get; }
        public long   Open_Time { set; get; }
        public long   Begin_Time { set; get; }
        public long   End_Time { set; get; }
        public long   VposBase_Time { set; get; }
        public long   Server_Time { set; get; }
        public string OnAirStatus { set; get; }
        public string StreamType { set; get; }
        public string User_Id { set; get; }
        public string AccountType { set; get; }
        public long   StartTs_Time { set; get; }
        public long   EndTs_Time { set; get; }
        public string Data_Props { set; get; }

        public BroadCastInfo(string liveid, string autkn, string wsurl, string frontendid)
        {
            this.LiveId = liveid;
            this.AuTkn = autkn;
            this.WsUrl = wsurl;
            this.FrontEndId = frontendid;
            this.MessageUrl = null;
            this.Status = null;
            this.Error = null;
            this.StartTs_Time = 0;
            this.EndTs_Time = 0;
            this.Data_Props = null;
        }

        public bool IsTimeShift()
        {
            return OnAirStatus == "ON_AIR" ? false : true;
        }

        //指定フォーマットに基づいて録画サブディレクトリー名を作る
        public string SetRecFolderFormat(string s)
        {
            return SetRecFileFormat(s);
        }

        //指定フォーマットに基づいて録画ファイル名を作る
        public string SetRecFileFormat(string s)
        {
            var result = s.Replace("?PID?", ReplaceWords(this.LiveId));
            result = result.Replace("?UNAME?", ReplaceWords(this.Provider_Name));
            result = result.Replace("?UID?", ReplaceWords(this.Provider_Id));
            result = result.Replace("?CNAME?", ReplaceWords(this.Community_Title));
            result = result.Replace("?CID?", ReplaceWords(this.Community_Id));
            result = result.Replace("?TITLE?", ReplaceWords(this.Title));

            //時間情報付加
            var date = Props.GetUnixToDateTime(this.Begin_Time);
            result = result.Replace("?YEAR?", date.ToString("yyyy"));
            result = result.Replace("?MONTH?", date.ToString("MM"));
            result = result.Replace("?DAY?", date.ToString("dd"));
            result = result.Replace("?DAY8?", date.ToString("yyyyMMdd"));
            result = result.Replace("?DAY6?", date.ToString("yyMMdd"));
            result = result.Replace("?HOUR?", date.ToString("HH"));
            result = result.Replace("?MINUTE?", date.ToString("mm"));
            result = result.Replace("?SECOND?", date.ToString("ss"));
            result = result.Replace("?TIME6?", date.ToString("HHmmss"));
            result = result.Replace("?TIME4?", date.ToString("HHmm"));

            return result;
        }

        private string ReplaceWords(string s)
        {
            var result = s.Replace("\\", "￥");
            result = result.Replace("/", "?");
            result = result.Replace(":", "：");
            result = result.Replace("*", "＊");
            result = result.Replace("?", "？");
            result = result.Replace("\"", "”");
            result = result.Replace("<", "＜");
            result = result.Replace(">", "＞");
            result = result.Replace("|", "｜");

            result = result.Replace("）", ")");
            result = result.Replace("（", "(");

            result = result.Replace("　", " ");
            result = result.Replace("\u3000", " ");

            return result;
        }

    }


    public class NicoNetStream : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        public volatile int WsStatus = -1; //WebSocketの状態

        //WebSocket
        private WebSocket _ws = null;
        private bool _wsconnect = false;
        private int _wsStatus = -1;

        private BroadCastInfo _bci = null;
        //private CommentInfo _cmi = null;
        private ExecPsInfo _epi = null;

        private NicoMessage _nms = null;        //MessageServer
        private ExecProcess _eProcess = null;   //Process
        private RecHtml _rHtml = null;          //RecHtml
        private RetryInfo _ri = null;
        private CookieContainer _cookieContainer = null;

        System.Threading.Timer _watchTimer;

        private Form1 _form = null;

        //放送情報

        public NicoNetStream(Form1 fo, BroadCastInfo bci, ExecPsInfo epi, NicoMessage nms, ExecProcess eProcess, CookieContainer cookieContainer, RecHtml rHtml, RetryInfo ri)
        {
            IsDebug = false;

            _ws = null;
            _wsconnect = false;
            _wsStatus = -1;

            WsStatus = -1;
            this._nms = nms;
            this._eProcess = eProcess;
            this._rHtml = rHtml;
            this._ri = ri;
            this._bci = bci;
            //this._cmi = cmi;
            this._epi = epi;
            this._cookieContainer = cookieContainer;
            this._form = fo;

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
                var _websocket = new WebSocket(_bci.WsUrl + "&frontend_id=" + _bci.FrontEndId,
                                               "", null, Props.WsHeaderStream.ToList(),
                                               Props.UserAgent, Props.NicoOrigin,
                                               WebSocketVersion.Rfc6455, null, System.Security.Authentication.SslProtocols.Tls12);
                _ws = _websocket;
            }

            /// 文字列受信
            _ws.MessageReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Message))
                    return;

                _form.AddLog(e.Message, 9);

                var jmes = JObject.Parse(e.Message);
                if (jmes["type"] == null)
                {
                    _form.AddLog("type Error: " + e.Message, 9);
                    return;
                }
                if ((string)jmes["type"] == "error")
                {
                    _form.AddLog("type Error: " + e.Message, 9);
                    return;
                }

                JToken data;
                jmes.TryGetValue("data", out data);
                switch ((string)jmes["type"])
                {
                    case "ping":
                        ttt = SendPong();
                        _form.AddLog(ttt, 9);
                        break;
                    case "schedule":
                        break;
                    case "statistics":
                        break;
                    case "serverTime":
                        break;
                    case "stream":
                        if (_wsStatus == 2)
                        {
                            //画質の配列
                            var qts = (JArray)data["availableQualities"];
                            _form.AddLog("availableQualities: [" + string.Join(" ", qts) + "]", 9);
                            _wsStatus = 3;
                            //画質一覧から画質を選ぶ
                            
                            string selqu = "";
                            if (!string.Join(" ", qts).Contains("super_low"))
                                selqu = SelectQuality(Form1.props.QuarityType2.ToString(), qts, Props.Quality2);
                            else
                                selqu = SelectQuality(Form1.props.QuarityType.ToString(), qts, Props.Quality);
                            if (string.IsNullOrEmpty(selqu))
                                selqu = (string)data["quality"];
                            _form.AddLog("Select: " + selqu, 9);
                            _epi.Quality = selqu;
                            ttt = SendChangeStream(selqu, _epi.Protocol);
                            _form.AddLog(ttt, 9);
                        }
                        else
                        {
                            ttt = (string)data["quality"];
                            _form.AddLog("Quarity: " + ttt, 9);
                            _form.DispQuality(ttt);
                            //cookieを追加する
                            string us = string.Empty;
                            if (_cookieContainer != null)
                            {
                                foreach (var ck in (JArray)data["cookies"])
                                {
                                    var cookie = new Cookie();
                                    if (!string.IsNullOrEmpty((string)ck["name"]))
                                    {
                                        cookie.Name = (string)ck["name"];
                                        cookie.Value = (string)ck["value"];
                                        cookie.Expires = DateTime.Parse((string)ck["expires"]);
                                        cookie.Domain = (string)ck["domain"];
                                        cookie.Path = (string)ck["path"];
                                        cookie.Secure = (string)ck["value"] == "true" ? true : false;
                                        _cookieContainer.Add(cookie);
                                    }
                                }
                            }
                            foreach (Cookie ck in _cookieContainer.GetCookies(new Uri(Props.NicoDomain)))
                            {
                                if (ck.Name == "user_session")
                                {
                                    us = ck.Value;
                                    break;
                                }
                            }
                            //ffmpegを実行する
                            ttt = (string)data["uri"];
                            _form.AddLog("Masterm3u8: " + ttt, 9);
                            var argument = ExecPsInfo.SetOption(_epi, ttt, NicoLiveNet.GetNicoPageUrl(_bci.LiveId), us, Form1.props, _bci, false);
                            if (Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native)
                                _rHtml.ExecPs(ttt, _epi.SaveFile);
                            else
                                _eProcess.ExecPs(_epi.Exec, argument);
                        }
                        break;
                    case "room":    //2024/08/05　新メッセージサーバーに変更された
                        break;
                    case "messageServer":   //2024/08/05～
                        if (Form1.props.IsComment)
                        {
                            if (!_bci.IsTimeShift() || !_ri.IsRetry)
                            {
                                ttt = (string)data["viewUri"];
                                _bci.MessageUrl = ttt;
                                _form.AddLog("MessageServer: " + ttt, 9);
                                //_nNetComment.Connect(_cmi.WsUrl);
                                Task.Run(() => _nms.Connect(ttt));
                            }
                        }
                        break;
                    case "seat":
                        //タイマーをスタートする
                        ttt = (string)data["keepIntervalSec"];
                        StartWatchTimer(TimeSpan.FromSeconds(double.Parse(ttt)));
                        _form.AddLog("HeartBeatStart: " + ttt, 9);
                        break;
                    case "disconnect":
                        //切断
                        ttt = (string)data["reason"];
                        _form.AddLog("Disconnect: " + ttt, 9);
                        WsStatus = 3; //再接続
                        if (ttt == "TAKEOVER") //追い出し
                            WsStatus = 4; //再接続あり(長)
                        else if (ttt == "SERVICE_TEMPORARILY_UNAVAILABLE")
                            WsStatus = 4; //再接続あり(長)
                        else if (ttt == "INTERNAL_SERVERERROR")
                            WsStatus = 4; //再接続あり(長)
                        else if (ttt == "TOO_MANY_CONNECTIONS")
                            WsStatus = 4; //再接続あり(長)
                        else if (ttt == "TEMPORARILY_CROWDED")
                            WsStatus = 4; //再接続あり(長)
                        else if (ttt == "END_PROGRAM") //放送終了
                            WsStatus = 1; //再接続なし
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
                    WsStatus = 0; //接続中
                    ttt = SendStartWatching(_epi.Protocol);
                    _form.AddLog(ttt, 9);
                    _wsStatus = 2; //StartWatching送信
                }
            };

            /// 接続断の発生
            _ws.Error += (s, e) =>
            {
                StopWatchTimer();   //タイマー終了
                WsStatus = 1; //再接続なし
                _wsconnect = false;
                _ws.Dispose();
                _ws = null;
                _form.AddLog("サーバーから切断されました。", 1);
            };

            /// サーバー切断完了
            _ws.Closed += (s, e) =>
            {
                StopWatchTimer();   //タイマー終了
                if (WsStatus == 0)
                    WsStatus = 1; //再接続なし
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
/*
        public void ReSendGetStream(string quality, string protocol)
        {
            var ttt = SendGetStream(quality, protocol);
            //_form.AddLog(ttt + "\r\n");

        }
*/
        private string SelectQuality(string quality, JArray qtypes, IEnumerable<string> qlist)
        {
            string result = null;
            if (string.IsNullOrEmpty(quality) || qtypes.Count() <= 0) return result;

            for (var i = qtypes.Count() - 1; i >= 0; i--)
            {
                if (Props.IsQTypes(qtypes[i].ToString(), qlist))
                {
                    result = qtypes[i].ToString();
                    if (result == quality) break;
                }
            }
            return result;
        }

        public string SendStartWatching(string protocol)
        {
            //StartWatching
            var s = @"{""type"":""startWatching"",""data"":{ ""stream"":{ ""quality"":""abr"","
                    + @"""protocol"":""%%proto%%"",""latency"":""high"",""accessRightMethod"":""single_cookie"","
                    + @"""chasePlay"":false},"
                    + @"""room"":{ ""protocol"":""webSocket"",""commentable"":true},""reconnect"":false} }";
            s = s.Replace("%%proto%%", protocol);
            _ws.Send(s);
            return s;
        }

        public string SendChangeStream(string quality, string protocol)
        {
            //changeStream
            var s = @"{""type"":""changeStream"",""data"":{ ""quality"":""%%quality%%"","
                    + @"""protocol"":""%%proto%%"",""latency"":""high"",""accessRightMethod"":""single_cookie"","
                    + @"""chasePlay"":false} }";
            s = s.Replace("%%quality%%", quality);
            s = s.Replace("%%proto%%", protocol);
            _ws.Send(s);
            return s;
        }

        private string SendKeepSeat()
        {
            //interval
            var s = @"{""type"":""keepSeat""}";
            _ws?.Send(s);
            return s;
        }
 
        private string SendPong()
        {
            var s = @"{""type"":""pong""}";
            _ws?.Send(s);
            return s;
        }

        private void StartWatchTimer(TimeSpan time)
        {
            _watchTimer = new System.Threading.Timer(_ =>
            {
                var s = SendKeepSeat();
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
                    StopWatchTimer();
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

