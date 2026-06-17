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
using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;
using Org.BouncyCastle.Crypto.Digests;

using NicoNamaRokuga.Prop;
using NicoNamaRokuga.Rec;
using NicoNamaRokuga.Net;
using Dwango.Nicolive.Chat.Service.Edge;

namespace NicoNamaRokuga.Message
{
    public class NicoMessage : IDisposable
    {
        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        public volatile int MessageStatus = -1; //MessageServerの状態

        private StreamWriter _sw = null;

        private NicoLiveNet _nln = null;         //WebClient
        private BroadCastInfo _bci = null;
        private NicoDb _ndb = null;
        private MessageServer _msc = null;
        private PackedServer _psc = null;
        private volatile bool _is_ts_start = true;
        private int _count_previous = 0;

        private Form1 _form = null;
        private Regex RgxCommand = new Regex(@"^{""([^""]+)"":", RegexOptions.Compiled);

        public NicoMessage(Form1 fo, BroadCastInfo bci, NicoLiveNet nln, NicoDb ndb)
        {
            IsDebug = false;

            this._nln = nln;
            this._bci = bci;
            this._ndb = ndb;
            this._form = fo;

            MessageStatus = 0;
            this._msc = null;
            _is_ts_start = true;
            _count_previous = 0;
        }

        //メッセージサーバー接続
        public async Task Connect(string uri)
        {
            bool IsMessageStart = false;
            string NextStreamAt = string.Empty;
            _is_ts_start = true;
            _count_previous = 2;

            try
            {
                _form.AddLog("メッセージサーバーに接続開始します:", 1);
                if (_msc == null)
                    _msc = new MessageServer(uri, null, MessageDataAsync, this);

                MessageStatus = 0;
                if (_bci.IsTimeShift())
                {
                    var when = _ndb.GetDbFromWhen();
                    _msc.SetNextStreamAt(when.ToString());
                }

                while (MessageStatus == 0)
                {
                    _form.AddLog("メッセージサーバーに接続します:" + _msc.GetNextStreamAt(), 1);
                    if (_msc != null)
                        NextStreamAt = _msc.GetNextStreamAt();
                    else
                        break;
                    if ((NextStreamAt.ToLower() == "now") && IsMessageStart == true)
                    {
                        _form.AddLog("MessageServer Connect Error: " + _msc.GetNextStreamAt(), 1);
                    }
                    else
                    {
                        IsMessageStart = true;
                        //_form.AddLog("*NextStreamAt: " + NextStreamAt, 1);
                        //_form.AddLog("*_msc.GetNextStreamAt(): " + _msc.GetNextStreamAt(), 1);
                        var status = await _msc.ConnectAsync();
                        if (!string.IsNullOrEmpty(status))
                        {
                            _form.AddLog("ConnectAsync() Error: " + status, 1);
                            break;
                        }
                    }
                    while ((NextStreamAt == _msc.GetNextStreamAt()) && MessageStatus == 0)
                    {
                        //_form.AddLog("**NextStreamAt: " + NextStreamAt, 1);
                        //_form.AddLog("**_msc.GetNextStreamAt(): " + _msc.GetNextStreamAt(), 1);
                        await Task.Delay(500);
                    }
                }
            }
            catch (Exception Ex)
            {
                _form.AddLog("メッセージサーバー接続エラー(Connect): \r\n" + Ex.Message, 1);
            }
        }

