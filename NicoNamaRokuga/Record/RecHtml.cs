using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;

using NicoNamaRokuga.Net;
using NicoNamaRokuga.Prop;

namespace NicoNamaRokuga.Rec
{
    public class PlayListInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }
        public string BaseUrl { set; get; }
        public string MasterUrl { set; get; }
        public ICollection<PlayerInfo> Player { private set; get; }
        public string NextTime { set; get; }
        public string Format { set; get; }
        public string WithoutFormat { set; get; }
        public int SeqNo { set; get; }
        public float Position { set; get; }

        public PlayListInfo()
        {
            this.Status = "";
            this.Error = "";
            this.Player = new List<PlayerInfo>();
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
        public ICollection<Segment> Seg { private set; get; }
        public string NextTime { set; get; }
        public string Format { set; get; }
        public string WithoutFormat { set; get; }
        public float Position { set; get; }

        public SegmentInfo()
        {
            this.Status = "";
            this.Error = "";
            this.Seg = new List<Segment>();
        }
    }

    public class Segment
    {
        public int SeqNo { set; get; }
        public float ExtInfo { set; get; }
        public string sUrl { set; get; }
    }

    public class RecHtml : IDisposable
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

        public volatile int PsStatus = -1; //実行ファイルの状態
        private Process _ps = null;

        private NicoNetComment _nNetComment = null;   //WebSocket(Comment)
        private BroadCastInfo _bci = null;
        private Form1 _form = null;

        public RecHtml(Form1 fo, BroadCastInfo bci, NicoNetComment nNetComment)
        {
            IsDebug = false;

            PsStatus = -1;
            this._nNetComment = nNetComment;
            this._bci = bci;
            this._form = fo;

            var wc = new WebClientEx();
            _wc = wc;

            _wc.Encoding = Encoding.UTF8;
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);

        }

        ~RecHtml()
        {
            this.Dispose();
        }

        public void ExecPs(string masterfile, string outfile)
        {

            // masterファイルをGet
            var pli = GetMasterM3u8Async(masterfile, "");

            // playerファイルをGet

            // 指定秒ごとにSegmentファイルを取得

            // ループ

        }

        //master.m3u8からplayer.m3u8のURLを取得
        public async Task<PlayListInfo> GetMasterM3u8Async(string url, string referer)
        {
            var pli = new PlayListInfo();
            pli.Status = "Error";
            pli.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return pli;

            try
            {
                var idx = url.IndexOf("master.m3u8");
                if (idx >= 0) pli.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        if (line.IndexOf("#EXT-X-STREAM-INF") >= 0)
                        {
                            var pi = new PlayerInfo();
                            int bw;
                            if (int.TryParse(Regex.Match(line, @"BANDWIDTH=(\d+)").Groups[1].Value, out bw))
                                pi.Bandwidth = bw;
                            pi.pUrl = pli.BaseUrl + line;
                            pli.Player.Add(pi);
                        }
                    }
                }
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
        public async Task<PlayListInfo> GetPlayerM3u8Async(string url, string referer)
        {
            var pli = new PlayListInfo();
            pli.Status = "Error";
            pli.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return pli;

            try
            {
                var idx = url.IndexOf("player.m3u8");
                if (idx >= 0) pli.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        var pi = new PlayerInfo();
                        int bw;
                        if (int.TryParse(Regex.Match(line, @"BANDWIDTH=(\d+)").Groups[1].Value, out bw))
                            pi.Bandwidth = bw;
                        pi.pUrl = pli.BaseUrl + line;
                        pli.Player.Add(pi);
                    }
                }
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetPlayerM3u8Async), Ex);
                pli.Error = Ex.ToString();
                return pli;
            }
            pli.Status = "Ok";
            pli.Error = "";
            return pli;
        }

        //segmentファイルを取得
        public async Task<string> GetSegmentAsync(string url, string referer)
        {
            var result = string.Empty;
            if (string.IsNullOrEmpty(url)) return result;

            try
            {
                var idx = url.IndexOf("player.m3u8");
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
            }
            catch (Exception Ex) //タイムアウトなど
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetSegmentAsync), Ex);
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
