using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

using Dwango.Nicolive.Chat.Service.Edge;

namespace NicoNamaRokuga.Message
{
    public class PackedServer
    {
        private readonly string uri;
        private readonly Dictionary<string, string> headers;
        private bool isDisconnect;
        private bool unexpectedDisconnect;
        //private readonly BinaryStream stream;
        private readonly List<byte> _buffer;
        private readonly CancellationTokenSource cancelSource;
        private NicoMessage nMsg;
        private Func<PackedSegment, Task> processData;
        private readonly object mu = new Object();

        private readonly HttpClient ClientPacked = new HttpClient();

        public PackedServer(string uri, string proxy, Func<PackedSegment, Task> processData, NicoMessage nicomessage)
        {
            this.uri = uri;
            this.processData = processData;
            this.nMsg = nicomessage;
            headers = new Dictionary<string, string>
            {
            //    { "header", "u=1, i" }
            };
            isDisconnect = false;
            unexpectedDisconnect = false;
            //stream = new BinaryStream();
            _buffer = new List<byte>();
            cancelSource = new CancellationTokenSource();

            var handler = new HttpClientHandler
            {
                Proxy = string.IsNullOrEmpty(proxy) ? null : new WebProxy(proxy),
                UseProxy = !string.IsNullOrEmpty(proxy),
                MaxConnectionsPerServer = 2
            };

            ClientPacked = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(25)
            };
        }

        public async Task<string> ConnectAsync()
        {
            string ret = string.Empty;
            var request = new HttpRequestMessage(HttpMethod.Get, this.uri);
            foreach (var kv in headers)
            {
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            try
            {
                using (var resp = await ClientPacked.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelSource.Token))
                {
                    if (resp.StatusCode != HttpStatusCode.OK)
                    {
                        ret = "Unexpected status code: " + (int)resp.StatusCode;
                        return ret;
                    }
                    var streamResp = await resp.Content.ReadAsStreamAsync();
                    var buffer = new byte[8192];
                    while (!cancelSource.Token.IsCancellationRequested)
                    {
                        if (nMsg.MessageStatus != 0)
                            cancelSource.Cancel();
                        int n = await streamResp.ReadAsync(buffer, 0, buffer.Length, cancelSource.Token);
                        if (n == 0) break;
                        var chunk = new byte[n];
                        Array.Copy(buffer, chunk, n);
                        _buffer.AddRange(chunk);
                        //await PackedData(chunk);
                    }
                }
                isDisconnect = true;

                if (nMsg.MessageStatus == 0)
                {
                    var segment = PackedSegment.Parser.ParseFrom(_buffer.ToArray());
                    if (!string.IsNullOrEmpty(segment.ToString()))
                        await processData(segment);
                }

                processData = null;
                return ret;
            }
            catch (OperationCanceledException)
            {
                ret = "Read operation was canceled due to a timeout or external cancellation.";
                Disconnect();
                processData = null;
                return ret;
            }
            catch (Exception Ex)
            {
                lock (mu) unexpectedDisconnect = true;
                ret = Ex.Message;
                Disconnect();
                processData = null;
                return ret;
            }
        }

        public bool Disconnect()
        {
            StopReceiving();
            isDisconnect = true;
            //Console.WriteLine("disconnect packed server.");
            return true;
        }

        public bool IsUnexpectedDisconnect()
        {
            lock (mu) return unexpectedDisconnect;
        }

        public bool IsDisconnect() => isDisconnect;

        private void StopReceiving() => cancelSource.Cancel();

        //private async Task PackedData(byte[] data)
        //{
            //stream.AddBuffer(stream, data);

            //foreach (var item in stream.Read(stream))
            //{
            //    try
            //    {
            //        // Google.Protobuf generated class
            //        var segment = PackedSegment.Parser.ParseFrom(item);
            //        if (!string.IsNullOrEmpty(segment.ToString()) && !isDisconnect)
            //        {
            //            await processData(segment);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex);
            //        continue;
            //    }
            //}

            //stream.ClearBuffer(stream);
        //}
    }

}
