using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleHTTP;

namespace HTTPtest
{
    class Program
    {
        static void Main(string[] args)
        {
            HttpsClient httpsClient = new HttpsClient("cn.bing.com");
            var res = httpsClient.SendRequest("https://cn.bing.com/?mkt=zh-CN", RequestType.GET);
            Console.WriteLine(Encoding.ASCII.GetString(res));
            Console.ReadKey();
            return;
        }
    }
}
