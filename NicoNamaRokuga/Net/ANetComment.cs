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
    public abstract class ANetComment
    {
        public volatile int WsStatus = -1; //WebSocketの状態

        //WebSocket
        protected WebSocket _ws = null;
        protected bool _wsconnect = false;

        protected long _seq_no = 0;
        protected bool _chat_flg = true;                    //チャットデータの最初かどうか？
        protected const int _MESSAGE_MAX = 1000;
        protected StreamWriter _sw = null;

        protected System.Threading.Timer _hbTimer;

        protected NicoLiveNet _nLiveNet = null;         //WebClient
        protected BroadCastInfo _bci = null;
        protected CommentInfo _cmi = null;
        protected NicoDb _ndb = null;
        protected CommentControl _cCtrl = null;

        protected Form1 _form = null;
        protected Regex RgxCommand = new Regex(@"^{""([^""]+)"":", RegexOptions.Compiled);

        /// サーバーに接続する
        public abstract void Connect(string wsurl);
        /// 文字列受信(生放送)
        protected abstract void wsReceived(object sender, MessageReceivedEventArgs e);
        /// 文字列受信(生放送)(DB)
        protected abstract void wsReceivedDB(object sender, MessageReceivedEventArgs e);
        /// 文字列受信(TS)
        protected abstract void wsReceivedTS(object sender, MessageReceivedEventArgs e);

        /// サーバーから切断する
        public void Close()
        {
            _ws?.Close();
        }

        //生放送コメント取得
        public void StartGetComment()
        {
            var ttt = SendThread(_cmi.ThreadId, _cmi.UserId, -150);
            _form.AddLog(ttt, 9);
        }

        //TSコメント取得
        public void StartGetTSComment()
        {
            try
            {
                if (_cCtrl.status == 0) //TSコメント取得開始
                {
                    _cCtrl._waybackkey = null;
                    _cCtrl._when = _cmi.EndTime + 120L;
                    _cCtrl.status = 1; //TSコメント取得中
                }
                _chat_flg = true;
                var ttt = SendThreadTS(_cmi.ThreadId, _cmi.UserId, -_MESSAGE_MAX, _cCtrl._when.ToString(), _cCtrl._waybackkey);
                _form.AddLog(ttt, 9);

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(StartGetTSComment), Ex);
            }
        }

        //生放送コメント取得
        protected string SendThread(string threadId, string user_id, int from)
        {
            var s = @"[{""ping"":{""content"":""rs:%%seqno%%""}},{""ping"":{""content"":""ps:%%seqno2%%""}},"
                  + @"{""thread"":{""thread"":""%%threadId%%"",""version"":""20061206"","
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
        //  TSはwhenで時間、res_fromで件数か番号を選んでループして取得
        protected string SendThreadTS(string threadId, string user_id, int from, string when, string waybackkey)
        {
            var s = @"[{""ping"":{""content"":""rs:%%seqno%%""}},{""ping"":{""content"":""ps:%%seqno2%%""}},"
                  + @"{""thread"":{""thread"":""%%threadId%%"",""version"":""20061206"","
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

        //TSコメント出力
        protected abstract void AppendComment();
        //TSコメント出力(DB)
        protected abstract void AppendCommentDB();

        public void BeginXmlDoc()
        {
            _sw.Write("<?xml version='1.0' encoding='UTF-8'?>\r\n");
            _sw.Write("<packet>\r\n");
        }

        public void EndXmlDoc()
        {
            _sw.Write("</packet>\r\n");
        }

        public void SetStreamWriter(StreamWriter sw)
        {
            if (_sw == null && sw != null)
                _sw = sw;
        }

        public void DisposeStreamWriter()
        {
            if (_sw != null)
                _sw = null;
        }

        public string CalcVpos(long start, long offset, string date, string vpos, string provider_type)
        {
            if (provider_type != "official")
            {
                long ll = start * 100 + offset;
                long.TryParse(date, out ll);
                return ((ll - start) * 100L - offset).ToString();
            }
            else
            {
                long ll = offset;
                long.TryParse(vpos, out ll);
                return (ll - offset).ToString();
            }
        }

        protected bool Json2Db(JObject jmes)
        {
            var r_hash = new Dictionary<string, string>();
            var mail = string.Empty;
            var content = string.Empty;
            var user_id = string.Empty;

            if (string.IsNullOrEmpty(jmes.ToString()))
                return false;

            try
            {
                JToken jtkn;
                if (jmes.TryGetValue("thread", out jtkn))
                {
                    _ndb.WriteDbKvs("comment/thread", System.Data.DbType.String, (string)jtkn["thread"]);
                    return true;
                }
                else if (!jmes.TryGetValue("chat", out jtkn))
                {
                    return false;
                }
                foreach (var it in JObject.Parse(jtkn.ToString()))
                {
                    if (it.Key == "mail")
                    {
                        r_hash[it.Key] = "@" + it.Key;
                        mail = it.Value.ToString();
                    }
                    else if (it.Key == "user_id")
                    {
                        r_hash[it.Key] = "@" + it.Key;
                        user_id = it.Value.ToString();
                    }
                    else if (it.Key == "content")
                    {
                        r_hash[it.Key] = "@" + it.Key;
                        content = it.Value.ToString();
                    }
                    else if (it.Value.Type == JTokenType.Integer)
                    {
                        r_hash[it.Key] = it.Value.ToString();
                    }
                    else if (it.Value.Type == JTokenType.String)
                    {
                        r_hash[it.Key] = "\"" + it.Value.ToString() + "\"";
                    }
                }
                if (!r_hash.ContainsKey("vpos")) r_hash["vpos"] = "0";
                r_hash["date2"] = ((long.Parse(r_hash["date"]) * 1000L * 1000L) + long.Parse(r_hash["date_usec"])).ToString();

                var calc_s = string.Format("{0:N},{1:N},{2:N},{3},{4}", r_hash["vpos"], r_hash["date"], r_hash["date_usec"], user_id, content);
                //var hash:= fmt.Sprintf("%x", sha3.Sum256([]byte(calc_s)))
                var hashAlgorithm = new Sha3Digest(256);
                byte[] input = Encoding.UTF8.GetBytes(calc_s);
                hashAlgorithm.BlockUpdate(input, 0, input.Length);
                byte[] result = new byte[32]; // 256 / 8 = 64
                hashAlgorithm.DoFinal(result, 0);
                string hash = BitConverter.ToString(result);
                hash = hash.Replace("-", "").ToLowerInvariant();
                r_hash["hash"] = "\"" + hash + "\"";
                //var ttt = "calc_s: " + calc_s + "\r\n" +
                //          "hash: " + hash + "\r\n" +
                //          "mail: " + mail + "\r\n" +
                //          "user_id: " + user_id + "\r\n" +
                //          "content: " + content + "\r\n";
                //MessageBox.Show(ttt);

                var command = "(" + string.Join(", ", r_hash.Keys.ToArray()) + ") VALUES \n(" + string.Join(", ", r_hash.Values.ToArray()) + ");\n";
                _ndb.WriteDbComment(command, mail, user_id, content);
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(Json2Db), Ex);
                return false;
            }

            return true;
        }

        protected string Json2Xml(JObject jmes)
        {
            var result = string.Empty;

            if (string.IsNullOrEmpty(jmes.ToString()))
                return result;

            try
            {
                foreach (var it in jmes)
                {
                    result = "<" + it.Key.ToString();
                    foreach (var it2 in (JObject)it.Value)
                    {
                        if (it2.Key.ToString() == "content")
                        {
                            result += ">" + HttpUtility.HtmlEncode(it2.Value.ToString());
                            result += "</" + it.Key.ToString();
                        }
                        else
                        {
                            result += " " + it2.Key.ToString() + @"=""" + it2.Value.ToString() + @"""";
                        }
                    }
                }
                if (result.IndexOf("<thread") == 0)
                    result += "/>\r\n";
                else
                    result += ">\r\n";

            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(Json2Xml), Ex);
                return result;
            }

            return result;
        }

        public string Table2Xml(IDictionary<string, string> data)
        {
            var result = string.Empty;
            if (data.Count <= 0)
                return result;

            try
            {
                string value;
                foreach (var it in data)
                {
                    value = it.Value.ToString();
                    switch (it.Key.ToString())
                    {
                        case "thread":
                            result = "<chat " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "no":
                            if (int.Parse(value) > -1)
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "mail":
                            if (value != "")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "premium":
                            if (value != "0")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "anonymity":
                            if (value != "0")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "score":
                            if (value != "0")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "origin":
                            if (value != "")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "locale":
                            if (value != "")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "content":
                            result += ">" + HttpUtility.HtmlEncode(value) + "</chat>\r\n";
                            break;
                        default:
                            result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                    }

                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(Table2Xml), Ex);
                return result;
            }

            return result;
        }


        //Timer Start/Stop
        protected void StartHBTimer()
        {
            var time = TimeSpan.FromSeconds(40.0);

            _hbTimer = new System.Threading.Timer(_ =>
            {
                _ws?.Send(string.Empty);
                _form.AddLog("Comment HeartBeat", 9);
            }
            , null, time, time);
        }

        protected void StopHBTimer()
        {
            _hbTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _hbTimer?.Dispose();
            _hbTimer = null;
        }

    }
}
