using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NicoNamaRokuga.Net;
using NicoNamaRokuga.Proc;
using NicoNamaRokuga.Prop;

namespace NicoNamaRokuga.Rec
{
    public class PlayListInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }
        public string MasterUrl { set; get; }
        public string BaseUrl { set; get; }
        public double StartTime { set; get; }
        public ICollection<PlayerInfo> Player { private set; get; }
        public int SeqNo { set; get; }
        public int CurrentNo { set; get; }
        public int LastSeqNo { set; get; }
        public bool EndList { set; get; }
        public double Position { set; get; }

        public PlayListInfo()
        {
            this.Status = "";
            this.Error = "";
            this.Player = new List<PlayerInfo>();
            this.SeqNo = -1;
            this.LastSeqNo = -1;
            this.EndList = false;
            this.Position = -1.0;
        }
    }

    public class PlayerInfo
    {
        public int Bandwidth { set; get; }
        public string pUrl { set; get; }
    }

    public class SegmentInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }
        public string BaseUrl { set; get; }
        public ICollection<Segment> Seg { private set; get; }
        public int SeqNo { set; get; }
        public bool EndList { set; get; }
        public double Position { set; get; }

        public SegmentInfo()
        {
            this.Status = "";
            this.Error = "";
            this.Seg = new List<Segment>();
            this.SeqNo = -1;
            this.EndList = false;
        }
    }

    public class Segment
    {
        public double ExtInfo { set; get; }
        public string sUrl { set; get; }
        public string sFile { set; get; }
    }

    public class RecHtml : EProcess, IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private WebClientEx _wc = null;
        private NicoDb _nd = null;

        private class WebClientEx : WebClient
        {
            public CookieContainer cookieContainer = new CookieContainer();
            private int timeout;

            public WebClientEx(int timeout) : base()
            {
                this.timeout = timeout;
            }

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

        //public volatile int PsStatus = -1; //実行ファイルの状態

        private NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        private BroadCastInfo _bci = null;
        private Form1 _form = null;

        public RecHtml(Form1 fo, BroadCastInfo bci, NicoNetComment nNetComment, CookieContainer cc)
        {
            IsDebug = false;

            PsStatus = -1;
            this._nNetComment = nNetComment;
            this._bci = bci;
            this._form = fo;

            var timeout = Form1.props.Timeout1;
            if (_bci.IsTimeShift()) timeout = Form1.props.Timeout2;
            var wc = new WebClientEx(timeout);
            _wc = wc;

            _wc.Encoding = Encoding.UTF8;
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
            _wc.Headers.Add(HttpRequestHeader.Referer, Props.GetLiveUrl(_bci.LiveId));
            _wc.cookieContainer = cc;
            if (IsDebug)
            {
                foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                    Debug.WriteLine(ck.Name.ToString() + ": " + ck.Value.ToString());
                for (int i = 0; i < _wc.Headers.Count; i++)
                    Debug.WriteLine(_wc.Headers.GetKey(i).ToString() + ": " + _wc.Headers.Get(i));
            }
        }

        ~RecHtml()
        {
            this.Dispose();
        }

        public override void ExecPs(string masterfile, string outfile)
        {
            try
            {
                _form.AddLog("プロセス実行中です。", 1);
                PsStatus = 0; //実行中
                //生放送の場合コメント出力開始
                if (Form1.props.IsComment && !_bci.IsTimeShift())
                {
                    while (_nNetComment.WsStatus != 0) ;
                    _nNetComment.StartGetComment();
                }
                if (_bci.IsTimeShift())
                    Task.Run(() => HtmlRecordTS(masterfile, outfile));
                else
                    Task.Run(() => HtmlRecord(masterfile, outfile));
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }

        private async Task HtmlRecordTS(string masterfile, string outfile)
        {
            try
            {
                var file = outfile;
                if (_bci.IsTimeShift()) file += Props.TIMESHIFT;
                file += ".sqlite3";
                _form.AddExecLog("Output: " + file + "\r\n");
                var nd = new NicoDb(_form, file);
                _nd = nd;

                // masterファイルをGet
                var pli = await GetMasterM3u8Async(masterfile);
                if (pli.Status != "Ok" || pli.Player.Count() <= 0)
                {
                    _form.AddExecLog("GetMasterM3u8 Error: " + pli.Error + "\r\n");
                    EndPs(1);
                }
                await Task.Delay(500);

                //速度 X2.0
                await SetPlayControlAsync(2, pli);

                while (PsStatus == 0)
                {
                    if (pli.EndList)
                    {
                         EndPs(1);
                         break;
                    }
                    // playerファイルをGet
                    var sgi = await GetPlayerM3u8Async(pli.Player.FirstOrDefault().pUrl);
                    if (sgi.Status != "Ok" || sgi.Seg.Count() <= 0)
                    {
                        _form.AddExecLog("GetPlayerM3u8 Error: " + sgi.Error + "\r\n");
                        EndPs(2); //Retry
                    }
                    if (pli.SeqNo < 0)
                    {
                        pli.SeqNo = sgi.SeqNo;
                        pli.CurrentNo = sgi.SeqNo;
                        pli.Position = sgi.Position;
                    }
                    await Task.Delay(500);

                    // 指定秒ごとにSegmentファイルを取得
                    foreach (var item in sgi.Seg)
                    {
                        if (PsStatus > 0) break;
                        if (sgi.SeqNo >= pli.SeqNo)
                        {
                            if (!await GetSegmentAsync(item, file, pli, sgi))
                                EndPs(2); //Retry
                            if (PsStatus > 0) break;
                            sgi.SeqNo++;
                            sgi.Position += item.ExtInfo;
                            await Task.Delay(500);
                        }
                        else
                        {
                            if (PsStatus > 0) break;
                            sgi.SeqNo++;
                            sgi.Position += item.ExtInfo;
                            await Task.Delay(500);
                        }
                    }
                    pli.SeqNo = sgi.SeqNo;
                    pli.CurrentNo = sgi.SeqNo;
                    pli.Position = sgi.Position;
                    if (sgi.EndList)
                    {
                        pli.EndList = true;
                         EndPs(1);
                    }
                }
                EndPs(1);
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }

        public void EndPs(int status)
        {
            //1:正常終了 2:異常終了
            PsStatus = status;
            //生放送の場合プロセスが終了したらコメントサーバーを切断する。
            if (Form1.props.IsComment)
            {
                if (!_bci.IsTimeShift() && _nNetComment.WsStatus == 0)
                {
                    _nNetComment?.Close();
                    _form.AddLog("コメントファイル出力終了", 1);
                    _nNetComment.EndXmlDoc();
                    _nNetComment?.Dispose();
                    _nNetComment.WsStatus = 2;  //再接続なし
                }
            }
        }

        private async Task HtmlRecord(string masterfile, string outfile)
        {
            try
            {
                var file = outfile;
                file += ".sqlite3";
                _form.AddExecLog("Output: " + file + "\r\n");
                var nd = new NicoDb(_form, file);
                _nd = nd;

                // masterファイルをGet
                var pli = await GetMasterM3u8Async(masterfile);
                if (pli.Status != "Ok" || pli.Player.Count() <= 0)
                {
                    _form.AddExecLog("GetMasterM3u8 Error: " + pli.Error + "\r\n");
                    EndPs(1);
                }
                await Task.Delay(250);

                while (PsStatus == 0)
                {
                    if (pli.EndList)
                    {
                         EndPs(1);
                         break;
                    }
                    // playerファイルをGet
                    var sgi = await GetPlayerM3u8Async(pli.Player.FirstOrDefault().pUrl);
                    if (sgi.Status != "Ok" || sgi.Seg.Count() <= 0)
                    {
                        _form.AddExecLog("GetPlayerM3u8 Error: " + sgi.Error + "\r\n");
                        EndPs(2); //Retry
                    }
                    if (pli.SeqNo < 0)
                    {
                        pli.SeqNo = sgi.SeqNo;
                        pli.CurrentNo = sgi.SeqNo;
                        //pli.Position = sgi.Position;
                    }
                    await Task.Delay(250);

                    // 指定秒ごとにSegmentファイルを取得
                    foreach (var item in sgi.Seg)
                    {
                        if (PsStatus > 0) break;
                        if (sgi.SeqNo >= pli.SeqNo)
                        {
                            if (!await GetSegmentAsync(item, file, pli, sgi))
                                EndPs(2); //Retry
                            if (PsStatus > 0) break;
                            sgi.SeqNo++;
                            //sgi.Position += item.ExtInfo;
                            await Task.Delay(500);
                        }
                        else
                        {
                            if (PsStatus > 0) break;
                            sgi.SeqNo++;
                            //sgi.Position += item.ExtInfo;
                            await Task.Delay(500);
                        }
                    }
                    pli.SeqNo = sgi.SeqNo;
                    pli.CurrentNo = sgi.SeqNo;
                    //pli.Position = sgi.Position;
                    if (sgi.EndList)
                    {
                        pli.EndList = true;
                         EndPs(1);
                    }
                }
                EndPs(1);
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }

        public override void BreakProcess(string breakkey)
        {
            EndPs(1);
        }

        //master.m3u8からplayer.m3u8のURLを取得
        public async Task<PlayListInfo> GetMasterM3u8Async(string url)
        {
            _form.AddExecLog("GetMasterFile\r\n");
            var pli = new PlayListInfo();
            pli.Status = "Error";
            pli.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return pli;

            try
            {
                pli.MasterUrl = url;
                var idx = url.IndexOf("master.m3u8");
                if (idx >= 0) pli.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    int bw;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        _form.AddExecLog(line + "\r\n");
                        if (line.Contains("#EXT-X-STREAM-INF"))
                        {
                            var pi = new PlayerInfo();
                            if (int.TryParse(Regex.Match(line, @"[:,]BANDWIDTH=(\d+)").Groups[1].Value, out bw))
                                pi.Bandwidth = bw;
                            line = sr.ReadLine();
                            _form.AddExecLog(line + "\r\n");
                            if (!string.IsNullOrEmpty(line))
                            {
                                pi.pUrl = pli.BaseUrl + line;
                                pli.Player.Add(pi);
                            }
                        }
                    }
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetMasterM3u8Async), Ex);
                pli.Error = Ex.Status.ToString();
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        pli.Error = ((int)errres.StatusCode).ToString();
                }
                return pli;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetMasterM3u8Async), Ex);
                pli.Error = Ex.ToString();
                return pli;
            }
            pli.Status = "Ok";
            pli.Error = "";
            return pli;
        }

        //player.m3u8からsegment情報を取得
        public async Task<SegmentInfo> GetPlayerM3u8Async(string url)
        {
            _form.AddExecLog("GetPlayerFile\r\n");
            var sgi = new SegmentInfo();
            sgi.Status = "Error";
            sgi.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return sgi;

            try
            {
                var idx = url.IndexOf("playlist.m3u8");
                if (idx >= 0) sgi.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    int sn;
                    double ei;
                    string line;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        _form.AddExecLog(line + "\r\n");
                        var ttt = line.Split(':');
                        switch (ttt[0])
                        {
                            case "#EXT-X-MEDIA-SEQUENCE":
                                if (int.TryParse(ttt[1], out sn))
                                    sgi.SeqNo = sn;
                                break;
                            case "#CURRENT-POSITION":
                                if (double.TryParse(ttt[1], out ei))
                                    sgi.Position = ei;
                                break;
                            case "#DMC-CURRENT-POSITION":
                                if (double.TryParse(ttt[1], out ei))
                                    sgi.Position = ei;
                                break;
                            case "#EXT-X-ENDLIST":
                                sgi.EndList = true;
                                break;
                            case "#EXTINF":
                                var sg = new Segment();
                                if (double.TryParse(ttt[1].Split(',')[0], out ei))
                                    sg.ExtInfo = ei;
                                line = sr.ReadLine();
                                _form.AddExecLog(line + "\r\n");
                                if (!string.IsNullOrEmpty(line))
                                {
                                    sg.sFile = line.Split('?')[0];
                                    sg.sUrl = sgi.BaseUrl + line;
                                    sgi.Seg.Add(sg);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetPlayerM3u8Async), Ex);
                sgi.Error = Ex.Status.ToString();
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        sgi.Error = ((int)errres.StatusCode).ToString();
                }
                return sgi;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetPlayerM3u8Async), Ex);
                sgi.Error = Ex.ToString();
                return sgi;
            }
            sgi.Status = "Ok";
            sgi.Error = "";
            return sgi;
        }

        //segmentファイルを取得
        public async Task<bool> GetSegmentAsync(Segment seg, string outfile, PlayListInfo pli, SegmentInfo sgi)
        {
            _form.AddExecLog("GetSegmentFile\r\n");
            byte[] data = null;
            if (string.IsNullOrEmpty(seg.sUrl)) return false;

            try
            {
                var file = outfile + "_" + seg.sFile;
                data = await _wc.DownloadDataTaskAsync(seg.sUrl);
                string ll = _wc.ResponseHeaders.Get("Content-Length");
                _form.AddExecLog("Input: " + seg.sUrl + "\r\n");
                _form.AddExecLog("SeqNo=" + sgi.SeqNo.ToString() + " Size: " + data.Length.ToString() + " Content-Length: " + ll + "\r\n");

                //データーをSqlite3に書き込み
                _nd.WriteDbMedia(seg, pli, sgi, data, data.Length, 0);

            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetSegmentAsync), Ex);
                int errno = 0;
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        errno = (int)errres.StatusCode;
                }
                _form.AddExecLog("GetSegment Error: " + Ex.Status.ToString() + " (" + errno + ")\r\n");
                return false;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetSegmentAsync), Ex);
                return false;
            }
            return true;
        }

        //速度変更
        public async Task<bool> SetPlayControlAsync(double speed, PlayListInfo pli)
        {
            _form.AddExecLog("SetPlayControlAsync\r\n");

            try
            {
                var ttt = pli.MasterUrl.Split('?')[1].Split('&').FirstOrDefault(s => s.StartsWith("ht2_nicolive="));
                var url = pli.BaseUrl + "play_control.json?" + ttt + "&play_speed=2";
                _form.AddExecLog(url + "\r\n");
                var str = await _wc.DownloadStringTaskAsync(url);
                var result = JObject.Parse(str);
                //{ "meta":{ "status":200,"message":"ok"},"data":{ "play_control":{ "play_speed":0.25} } }
                _form.AddExecLog(str + "\r\n");
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(SetPlayControlAsync), Ex);
                int errno = 0;
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        errno = (int)errres.StatusCode;
                }
                _form.AddExecLog("SetPlayControlAsync Error: " + Ex.Status.ToString() + " (" + errno + ")\r\n");
                return false;
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(SetPlayControlAsync), Ex);
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
                    _nd?.Dispose();
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
