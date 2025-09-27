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
using NicoNamaRokuga.Net;

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

        private Form1 _form = null;
        private Regex RgxCommand = new Regex(@"^{""([^""]+)"":", RegexOptions.Compiled);

        public NicoMessage(Form1 fo, BroadCastInfo bci, NicoLiveNet nln, NicoDb ndb)
        {
            IsDebug = false;

            MessageStatus = -1;
            this._nln = nln;
            this._bci = bci;
            this._ndb = ndb;
            this._form = fo;

            MessageStatus = 0;
        }

        public void Connect()
        {

        }

        ~NicoMessage()
        {
            this.Dispose();
        }

        /// サーバーから切断する
        public void Close()
        {
            MessageStatus = 1;
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

        //2024/8/5以降（メッセージサーバーに変更後）
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