        //メッセージサーバーからのデーター処理
        public async Task MessageDataAsync(ChunkedEntry entry)
        {
            try
            {
                //解析処理
                //_form.AddLog("Enrty: " + entry.ToString(), 9);
                //_form.AddLog("EnrtyCase: " + entry.EntryCase.ToString(), 9);
                if (MessageStatus != 0 || _is_ts_start == false)
                    return;
                switch (entry.EntryCase)
                {
                    case ChunkedEntry.EntryOneofCase.Previous:
                        _form.AddLog("Previous: " + entry.Previous.Uri.ToString(), 9);
                        if (!_bci.IsTimeShift() && _count_previous > 0)
                        {
                            --_count_previous;
                            ConnectSegment(entry.Previous.Uri, "Previous");
                        }
                        break;
                    case ChunkedEntry.EntryOneofCase.Backward:
                        _form.AddLog("Backward: " + entry.Backward.Segment.Uri.ToString(), 9);
                        if (_bci.IsTimeShift() && _is_ts_start)
                        {
                            _is_ts_start = false;
                            ConnectPacked(entry.Backward.Segment.Uri.ToString());
                        }
                        break;
                    case ChunkedEntry.EntryOneofCase.Segment:
                        _form.AddLog("Segment: " + entry.Segment.Uri.ToString(), 9);
                        if (!_bci.IsTimeShift())
                        {
                            ConnectSegment(entry.Segment.Uri, "Segment");
                        }
                        break;
                    case ChunkedEntry.EntryOneofCase.Next:
                        _form.AddLog("NextAt: " + entry.Next.At.ToString(), 9);
                        if (!_bci.IsTimeShift())
                        {
                            if (_msc.GetNextStreamAt().ToLower() != "now")
                                _msc.SetBeforeNextStreamAt(_msc.GetNextStreamAt());
                            _msc.SetNextStreamAt(entry.Next.At.ToString());
                        }
                        break;
                    case ChunkedEntry.EntryOneofCase.None:
                        break;
                    default:
                        _form.AddLog("Unknown entry: " + entry.ToString(), 9);
                        break;
                }
                await Task.Delay(100);
            }
            catch (Exception Ex)
            {
                    _form.AddLog("メッセージサーバー接続エラー(MessageDataAsync): \r\n" + Ex.Message, 1);
            }
        }

        //メッセージサーバー切断
        public async Task Disconnect()
        {
            if (_msc != null)
            {
                MessageStatus = 1;
                _msc.Disconnect();
                await Task.Delay(100);
                _form.AddLog("メッセージサーバーから切断しました(Disconnect)", 1);
            }
        }

        //セグメントサーバー接続
        public async void ConnectSegment(string uri, string servername)
        {
            SegmentServer _ssc = null;

            try
            {
                if (_ssc == null)
                    _ssc = new SegmentServer(uri, null, servername, SegmentDataAsync, this);

                _form.AddLog(servername + "サーバーに接続します", 1);
                if (_ssc != null)
                {
                    var status = await _ssc.ConnectAsync();
                    if (!string.IsNullOrEmpty(status))
                    {
                        _form.AddLog("ConnectSegmentAsync() Error: " + status, 1);
                        await Task.Delay(100);
                    }
                }
                _form.AddLog(servername + "サーバーから切断されました", 1);
                _ssc = null;
            }
            catch (Exception Ex)
            {
                _form.AddLog("セグメントサーバー接続エラー(ConnectSegment): \r\n" + Ex.Message, 1);
                _ssc = null;
            }
        }

        //セグメントサーバーからのデーター処理
        public async Task SegmentDataAsync(ChunkedMessage message)
        {
            try
            {
                //解析処理
                //_form.AddLog("Message: " + message.ToString(), 9);
                //_form.AddLog("MessagePayloadCase: " + message.PayloadCase.ToString(), 9);
                if (MessageStatus != 0)
                    return;
                switch (message.PayloadCase)
                {
                    case ChunkedMessage.PayloadOneofCase.Signal:
                        //_form.AddLog("Signal:" + message.ToString(), 9);
                        break;
                    case ChunkedMessage.PayloadOneofCase.State:
                        //_form.AddLog("State:" + message.ToString(), 9);
                        CommentHandler("chat", message);
                        break;
                    case ChunkedMessage.PayloadOneofCase.Message:
                        //_form.AddLog("Mes:" + message.ToString(), 9);
                        CommentHandler("chat", message);
                        break;
                    case ChunkedMessage.PayloadOneofCase.None:
                        break;
                    default:
                        _form.AddLog("Unknown message: " + message.ToString(), 9);
                        break;
                }
                await Task.Delay(100);

            }
            catch (Exception Ex)
            {
                    _form.AddLog("セグメントサーバー接続エラー(SegmentDataAsync): \r\n" + Ex.Message, 1);
            }
        }

