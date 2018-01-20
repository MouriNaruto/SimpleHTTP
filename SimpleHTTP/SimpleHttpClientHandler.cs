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
        public virtual async Task<IPAddress> GetIPFromHostAsync(string host)
        {
            return (await Dns.GetHostAddressesAsync(host).ConfigureAwait(false)).First();
        }
        public abstract HostsConfig GetHostsConfig();
        public abstract void LoadHostsConfig(HostsConfig hostsConfig);

        public int MaxThreadPerIp { get; set; } = 10;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            switch (request.RequestUri.HostNameType)
            {
                case UriHostNameType.Basic:
                case UriHostNameType.Dns:
                    return await SendInnerAsync(request, cancellationToken, await GetIPFromHostAsync(request.RequestUri.DnsSafeHost)).ConfigureAwait(false);
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
                cancellationToken.ThrowIfCancellationRequested();
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
                try
                {
                    thisrequest.CancellationToken = cancellationToken;
                    httpResponse = await thisrequest.SendRequestAsync(request.RequestUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped), request.Method, requestcontent);
                }
                catch (OperationCanceledException)
                {
                    thisrequest.Dispose();
                    throw;
                }
                finally
                {
                    thisrequest.CancellationToken = CancellationToken.None;
                }
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
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        taskCompletionSource.SetResult(await SendInnerAsync(request, cancellationToken, ip));
                    }
                    catch(Exception e)
                    {
                        taskCompletionSource.SetException(e);
                    }
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
        internal ConcurrentDictionary<string, Task<IPAddress>> hostsdic = new ConcurrentDictionary<string, Task<IPAddress>>();
        protected ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>> dictionary = new ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>>();
        internal override List<SinpleHttpRequest> GetRequestsCache(IPAddress iPAddress)
        {
            List<SinpleHttpRequest> parse(List<SinpleHttpRequest> list)
            {
                list.RemoveAll(a => a.IsDisposed);
                return list;
            }
            if (dictionary.TryGetValue(iPAddress, out var value))
            {
                return parse(value);
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

        public override Task<IPAddress> GetIPFromHostAsync(string host)
        {
            if(hostsdic.TryGetValue(host,out var ip))
            {
                return ip;
            }
            else
            {
                return base.GetIPFromHostAsync(host);
            }
        }

        public override HostsConfig GetHostsConfig()
        {
            return new HostsConfig(hostsdic.ToDictionary(a => a.Key, a => a.Value.Result));
        }

        public override void LoadHostsConfig(HostsConfig hostsConfig)
        {
            hostsdic = new ConcurrentDictionary<string, Task<IPAddress>>(hostsConfig.Map.Select(a => new KeyValuePair<string, Task<IPAddress>>(a.Key, Task.FromResult(a.Value))));
        }
    }
}
