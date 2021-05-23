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
using System.Diagnostics;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;
using Org.BouncyCastle.Crypto.Digests;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Rec;

namespace NicoNamaRokuga.Net
{
    public class NicoNetComment : ANetComment, IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        public NicoNetComment(Form1 fo, BroadCastInfo bci, CommentInfo cmi, NicoLiveNet nLiveNet, NicoDb ndb, CommentControl cctl)
        {
            IsDebug = false;

            _ws = null;
            _wsconnect = false;

            WsStatus = -1;
            this._nLiveNet = nLiveNet;
            this._bci = bci;
            this._cmi = cmi;
            this._ndb = ndb;
            this._cctl = cctl;
            this._form = fo;

            _seq_no = 0;
            if (_bci != null && _bci.LiveId != null)
                if (_bci.IsTimeShift() && _cctl.status == 0)
                {
                    _cctl._come_list.Clear();
                    if (_bci.StartTs_Time > 0)
                        _cmi.Offset = (long)_bci.StartTs_Time * 6000L;
                }
        }

        ~NicoNetComment()
        {
            this.Dispose();
        }

        /// サーバーに接続する
        public override void Connect(string wsurl)
        {

            if (_ws == null)
            {
                _form.AddLog("コメントサーバー接続を開始します。", 1);
                _wsconnect = false;
                var _websocket = new WebSocket(wsurl, Props.WsSubProtocol, null,
                                               Props.WsHeaderComment.ToList(), 
                                               Props.UserAgent, Props.NicoOrigin,
                                               WebSocketVersion.Rfc6455, null);
                _ws = _websocket;
            }

            /// 文字列受信
            if (_bci.IsTimeShift())
            {
                _ws.MessageReceived += wsReceivedTS;
            }
            else
            {
                if (Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native)
                    _ws.MessageReceived += wsReceivedDB;
                else
                    _ws.MessageReceived += wsReceived;
            }

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
                StopHBTimer();          //タイマー終了
                if (_bci.IsTimeShift() && _cctl.status == 1) //TSコメント取得中
                    WsStatus = 2; //再接続あり
                else
                    WsStatus = 1; //再接続なし
                //映像なしで生放送の場合、コメント終了処理
                if (!Form1.props.IsVideo && !_bci.IsTimeShift())
                {
                    _form.AddLog("コメントファイル出力終了", 1);
                    if (!(Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native))
                        EndXmlDoc();
                }
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
                if (_bci.IsTimeShift() && _cctl.status == 1) //TSコメント取得中
                    WsStatus = 2; //再接続あり
                else if (WsStatus == 0)
                    WsStatus = 1; //再接続なし
                //映像なしで生放送の場合、コメント終了処理
                if (!Form1.props.IsVideo && !_bci.IsTimeShift())
                {
                    _form.AddLog("コメントファイル出力終了", 1);
                    if (!(Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native))
                        EndXmlDoc();
                }
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
        protected override void wsReceived(object sender, MessageReceivedEventArgs e)
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
                            _cmi.Offset = ((long )jmes["thread"]["server_time"] - _cmi.OpenTime) * 100L;
                            var enc = new System.Text.UTF8Encoding(false);
                            var sw = new StreamWriter(_cmi.SaveFile, true, enc);
                            _sw = sw;
                            _form.AddLog("コメントファイル出力開始", 1);
                            BeginXmlDoc();
                            _sw.Write(Json2Xml(jmes));
                            if (_seq_no == 0)
                            {
                                StartHBTimer();
                            }
                        }
                        break;
                    case "chat":
                        //System.IO.File.AppendAllText(_cmi.SaveFile, Json2Xml(jmes));
                        if (jmes["chat"]["vpos"] == null) jmes["chat"]["vpos"] = 0;
                        jmes["chat"]["vpos"] = CalcVpos(_cmi.OpenTime, _cmi.Offset, (string)jmes["chat"]["date"], (string)jmes["chat"]["vpos"], _bci.Provider_Type);
                        _sw.Write(Json2Xml(jmes));
                        break;
                }

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(wsReceived), Ex);
            }
        }

        /// 文字列受信(生放送)(DB)
        protected override void wsReceivedDB(object sender, MessageReceivedEventArgs e)
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
                            _form.AddLog("コメントファイル出力開始", 1);
                            Json2Db(jmes);
                            if (_seq_no == 0)
                            {
                                StartHBTimer();
                            }
                        }
                        break;
                    case "chat":
                        if (jmes["chat"]["vpos"] == null) jmes["chat"]["vpos"] = 0;
                        if (!Json2Db(jmes))
                        {
                            _form.AddLog("コメント出力失敗", 9);
                            _form.AddLog(e.Message, 9);
                        }
                        break;
                }

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(wsReceived), Ex);
            }
        }

        /// 文字列受信(TS)
        protected override void wsReceivedTS(object sender, MessageReceivedEventArgs e)
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
                            _form.AddLog("SEQ "+ _seq_no +" num "+ _cctl._last_res +" ", 1);
                            _seq_no++;
                            //if (_seq_no >= 4) this.Close(); //DEBUG
                            _cctl._come_list.Add(_cctl._come_text);
                            if (_cctl._last_res < _MESSAGE_MAX)
                            {
                                _cctl.status = 3; //TSコメント取得終了
                                this.Close();
                                if (Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native)
                                    AppendCommentDB();
                                else
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
                            if (jmes["thread"]["last_res"] != null)
                            {
                                _cctl._last_res = (long)jmes["thread"]["last_res"];
                                _form.AddLog(_cctl._last_res + " 個のコメントを読み込みます。", 1);
                                var cmlist = new List<string>();
                                _cctl._come_text = cmlist;
                                if (_seq_no == 0)
                                {
                                    StartHBTimer();
                                    if (Form1.props.Protocol == Protocol.hls && Form1.props.UseExternal == UseExternal.native)
                                        Json2Db(jmes);
                                }
                            }
                        }
                        break;
                    case "chat":
                        if (_chat_flg == true)
                        {
                            _cctl._when = (long)jmes["chat"]["date"] + 1L;
                            _chat_flg = false;
                        }
                        _cctl._come_text.Add(e.Message);
                        break;
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(wsReceivedTS), Ex);
            }
        }

        //TSコメント出力
        protected override void AppendComment()
        {
            var enc = new System.Text.UTF8Encoding(false);
            long come_time_prev = 0L;
            long come_time = 0L;

            //一時ファイル番号大きい方から読み込み
            //ファイル書き出す
            try
            {
                _form.AddLog("コメントファイル出力開始", 1);
                using (var sw = new StreamWriter(_cmi.SaveFile, true, enc))
                {
                    _sw = sw;
                    BeginXmlDoc();
                    for (var i = _cctl._come_list.Count() - 1; i >= 0; i--)
                    {
                        var write_flg = (come_time_prev <= 0L) ? true : false;
                        foreach (var line in _cctl._come_list.ToArray()[i])
                        {
                            var jmes = JObject.Parse(line);
                            if (jmes["chat"]["vpos"] == null) jmes["chat"]["vpos"] = 0;
                            come_time = (long)jmes["chat"]["date"];
                            if (write_flg == false)
                                if (come_time > come_time_prev) write_flg = true;
                            if (write_flg == true)
                            {
                                if (Form1.props.IsSeetNo)
                                {
                                    if (line.Contains(Props.Commnet_SeetNo))
                                        continue;
                                }
                                jmes["chat"]["vpos"] = CalcVpos(_cmi.OpenTime, _cmi.Offset, (string)jmes["chat"]["date"], (string)jmes["chat"]["vpos"], _bci.Provider_Type);
                                sw.Write(Json2Xml(jmes));
                            }
                        }
                        come_time_prev = come_time;
                        _cctl._come_list.ToArray()[i].Clear();
                        _cctl._come_list.ToArray()[i].TrimExcess();
                    }
                    EndXmlDoc();
                    _sw = null;
                }
                _form.AddLog("コメントファイル出力終了", 1);

                _cctl._come_list.Clear();
                _cctl._come_list.TrimExcess();

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(AppendComment), Ex);
                _form.AddLog("コメントファイル出力失敗", 1);
            }

        }

        //TSコメント出力(DB)
        protected override void AppendCommentDB()
        {
            long come_time_prev = 0L;
            long come_time = 0L;

            //一時ファイル番号大きい方から読み込み
            //ファイル書き出す
            try
            {
                _form.AddLog("コメントファイル出力開始", 1);
                for (var i = _cctl._come_list.Count() - 1; i >= 0; i--)
                {
                    var write_flg = (come_time_prev <= 0L) ? true : false;
                    foreach (var line in _cctl._come_list.ToArray()[i])
                    {
                        var jmes = JObject.Parse(line);
                        if (jmes["chat"]["vpos"] == null) jmes["chat"]["vpos"] = 0;
                        come_time = (long)jmes["chat"]["date"];
                        if (write_flg == false)
                            if (come_time > come_time_prev) write_flg = true;
                        if (write_flg == true)
                        {
                            if (Form1.props.IsSeetNo)
                            {
                                if (line.Contains(Props.Commnet_SeetNo))
                                    continue;
                            }
                            if (!Json2Db(jmes))
                            {
                                _form.AddLog("コメント書き込み失敗", 9);
                                _form.AddLog(line, 9);
                            }
                        }
                    }
                    come_time_prev = come_time;
                    _cctl._come_list.ToArray()[i].Clear();
                    _cctl._come_list.ToArray()[i].TrimExcess();
                }

                _form.AddLog("コメントファイル出力終了", 1);

                _cctl._come_list.Clear();
                _cctl._come_list.TrimExcess();

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(AppendCommentDB), Ex);
                _form.AddLog("コメントファイル出力失敗", 1);
            }

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
                    StopHBTimer();
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

