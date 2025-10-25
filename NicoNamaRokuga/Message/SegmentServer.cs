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
    public class SegmentServer
    {
        private readonly string uri;
        private readonly Dictionary<string, string> headers;
        private bool isDisconnect;
        private bool unexpectedDisconnect;
        private readonly BinaryStream stream;
        private readonly CancellationTokenSource cancelSource;
        private NicoMessage nMsg;
        private readonly string serverName;
        private Func<ChunkedMessage, Task> processData;
        private readonly object mu = new Object();

        private readonly HttpClient ClientSegment = new HttpClient();

        public SegmentServer(string uri, string proxy, string servername, Func<ChunkedMessage, Task> processData, NicoMessage nicomessage)
        {
            this.uri = uri;
            this.processData = processData;
            this.serverName = servername;
            this.nMsg = nicomessage;
            headers = new Dictionary<string, string>
            {
            //    { "header", "u=1, i" }
            };
            isDisconnect = false;
            unexpectedDisconnect = false;
            stream = new BinaryStream();
            cancelSource = new CancellationTokenSource();

            var handler = new HttpClientHandler
            {
                Proxy = string.IsNullOrEmpty(proxy) ? null : new WebProxy(proxy),
                UseProxy = !string.IsNullOrEmpty(proxy),
                MaxConnectionsPerServer = 2
            };

            ClientSegment = new HttpClient(handler)
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
                using (var resp = await ClientSegment.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelSource.Token))
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
                        await SegmentData(chunk);
                    }
                }
                isDisconnect = true;
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
            //Console.WriteLine("disconnect segment server.");
            return true;
        }

        public bool IsUnexpectedDisconnect()
        {
            lock (mu) return unexpectedDisconnect;
        }

        public bool IsDisconnect() => isDisconnect;

        private void StopReceiving() => cancelSource.Cancel();

        private async Task SegmentData(byte[] data)
        {
            stream.AddBuffer(stream, data);

            foreach (var item in stream.Read(stream))
            {
                try
                {
                    // Google.Protobuf generated class
                    var message = ChunkedMessage.Parser.ParseFrom(item);
                    if (!string.IsNullOrEmpty(message.ToString()) && !isDisconnect)
                    {
                        await processData(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    continue;
                }
            }

            stream.ClearBuffer(stream);
        }
    }

}
