using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleHTTP
{
    public class SimpleHttpClient : System.Net.Http.HttpClient
    {
        static SimpleHttpClientHandler Instance { get; } = new SimpleHttpClientHandlerThreadSafe();
        public SimpleHttpClient() : base(Instance)
        {

        }
        public SimpleHttpClient(SimpleHttpClientHandler handler) : base(handler)
        {

        }
    }
}
