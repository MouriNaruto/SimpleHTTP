using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SimpleHTTP
{
    public class HostsConfig
    {
        internal HostsConfig(Dictionary<string, IPAddress> dictionary)
        {
            inner_dic = dictionary;
        }
        public HostsConfig()
        {
            inner_dic = new Dictionary<string, IPAddress>();
        }
        Dictionary<string, IPAddress> inner_dic;
        public IDictionary<string, IPAddress> Map
        {
            get => inner_dic;
        }
    }
}