        public async void ConnectPacked(string uri)
        {
            try
            {
                if (_psc == null)
                    _psc = new PackedServer(uri, null, PackedDataAsync, this);

                _form.AddLog("Packedサーバーに接続します", 1);
                while (MessageStatus == 0)
                {
                    if (_psc != null)
                    {
                        var status = await _psc.ConnectAsync();
                        if (!string.IsNullOrEmpty(status))
                        {
                            _form.AddLog("ConnectPackedAsync() Error: " + status, 1);
                            await Task.Delay(100);
                        }
                    }

                }
                _form.AddLog("Packedサーバーから切断されました", 1);
                _psc = null;
            }
            catch (Exception Ex)
            {
                _form.AddLog("Packedサーバー接続エラー(ConnectPacked): \r\n" + Ex.Message, 1);
                _psc = null;
            }
        }

        //Packedサーバーからのデーター処理
        public async Task PackedDataAsync(PackedSegment segment)
        {
            var cnt = segment.Messages.Count();
            try
            {
                //解析処理
                //_form.AddLog("Segment: " + segment.ToString(), 9);
                if (MessageStatus != 0)
                    return;
                foreach (ChunkedMessage message in segment.Messages)
                {
                    CommentHandler("chat", message);
                    if (MessageStatus != 0)
                        return;
                }
                _form.AddLog("コメントを" + cnt.ToString() + "取得しました", 1);
                _psc.ClearBuffer();
                if (MessageStatus == 0)
                {
                    if (segment.Next != null && segment.Next.Uri != null)
                    {
                        var nexturi = segment.Next.Uri;
                        //_form.AddLog("nexturi: " + nexturi, 9);
                        if (_psc.GetNextUri() != nexturi)
                        {
                            _psc.SetNextUri(nexturi);
                            await Task.Delay(100);
                        }
                        else
                        {
                            _form.AddLog("Comment done.", 1);
                            MessageStatus = 1;
                            return;
                        }
                    }
                    else
                    {
                        _form.AddLog("Comment done.", 1);
                        MessageStatus = 1;
                        return;
                    }

                }
            }
            catch (Exception Ex)
            {
                _form.AddLog("Packedサーバー接続エラー(PackedDataAsync): \r\n" + Ex.Message, 1);
            }
        }
        ~NicoMessage()
        {
            this.Dispose();
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

        //vpos計算
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

        //2024/8/5以降のvpos計算（メッセージサーバーに変更後）
        public string CalcVpos(long start, long offset, string date, string vpos, long vposbasetime, int premium)
        {
            long ll = 0L;
            if (premium == 3)
            {
                // ret = (date - opentime) * 100 - offset
                long.TryParse(date, out ll);
                return ((ll - start) * 100L - offset).ToString();
            }
            else
            {
                // ret = vpos + ((vposbasetime - opentime) * 100) - offset
                long.TryParse(vpos, out ll);
                return (ll + ((vposbasetime - start) * 100L) - offset).ToString();
            }
        }

        //ISO8601形式の日付と時刻からUnixtime秒とマイクロ秒に変換する
        private (long unixtime, long micros, string err) GetUnixTimeAndMicros(string timestamp)
        {
            long unixtime = 0L, micros = 0L;
            string err = string.Empty;

            // UTC時刻をパース
            DateTime utcTime;
            string microseconds = "000000";

            try
            {
                var index = timestamp.LastIndexOf(".");
                if (index > -1)
                {
                    // マイクロ秒部分を抽出
                    var msec = timestamp.Substring(index + 1, timestamp.Length - index - 2);
                    msec += microseconds;
                    microseconds = msec.Substring(0, 6);
                    timestamp = timestamp.Substring(0, index) + "Z";
                }

                if (DateTime.TryParseExact(
                        timestamp,
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal,
                        out utcTime))
                {
                    // UNIXエポック (1970-01-01T00:00:00Z)
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    // 経過秒（小数以下切り捨て）
                    double totalSeconds = (utcTime - epoch).TotalSeconds;

                    unixtime = (long)(Math.Floor(totalSeconds));
                    long.TryParse(microseconds, out micros);
                }
                else
                {
                    err = "Error: Failed to DateTime.TryParseExact().";
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(GetUnixTimeAndMicros), Ex);
                err = Ex.Message;
            }

            return (unixtime, micros, err);
        }

        //ModifierがあればXML形式互換の要素に変換してListで返す
        private (List<string>, bool) GetModifier(JObject jsonObj)
        {
            var mail = new List<string>();
            bool isTranslucent = false;

            if (jsonObj["modifier"] == null || jsonObj["modifier"].Count() <= 0)
                return (mail, isTranslucent);

            var jsonMod = jsonObj["modifier"];
            // namedColor
            if (jsonMod["namedColor"] != null)
            {
                mail.Add(jsonMod["namedColor"].ToString());
            }
            else if (jsonMod["fullColor"] != null && jsonMod["fullColor"] is JObject fcol)
            {
                int r = fcol["r"] != null ? (int)fcol["r"] : 0;
                int g = fcol["g"] != null ? (int)fcol["g"] : 0;
                int b = fcol["b"] != null ? (int)fcol["b"] : 0;
                mail.Add($"#{r:X2}{g:X2}{b:X2}");
            }

            if (jsonMod["position"] != null)
                mail.Add(jsonMod["position"].ToString());
            if (jsonMod["size"] != null)
                mail.Add(jsonMod["size"].ToString());
            if (jsonMod["font"] != null)
                mail.Add(jsonMod["font"].ToString());

            if (jsonMod["opacity"] != null)
            {
                string opacity = jsonMod["opacity"].ToString();
                if (opacity == "Translucent")
                {
                    mail.Add(opacity);
                    isTranslucent = true;
                }
            }

            return (mail, isTranslucent);
        }

        public void CommentHandler(string tag, ChunkedMessage message)
        {

            string e = "";
            string s = "";

            try
            {
                if (message.Message != null)
                {
                    s = message.Message.ToString();
                    e = message.Message.DataCase.ToString();
                }
                else if (message.State != null)
                {
                    s = message.State.ToString();
                    if (message.State.Marquee != null)
                        e = "Marquee";
                    else if (message.State.Enquete != null)
                        e = "Enquete";
                    else if (message.State.MoveOrder != null)
                        e = "MoveOrder";
                    else if (message.State.TrialPanel != null)
                        e = "TrialPanel";
                    else
                        return;
                }
                else
                {
                    return;
                }
                //Console.WriteLine("s: " + s);
                //Console.WriteLine("DataCase: " + e);

                string jsonStr = null;
                JObject jsonObj = null;

                switch (e)
                {
                    case "Chat":
                        jsonStr = message.Message.Chat.ToString();
                        break;
                    case "SimpleNotification":
                        jsonStr = message.Message.SimpleNotification.ToString();
                        break;
                    case "SimpleNotificationV2":
                        jsonStr = message.Message.SimpleNotificationV2.ToString();
                        break;
                    case "Gift":
                        if (message.Message.Gift.GiftBarUpdate != null)
                            return;
                        jsonStr = message.Message.Gift.ToString();
                        break;
                    case "Nicoad":
                        jsonStr = message.Message.Nicoad.ToString();
                        break;
                    case "GameUpdate":
                        return;
                    case "TagUpdated":
                        return;
                    case "CruiseRecentContents":
                        jsonStr = message.Message.CruiseRecentContents.ToString();
                        break;
                    case "ModeratorUpdated":
                        jsonStr = message.Message.ModeratorUpdated.ToString();
                        break;
                    case "SsngUpdated":
                        jsonStr = message.Message.SsngUpdated.ToString();
                        break;
                    case "OverflowedChat":
                        _form.AddLog("commentHandler: Recieved OverflowedChat", 1);
                        return;
                    case "FeaturesUpdated":
                        return;
                    //ここからstate
                    case "Marquee":
                        //_form.AddLog($"Marquee: {message.State.Marquee.ToString()}", 9);
                        if (message.State.Marquee.Display == null)
                            return;
                        else
                            jsonStr = message.State.Marquee.Display.ToString();
                        break;
                    case "Enquete":
                        if (message.State.Enquete != null)
                            return;
                        jsonStr = message.State.Enquete.ToString();
                        break;
                    case "Statistics":
                        //jsonStr = message.State.Statistics.ToString();
                        return;
                    case "TrialPanel":
                        jsonStr = message.State.TrialPanel.ToString();
                        break;
                    case "ProgramStatus":
                        //jsonStr = message.State.ProgramStatus.ToString();
                        return;
                    case "MoveOrder":
                        //_form.AddLog($"MoveOrder: {message.ToString()}", 9);
                        jsonStr = message.State.MoveOrder.ToString();
                        break;
                    default:
                        _form.AddLog($"Unknown DataCase: {message.ToString()}", 1);
                        return;
                }
                //Console.WriteLine("Data: " + jsonStr.ToString());
                jsonObj = JObject.Parse(jsonStr);

                JToken jtkn;
                var attrMap = new Dictionary<string, object>();

                if (jsonObj.TryGetValue("no", out jtkn))
                    attrMap["no"] = Convert.ToInt64(jtkn.ToString());

                if (jsonObj.TryGetValue("name", out jtkn))
                    attrMap["name"] = jtkn.ToString();

                long vpos = 0;
                if (jsonObj.TryGetValue("vpos", out jtkn))
                    vpos = Convert.ToInt64(jtkn.ToString());
                attrMap["vpos"] = vpos;

                long date = 0, date_usec = 0;
                string err = string.Empty;
                var metaDate = message.Meta.At.ToString().Trim('"');
                if (!string.IsNullOrEmpty(metaDate))
                {
                    (date, date_usec, err) = GetUnixTimeAndMicros(metaDate);
                    if (!string.IsNullOrEmpty(err))
                    {
                        _form.AddLog("CommentHandler: " + err, 9);
                        _form.AddLog("metaDate: " + metaDate, 9);
                    }

                }
                //Console.WriteLine("date=" + date + " date_usec=" + date_usec);
                attrMap["date"] = date;
                attrMap["date_usec"] = date_usec;
                attrMap["date2"] = (date * 1000 * 1000) + date_usec;

                string userId = "";
                var mail = new List<string>();
                if (jsonObj.TryGetValue("hashedUserId", out jtkn))
                {
                    userId = jtkn.ToString();
                    attrMap["anonymity"] = 1;
                    mail.Add("184");
                }
                else if (jsonObj.TryGetValue("rawUserId", out jtkn))
                {
                    userId = jtkn.ToString();
                }
                attrMap["user_id"] = userId.ToString();
                //Console.WriteLine("user_id: " + attrMap["user_id"]);

                if (jsonObj.TryGetValue("accountStatus", out jtkn))
                    attrMap["premium"] = 1;

                var content = string.Empty;
                var modifier = new List<string>();
                bool translucent;
                JObject jsonObj2;
                switch (e)
                {
                    case "Chat":
                        // Modifier処理
                        (modifier, translucent) = GetModifier(jsonObj);

                        if (modifier.Count > 0)
                            mail.AddRange(modifier);

                        if (translucent)
                        {
                            if (attrMap.ContainsKey("premium") &&
                                Convert.ToInt32(attrMap["premium"]) == 1)
                                attrMap["premium"] = 25;
                            else
                                attrMap["premium"] = 24;
                        }

                        if (mail.Count > 0)
                            attrMap["mail"] = string.Join(" ", mail);

                        if (jsonObj.TryGetValue("content", out jtkn))
                            content = jtkn.ToString();
                        break;
                    case "SimpleNotification":
                        attrMap["premium"] = 3;
                        if (jsonObj.TryGetValue("emotion", out jtkn))
                            content = "/emotion " + jtkn.ToString();
                        else if (jsonObj.TryGetValue("cruise", out jtkn))
                            content = "/cruise \"" + jtkn.ToString() + "\"";
                        else if (jsonObj.TryGetValue("quote", out jtkn))
                            content = "/quote \"" + jtkn.ToString() + "\"";
                        else if (jsonObj.TryGetValue("programExtended", out jtkn))
                            content = "/info 3 " + jtkn.ToString(); //3秒
                        else if (jsonObj.TryGetValue("rankingIn", out jtkn))
                            content = "/info 8 " + jtkn.ToString();	//8秒
                        else if (jsonObj.TryGetValue("rankingUpdated", out jtkn))
                            content = "/info 8 " + jtkn.ToString();	//8秒
                        else if (jsonObj.TryGetValue("visited", out jtkn))
                            content = "/info 10 " + jtkn.ToString();
                        else if (jsonObj.TryGetValue("ichiba", out jtkn))
                            content = "/info 10 " + jtkn.ToString();
                        else
                        {
                            _form.AddLog($"Unknown SimpleNotification: {message.ToString()}", 1);
                            content = "/info 10 " + jsonStr;
                        }
                        break;
                    case "SimpleNotificationV2":
                        attrMap["premium"] = 3;
                        var type = string.Empty;
                        if (jsonObj.TryGetValue("type", out jtkn))
                            type = jtkn.ToString();
                        if (type == "ICHIBA")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 10 " + jtkn.ToString();
                        }
                        else if (type == "EMOTION")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/emotion " + jtkn.ToString();
                        }
                        else if (type == "CRUISE")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/cruise \"" + jtkn.ToString() + "\"";
                        }
                        else if (type == "PROGRAM_EXTENDED")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 3 " + jtkn.ToString(); //3秒
                        }
                        else if (type == "RANKING_IN")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 8 " + jtkn.ToString(); //8秒
                        }
                        else if (type == "VISITED")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 3 " + jtkn.ToString();
                        }
                        else if (type == "SUPPORTER_REGISTERED")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 5 " + jtkn.ToString();
                        }
                        else if (type == "USER_LEVEL_UP")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 5 " + jtkn.ToString();
                        }
                        else if (type == "USER_FOLLOW")
                        {
                            if (jsonObj.TryGetValue("message", out jtkn))
                                content = "/info 5 " + jtkn.ToString();
                        }
                        //else if (type == "")
                        //{
                        //    if (jsonObj.TryGetValue("message", out jtkn))
                        //        content = "/info 5 " + jtkn.ToString();
                        //}
                        else
                        {
                            _form.AddLog($"Unknown SimpleNotificationV2: {message.ToString()}", 1);
                            content = "/info 10 " + jsonStr;
                        }
                        break;
                    case "Gift":
                        attrMap["premium"] = 3;
                        //_form.AddLog($"Gift: {message.ToString()}", 1);
                        if (jsonObj.TryGetValue("itemId", out jtkn))
                            content = "/gift " + jtkn.ToString();
                        if (jsonObj.TryGetValue("advertiserUserId", out jtkn))
                            content += " " + jtkn.ToString();
                        else
                            content += " NULL";

                        if (jsonObj.TryGetValue("advertiserName", out jtkn))
                            content += " \"" + jtkn.ToString() + "\"";
                        if (jsonObj.TryGetValue("point", out jtkn))
                            content += " " + jtkn.ToString();
                        if (jsonObj.TryGetValue("itemName", out jtkn))
                            content += " \"\" \"" + jtkn.ToString() + "\"";
                        if (jsonObj.TryGetValue("contributionRank", out jtkn))
                            content += " " + jtkn.ToString();

                        if (content.Length <= 0)
                        {
                            content = "/gift " + jsonStr;
                            _form.AddLog($"[FIXME]: {content}", 9);
                        }
                        break;
                    case "Nicoad":
                        attrMap["premium"] = 3;
                        //_form.AddLog($"Nicoad: {message.ToString()}", 1);
                        if (jsonObj.TryGetValue("v1", out jtkn))
                        {
                            jsonObj2 = (JObject)jtkn;
                            if (jsonObj2.TryGetValue("totalAdPoint", out jtkn))
                                content = "/nicoad {\"version:\":\"1\",\"totalAdPoint\":" + jtkn.ToString() + ",";
                            if (jsonObj2.TryGetValue("message", out jtkn))
                                content += "\"message\":\"" + jtkn.ToString() + "\"}";
                        }
                        else if(jsonObj.TryGetValue("v0", out jtkn))
                        {
                            jsonObj2 = (JObject)jtkn;
                            if (jsonObj2.TryGetValue("totalPoint", out jtkn))
                                content = "/nicoad {\"version:\":\"1\",\"totalAdPoint\":" + jtkn.ToString() + ",";
                            if (jsonObj2.TryGetValue("latest", out jtkn))
                                content += "\"message\":\"" + jtkn["message"].ToString() + "\"}";
                        }
                        if (content.Length <= 0)
                        {
                            content = "/nicoad " + jsonStr;
                            _form.AddLog($"[FIXME]: {content}", 9);
                        }
                        break;
                    case "CruiseRecentContents":
                        _form.AddLog($"CruiseRecentContents: {message.ToString()}", 1);
                        break;
                    case "Marquee":
                        attrMap["premium"] = 3;
                        if (jsonObj.TryGetValue("operatorComment", out jtkn))
                        {
                            jsonObj2 = (JObject)jtkn;
                            modifier = new List<string>();
                            (modifier, _) = GetModifier(jsonObj2);
                            if (modifier.Count > 0)
                                attrMap["mail"] = string.Join(" ", modifier);
                            if (jsonObj2.TryGetValue("content", out jtkn))
                                content = jtkn.ToString();
                            if (jsonObj2.TryGetValue("link", out jtkn))
                                content += "(\"" + jtkn.ToString() + "\")";
                        }
                        break;
                    case "Enquete":
                        _form.AddLog($"Enquete: {message.ToString()}", 9);
                        attrMap["premium"] = 3;
                        if (message.State.Enquete.Status.ToString() == "Close")
                        {
                            content = "/vote stop";
                        }
                        else if (message.State.Enquete.Choices != null)
                        {
                            if (message.State.Enquete.Choices.FirstOrDefault().HasPerMille)
                            {
                                content = "/vote showresult per ";
                                content += string.Join(" ", message.State.Enquete.Choices.Select(x => x.PerMille.ToString()).ToArray());
                            }
                            else
                            {
                                content = "/vote start \"" + message.State.Enquete.Question.ToString() + "\" ";
                                content += string.Join(" ", message.State.Enquete.Choices.Select(x => "\"" + x.Description + "\"").ToArray());
                            }
                        }
                        break;
                    case "TrialPanel":
                        break;
                    case "MoveOrder":
                        attrMap["premium"] = 3;
                        string _message = string.Empty, _content = string.Empty;
                        if (jsonObj.TryGetValue("jump", out jtkn))
                        {
                            jsonObj2 = (JObject)jtkn;
                            if (jsonObj2.TryGetValue("message", out jtkn))
                                _message = jtkn.ToString();
                            if (jsonObj2.TryGetValue("content", out jtkn))
                                _content = jtkn.ToString();
                            content = "/move_order " + _message +
                              "(https://live.nicovideo.jp/watch/" + _content + ")";
                        }
                        else if (jsonObj.TryGetValue("redirect", out jtkn))
                        {
                            jsonObj2 = (JObject)jtkn;
                            if (jsonObj2.TryGetValue("message", out jtkn))
                                _message = jtkn.ToString();
                            if (jsonObj2.TryGetValue("uri", out jtkn))
                                _content = jtkn.ToString();
                            content = "/move_order " + _message +
                                    "(" + _content + ")";
                        }
                        else
                        {
                            content = "/move_order " + jsonStr;
                            _form.AddLog($"[FIXME]: {content}", 9);
                        }
                        break;
                    default:
                        _form.AddLog($"DataCase: {message.ToString()}", 1);
                        break;
                }
                attrMap["content"] = content;

                if (string.IsNullOrEmpty(content))
                    return;

                var calc_s = string.Format("{0:N},{1:N},{2:N},{3},{4}", vpos, date, date_usec, userId, content);
                //var hash:= fmt.Sprintf("%x", sha3.Sum256([]byte(calc_s)))
                var hashAlgorithm = new Sha3Digest(256);
                byte[] input = Encoding.UTF8.GetBytes(calc_s);
                hashAlgorithm.BlockUpdate(input, 0, input.Length);
                byte[] result = new byte[32]; // 256 / 8 = 64
                hashAlgorithm.DoFinal(result, 0);
                string hash = BitConverter.ToString(result);
                hash = hash.Replace("-", "").ToLowerInvariant();
                attrMap["hash"] = hash;
                //var ttt = "calc_s: " + calc_s + "\r\n" +
                //          "hash: " + hash + "\r\n" +
                //          "mail: " + mail + "\r\n" +
                //          "userId: " + userId + "\r\n" +
                //          "content: " + content + "\r\n";
                //MessageBox.Show(ttt);

                var metaId = message.Meta.Id.ToString();
                if (!string.IsNullOrEmpty(metaId))
                    attrMap["thread"] = metaId;

                Table2Db(attrMap);

                //if (isTimeshift || dbKVExist("comment/thread") == 0)
                //else
                //{
                //    {
                //        if (attrMap.ContainsKey("thread"))
                //            dbKVSet("comment/thread", attrMap["thread"].ToString());
                //    }
                //}
        }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(CommentHandler), Ex);
            }
        }

        private bool Table2Db(IDictionary<string, object> data)
        {
            var r_hash = new Dictionary<string, string>();
            var mail = string.Empty;
            var content = string.Empty;
            var user_id = string.Empty;
            if (data.Count <= 0)
                return false;

            try
            {
                string value;
                foreach (var it in data)
                {
                    value = it.Value.ToString();
                    switch (it.Key.ToString())
                    {
                        case "mail":
                            r_hash[it.Key] = "@" + it.Key;
                            mail = it.Value.ToString();
                            break;
                        case "name":
                            r_hash[it.Key] = "\"" + it.Value.ToString() + "\"";
                            break;
                        case "user_id":
                            r_hash[it.Key] = "@" + it.Key;
                            user_id = it.Value.ToString();
                            break;
                        case "content":
                            r_hash[it.Key] = "@" + it.Key;
                            content = it.Value.ToString();
                            break;
                        default:
                            if (it.Value.GetType().ToString().ToLower().Contains("int"))
                            {
                                r_hash[it.Key] = it.Value.ToString();
                            }
                            else if (it.Value.GetType().ToString().ToLower().Contains("string"))
                            {
                                r_hash[it.Key] = "\"" + it.Value.ToString() + "\"";
                            }
                            break;
                    }
                }
                var command = "(" + string.Join(", ", r_hash.Keys.ToArray()) + ") VALUES \n(" + string.Join(", ", r_hash.Values.ToArray()) + ");\n";
                _ndb.WriteDbComment(command, mail, user_id, content);
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(Table2Db), Ex);
                return false;
            }

            return true;
        }

        private bool Json2Db(JObject jmes)
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
                    else if (it.Key == "name")
                    {
                        r_hash[it.Key] = "\"" + it.Value.ToString() + "\"";
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
                if (!r_hash.ContainsKey("date_usec")) r_hash["date_usec"] = "0";
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

        private string Json2Xml(JObject jmes)
        {
            var result = string.Empty;
            var content = string.Empty;

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
                            content = Props.HtmlEncode(it2.Value.ToString());
                        }
                        else if (it2.Key.ToString() == "name")
                        {
                            result += " " + it2.Key.ToString() + @"=""" + Props.HtmlEncode(it2.Value.ToString()) + @"""";
                        }
                        else
                        {
                            result += " " + it2.Key.ToString() + @"=""" + it2.Value.ToString() + @"""";
                        }
                    }
                    if (it.Key.ToString() == "thread")
                        result += " />\r\n";
                    else
                        result += ">" + content + "</" + it.Key.ToString() + ">\r\n";
                }
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
            var content = string.Empty;
            if (data.Count <= 0)
                return result;
            else
                result = "<chat";

            try
            {
                string value;
                foreach (var it in data)
                {
                    value = it.Value.ToString();
                    switch (it.Key.ToString())
                    {
                        case "thread":
                            result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "no":
                            if (int.Parse(value) > -1)
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "mail":
                            if (value != "")
                                result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                        case "name":
                            if (value != "")
                                result += " " + it.Key.ToString() + @"=""" + Props.HtmlEncode(value) + @"""";
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
                            content = Props.HtmlEncode(value);
                            break;
                        default:
                            result += " " + it.Key.ToString() + @"=""" + value + @"""";
                            break;
                    }
                }
                result += ">" + content + "</chat>\r\n";
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(Table2Xml), Ex);
                return result;
            }

            return result;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
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

