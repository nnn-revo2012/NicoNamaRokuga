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
using NicoNamaRokuga.Net;

namespace NicoNamaRokuga.Net
{

    public class CommentInfo
    {
        public string WsUrl { set; get; }
        public string UserId { set; get; }
        public string ThreadId { set; get; }
        public string SaveFile { get; set; }
        public string BeginTime { get; set; }
        public string EndTime { get; set; }

        public CommentInfo(string userid)
        {
            this.UserId = userid;
        }
    }


    public class NicoNetComment : IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        public volatile int WsStatus = -1; //WebSocketの状態

        //WebSocket
        private WebSocket _ws = null;
        private bool _wsconnect = false;

        private int _seq_no = 0;
        private string _waybackkey = null;
        private long _when = 0L;                          //when
        private int _last_res = 0;                        //last_res
        private bool _chat_flg = true;                    //チャットデータの最初かどうか？
        private const int _MESSAGE_MAX = 1000;
        private List<List<string>> _come_list = new List<List<string>>();
        private List<string> _come_text = new List<string>();
        private StreamWriter _sw = null;

        System.Threading.Timer _hbTimer;

        private BroadCastInfo _bci = null;
        private CommentInfo _cmi = null;
        private NicoLiveNet _nLiveNet = null;         //WebClient

        private Form1 _form = null;
        private Regex RgxCommand = new Regex(@"^{""([^""]+)"":", RegexOptions.Compiled);

        //放送情報

        public NicoNetComment(Form1 fo, BroadCastInfo bci, CommentInfo cmi, NicoLiveNet nLiveNet)
        {
            IsDebug = false;

            _ws = null;
            _wsconnect = false;

            WsStatus = -1;
            this._nLiveNet = nLiveNet;
            this._bci = bci;
            this._cmi = cmi;
            this._form = fo;

            _seq_no = 0;
            if (_bci.IsTimeShift())
                _come_list.Clear();

        }

        ~NicoNetComment()
        {
            this.Dispose();
        }

        /// サーバーへ接続する
        public void Connect(string wsurl)
        {

            if (_ws == null)
            {
                _form.AddLog("コメントサーバー接続を開始します。", 1);
                _wsconnect = false;
                var _websocket = new WebSocket(wsurl, "", null, Props.WsHeaderComment.ToList(), Props.UserAgent, "", WebSocketVersion.Rfc6455);
                _ws = _websocket;
            }

            /// 文字列受信
            if (_bci.IsTimeShift())
                _ws.MessageReceived += wsReceivedTS;
            else
                _ws.MessageReceived += wsReceived;

            /// サーバー接続完了
            _ws.Opened += (s, e) =>
            {
                _form.AddLog("コメントサーバーに接続しました。", 1);
                _form.EnableButton(false);
                if (_wsconnect == false)
                {
                    _wsconnect = true;
                    WsStatus = 0; //接続中
                }
            };

            /// 接続断の発生
            _ws.Error += (s, e) =>
            {
                StopHBTimer();   //タイマー終了
                WsStatus = 1; //再接続なし
                _wsconnect = false;
                _ws.Dispose();
                _ws = null;
                _sw?.Dispose();
                _sw = null;
                _form.AddLog("コメントサーバーから切断されました。", 1);
            };

            /// サーバー切断完了
            _ws.Closed += (s, e) =>
            {
                StopHBTimer();   //タイマー終了
                if (WsStatus == 0)
                    WsStatus = 1; //再接続なし
                _wsconnect = false;
                _ws.Dispose();
                _ws = null;
                _sw?.Dispose();
                _sw = null;
                _form.AddLog("コメントサーバーから切断しました。", 1);
            };

            /// サーバー接続開始
            _ws.Open();

        }

