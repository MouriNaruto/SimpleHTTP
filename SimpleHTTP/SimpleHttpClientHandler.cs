using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using System.Linq;

namespace SimpleHTTP
{
    public abstract class SimpleHttpClientHandler : HttpMessageHandler
    {
        public int MaxThreadPerIp { get; set; } = 10;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            switch (request.RequestUri.HostNameType)
            {
                case UriHostNameType.Basic:
                case UriHostNameType.Dns:
                    return await SendInnerAsync(request, cancellationToken, (await Dns.GetHostAddressesAsync(request.RequestUri.DnsSafeHost).ConfigureAwait(false)).First()).ConfigureAwait(false);
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    return await SendInnerAsync(request, cancellationToken, IPAddress.Parse(request.RequestUri.Host)).ConfigureAwait(false);
                case UriHostNameType.Unknown:
                default:
                    throw new NotSupportedException(nameof(UriHostNameType) + "  " + request.RequestUri);
            }
        }
        protected ConcurrentQueue<Action> concurrentQueue = new ConcurrentQueue<Action>();
        protected async Task<HttpResponseMessage> SendInnerAsync(HttpRequestMessage request, CancellationToken cancellationToken,IPAddress ip)
        {
            var requests = GetRequestsCache(ip);
            var unbusyelement = requests.Where(a => !a.IsBusy).FirstOrDefault();
            HttpResponse httpResponse = null;
            async Task getcontent(SinpleHttpRequest thisrequest)
            {
                var requestcontent = request.Content == null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync();
                StringBuilder sb = new StringBuilder();
                IEnumerable<KeyValuePair<string, IEnumerable<string>>> header = request.Headers;
                //if (/*!request.Headers.Contains("Host")*/true)
                //{
                //    header = header.Concat(Enumerable.Repeat(new KeyValuePair<string, IEnumerable<string>>("Host", Enumerable.Repeat(request.RequestUri.Host, 1)), 1));
                //}
                //if (/*!request.Headers.Contains("Content-Length")*/true)
                //{
                //    header = header.Concat(Enumerable.Repeat(new KeyValuePair<string, IEnumerable<string>>("Content-Length",
                //        Enumerable.Repeat(requestcontent.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),1)),1));
                //}
                foreach (var one in header)
                {
                    foreach(var two in one.Value)
                    {
                        sb.Append(one.Key);
                        sb.Append(": ");
                        sb.Append(two);
                        sb.Append("\r\n");
                    }
                }
                thisrequest.CustomizeHeader = sb.ToString();
                httpResponse = await thisrequest.SendRequestAsync(request.RequestUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped), request.Method,requestcontent);
                _ = CheckQueueAsync();
            }
            if (unbusyelement != null)
            {
                await getcontent(unbusyelement);
            }
            else if (requests.Count >= MaxThreadPerIp)
            {
                //wait
                TaskCompletionSource<HttpResponseMessage> taskCompletionSource = new TaskCompletionSource<HttpResponseMessage>();
                concurrentQueue.Enqueue(async() =>
                {
                    taskCompletionSource.SetResult(await SendInnerAsync(request, cancellationToken, ip));
                });
                return await taskCompletionSource.Task;
            }
            else
            {
                SinpleHttpRequest ourrequest;
                lock (requests)
                {
                    ourrequest = CreateRequest(request.RequestUri, ip);
                    requests.Add(ourrequest);
                }
                await getcontent(ourrequest);
            }
            return new HttpResponseMessage() { Content = new StreamContent(httpResponse.Content) };
        }
        int doing = 0;
        protected async Task CheckQueueAsync()
        {
            if(Interlocked.CompareExchange(ref doing, 1, 0) == 1)
            {
                return;
            }
            while (!concurrentQueue.IsEmpty)
            {
                if(concurrentQueue.TryDequeue(out var value))
                {
                    value();
                }
                await Task.Yield();
            }
            Interlocked.Exchange(ref doing, 0);
        }
        internal SinpleHttpRequest CreateRequest(Uri uri,IPAddress ip)
        {
            return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? new SinpleHttpsRequest(uri.Host, ip) : new SinpleHttpRequest(uri.Host, ip);
        }

        internal abstract List<SinpleHttpRequest> GetRequestsCache(IPAddress iPAddress);
    }
    internal class SimpleHttpClientHandlerThreadSafe : SimpleHttpClientHandler
    {
        protected ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>> dictionary = new ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>>();
        internal override List<SinpleHttpRequest> GetRequestsCache(IPAddress iPAddress)
        {
            if (dictionary.TryGetValue(iPAddress, out var value))
            {
                return value;
            }
            else
            {
                var list = new List<SinpleHttpRequest>();
                if (dictionary.TryAdd(iPAddress, list))
                    return list;
                else
                    return GetRequestsCache(iPAddress);
            }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
