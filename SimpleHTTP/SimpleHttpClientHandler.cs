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
        public int MaxThreadPerIp { get; set; }
        public int MaxThread { get; set; }

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
        protected async Task<HttpResponseMessage> SendInnerAsync(HttpRequestMessage request, CancellationToken cancellationToken,IPAddress ip)
        {
            var requests = GetRequestsCache(ip);
            var unbusyelement = requests.Where(a => !a.IsBusy).FirstOrDefault();
            HttpResponse httpResponse = null;
            if (unbusyelement != null)
            {
                httpResponse=await unbusyelement.SendRequestAsync(request.RequestUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped), request.Method).ConfigureAwait(false);
            }
            else if (requests.Count >= MaxThreadPerIp)
            {
                //wait
            }
            else
            {
                var ourrequest = CreateRequest(request.RequestUri, ip);
                httpResponse = await ourrequest.SendRequestAsync(request.RequestUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped), request.Method).ConfigureAwait(false);
                requests.Add(ourrequest);
            }
            return new HttpResponseMessage() { Content = new StreamContent(httpResponse.Content) };
        }
        protected SinpleHttpRequest CreateRequest(Uri uri,IPAddress ip)
        {
            return uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? new SinpleHttpsRequest(uri.Host, ip) : new SinpleHttpRequest(uri.Host, ip);
        }

        protected abstract List<SinpleHttpRequest> GetRequestsCache(IPAddress iPAddress);
    }
    internal class SimpleHttpClientHandlerThreadSafe : SimpleHttpClientHandler
    {
        protected ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>> dictionary = new ConcurrentDictionary<IPAddress, List<SinpleHttpRequest>>();
        protected override List<SinpleHttpRequest> GetRequestsCache(IPAddress iPAddress)
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