        /// 文字列受信(生放送)
        private void wsReceived(object sender, MessageReceivedEventArgs e)
        {

            if (string.IsNullOrEmpty(e.Message))
                return;

            if (Form1.props.IsSeetNo)
            {
                if (e.Message.Contains(Props.Commnet_SeetNo))
                    return;
            }

            try
            {
                //_form.AddLog(e.Message + "\r\n");
                var jmes = JObject.Parse(e.Message);

                switch (RgxCommand.Match(e.Message).Groups[1].Value)
                {
                    case "ping":
                        //_form.AddLog(e.Message + "\r\n");
                        if (e.Message.IndexOf("rf:") > 0) _seq_no++;
                        break;
                    case "thread":
                        //_form.AddLog(e.Message + "\r\n");
                        if ((int)jmes["thread"]["resultcode"] == 0)
                        {
                            var enc = new System.Text.UTF8Encoding(false);
                            var sw = new StreamWriter(_cmi.SaveFile, true, enc);
                            _sw = sw;
                            _form.AddLog("コメントファイル出力開始", 1);
                            BeginXmlDoc();
                            if (_seq_no == 0)
                            {
                                StartHBTimer();
                            }
                        }
                        break;
                    case "chat":
                        //System.IO.File.AppendAllText(_cmi.SaveFile, Json2Xml(jmes));
                        _sw.Write(Json2Xml(jmes));
                        break;
                }

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(wsReceived), Ex);
            }
        }

        /// 文字列受信(TS)
        private void wsReceivedTS(object sender, MessageReceivedEventArgs e)
        {

            if (string.IsNullOrEmpty(e.Message))
                return;

            try
            {
                //_form.AddLog(e.Message + "\r\n");
                var jmes = JObject.Parse(e.Message);

                switch (RgxCommand.Match(e.Message).Groups[1].Value)
                {
                    case "ping":
                        //_form.AddLog(e.Message + "\r\n");
                        if (e.Message.IndexOf("rf:") > 0)
                        {
                            _form.AddLog("SEQ "+_seq_no+" num "+_last_res+" ", 1);
                            _seq_no++;
                            _come_list.Add(_come_text);
                            if (_last_res < _MESSAGE_MAX)
                            {
                                this.Close();
                                AppendComment();
                            }
                            else
                            {
                                _chat_flg = true;
                                StartGetTSComment();
                            }
                        }
                        break;
                    case "thread":
                        _form.AddLog(e.Message, 9);
                        if ((int)jmes["thread"]["resultcode"] == 0)
                        {
                            _last_res = (int)jmes["thread"]["last_res"];
                            _form.AddLog(_last_res + " 個のコメントを読み込みます。", 1);
                            var cmlist = new List<string>();
                            _come_text = cmlist;
                            if (_seq_no == 0) StartHBTimer();
                        }
                        break;
                    case "chat":
                        if (_chat_flg == true)
                        {
                            _when = (long)jmes["chat"]["date"] + 5L;
                            _chat_flg = false;
                        }
                        _come_text.Add(e.Message);
                        break;
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(wsReceivedTS), Ex);
            }
        }

        /// サーバーから切断する
        public void Close()
        {
            _ws?.Close();
        }

        //生放送コメント取得
        public void StartGetComment()
        {
            var ttt = SendThread(_cmi.ThreadId, _cmi.UserId, -1);
            _form.AddLog(ttt, 9);
        }

        //TSコメント取得
        public void StartGetTSComment()
        {
            try
            {
                if (_seq_no == 0)
                {
                    _waybackkey = _nLiveNet.GetWayBackKeyAsync(_cmi.ThreadId).Result; //waybackkey取得
                    _when = long.Parse(_cmi.EndTime.Substring(0, _cmi.EndTime.Length - 3));
                    _chat_flg = true;
                }
                var ttt = SendThreadTS(_cmi.ThreadId, _cmi.UserId, -_MESSAGE_MAX, _when.ToString(), _waybackkey);
                _form.AddLog(ttt, 9);

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(StartGetTSComment), Ex);
            }
        }

        //生放送コメント取得
        private string SendThread(string threadId, string user_id, int from)
        {
            var s = @"[{""ping"":{""content"":""rs:%%seqno%%""}},{""ping"":{""content"":""ps:%%seqno2%%""}},"
                  + @"{""thread"":{""thread"":""%%threadId%%"",""version"":""20061206"",""fork"":0,"
                  + @"""user_id"":""%%user_id%%"",""res_from"":%%from%%,""with_global"":1,""scores"":1,""nicoru"":0}},"
                  + @"{""ping"":{""content"":""pf:%%seqno2%%""}},{""ping"":{""content"":""rf:%%seqno%%""}}]";
            var seqno2 = _seq_no * 5;
            s = s.Replace("%%seqno%%", _seq_no.ToString());
            s = s.Replace("%%seqno2%%", seqno2.ToString());
            s = s.Replace("%%threadId%%", threadId);
            s = s.Replace("%%user_id%%", user_id);
            s = s.Replace("%%from%%", from.ToString());
            _ws.Send(s);
            return s;
        }

        //TSコメント取得
        //  TSはwebbackkeyを取得し、whenで時間、res_fromで件数か番号を選んでループして取得
        private string SendThreadTS(string threadId, string user_id, int from, string when, string waybackkey)
        {
            var s = @"[{""ping"":{""content"":""rs:%%seqno%%""}},{""ping"":{""content"":""ps:%%seqno2%%""}},"
                  + @"{""thread"":{""thread"":""%%threadId%%"",""version"":""20061206"",""fork"":0,"
                  + @"""when"":%%when%%,""user_id"":""%%user_id%%"",""res_from"":%%from%%,""with_global"":1,"
                  + @"""scores"":1,""nicoru"":0,""waybackkey"":""%%waybackkey%%""}},"
                  + @"{""ping"":{""content"":""pf:%%seqno2%%""}},{""ping"":{""content"":""rf:%%seqno%%""}}]";
            var seqno2 = _seq_no * 5;
            s = s.Replace("%%seqno%%", _seq_no.ToString());
            s = s.Replace("%%seqno2%%", seqno2.ToString());
            s = s.Replace("%%threadId%%", threadId);
            s = s.Replace("%%user_id%%", user_id);
            s = s.Replace("%%from%%", from.ToString());
            s = s.Replace("%%when%%", when);
            s = s.Replace("%%waybackkey%%", waybackkey);
            _ws.Send(s);
            return s;
        }

        private void AppendComment()
        {
            var enc = new System.Text.UTF8Encoding(false);
            var come_time_prev = string.Empty;
            var come_time = string.Empty;

            //一時ファイル番号大きい方から読み込み
            //ファイル書き出す
            try
            {
                _form.AddLog("コメントファイル出力開始", 1);
                using (var sw = new StreamWriter(_cmi.SaveFile, true, enc))
                {
                    _sw = sw;
                    BeginXmlDoc();
                    for (var i = _seq_no - 1; i >= 0; i--)
                    {
                        var write_flg = (come_time_prev == string.Empty) ? true : false;
                        foreach (var line in _come_list.ToArray()[i])
                        {
                            var jmes = JObject.Parse(line);
                            come_time = jmes["chat"]["date"].ToString() + jmes["chat"]["date_usec"].ToString();
                            if (write_flg)
                            {
                                if (Form1.props.IsSeetNo)
                                {
                                    if (line.Contains(Props.Commnet_SeetNo))
                                        continue;
                                }
                                sw.Write(Json2Xml(jmes));
                            }
                            else
                            {
                                if (come_time == come_time_prev) write_flg = true;
                            }
                        }
                        come_time_prev = come_time;
                        _come_list.ToArray()[i].Clear();
                        _come_list.ToArray()[i].TrimExcess();
                    }
                    EndXmlDoc();
                    _sw = null;
                }
                _form.AddLog("コメントファイル出力終了", 1);

                _come_list.Clear();
                _come_list.TrimExcess();

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(AppendComment), Ex);
            }

        }


        public void BeginXmlDoc() 
        {
            _sw.Write("<?xml version='1.0' encoding='UTF-8'?>\r\n");
            _sw.Write("<packet>\r\n");
        }

        public void EndXmlDoc()
        {
            _sw.Write("</packet>\r\n");
        }

        private string Json2Xml(JObject jmes)
        {
            var result = string.Empty;

            if (string.IsNullOrEmpty(jmes.ToString()))
                return result;

            foreach (var it in jmes)
            {
                result = "<" + it.Key.ToString() + " ";
                foreach (var it2 in (JObject)it.Value)
                {
                    if (it2.Key.ToString() == "content")
                    {
                        result += ">" + it2.Value.ToString();
                        result += "</" + it.Key.ToString() + ">\r\n";
                    }
                    else
                    {
                        result += it2.Key.ToString() + @"=""" + it2.Value.ToString() + @""" ";
                    }
                }
            }

            return result;
        }


        private void StartHBTimer()
        {
            var time = TimeSpan.FromSeconds(40.0);

            _hbTimer = new System.Threading.Timer(_ =>
            {
                _ws?.Send(string.Empty);
                _form.AddLog("Send HeartBeat", 9);
            }
            , null, time, time);
        }

        private void StopHBTimer()
        {
            _hbTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _hbTimer?.Dispose();
            _hbTimer = null;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _ws?.Dispose();
                    _sw?.Dispose();
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

