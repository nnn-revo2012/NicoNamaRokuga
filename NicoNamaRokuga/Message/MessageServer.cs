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
    public class MessageServer
    {
        private readonly string uri;
        private readonly Dictionary<string, string> headers;
        private string nextStreamAt;
        private string beforeNextStreamAt;
        private bool isDisconnect;
        private bool unexpectedDisconnect;
        private readonly BinaryStream stream;
        private readonly CancellationTokenSource cancelSource;
        private readonly ChannelWriter<ChunkedEntry> entryWriter;
        private readonly object mu = new Object();

        private static HttpClient ClientMessage = new HttpClient();

        public MessageServer(string uri, string proxy, ChannelWriter<ChunkedEntry> entryWriter)
        {
            this.uri = uri;
            this.entryWriter = entryWriter;
            headers = new Dictionary<string, string>
            {
                { "header", "u=1, i" }
            };
            nextStreamAt = "now";
            beforeNextStreamAt = string.Empty;
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

            ClientMessage = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
        }

        public async Task ConnectAsync()
        {
            string messageUri = $"{uri}?at={nextStreamAt}";
            var request = new HttpRequestMessage(HttpMethod.Get, messageUri);
            foreach (var kv in headers)
            {
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            try
            {
                var resp = await ClientMessage.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelSource.Token);
                if (resp.StatusCode != HttpStatusCode.OK)
                    throw new HttpRequestException($"unexpected status code: {(int)resp.StatusCode}");

                var streamResp = await resp.Content.ReadAsStreamAsync();
                var buffer = new byte[8192];
                while (!cancelSource.Token.IsCancellationRequested)
                {
                    int n = await streamResp.ReadAsync(buffer, 0, buffer.Length, cancelSource.Token);
                    if (n == 0) break; // EOF
                    var chunk = new byte[n];
                    Array.Copy(buffer, chunk, n);
                    await MessageData(chunk);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Read operation was canceled due to a timeout or external cancellation.");
            }
            catch (Exception)
            {
                lock (mu) unexpectedDisconnect = true;
                throw;
            }
        }

        public void Disconnect()
        {
            StopReceiving();
            isDisconnect = true;
            Console.WriteLine("disconnect message server.");
        }

        public bool IsUnexpectedDisconnect()
        {
            lock (mu) return unexpectedDisconnect;
        }

        public bool IsDisconnect() => isDisconnect;

        public string GetNextStreamAt() => nextStreamAt;

        public void SetNextStreamAt(string nextat)
        {
            if (!string.IsNullOrEmpty(nextat))
                nextStreamAt = nextat;
        }

        private void StopReceiving() => cancelSource.Cancel();

        private async Task MessageData(byte[] data)
        {
            stream.AddBuffer(stream, data);

            foreach (var item in stream.Read(stream))
            {
                try
                {
                    // Google.Protobuf generated class
                    var entry = ChunkedEntry.Parser.ParseFrom(item);
                    if (!string.IsNullOrEmpty(entry.ToString()) && !isDisconnect)
                    {
                        await entryWriter.WriteAsync(entry);
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
