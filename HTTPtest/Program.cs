using System;
using System.Collections.Generic;
using System.IO;
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
            HttpsClient httpClient = new HttpsClient("cn.bing.com");
            var res = httpClient.SendRequest("https://cn.bing.com/?mkt=zh-CN", RequestType.GET);
            Console.WriteLine(res.Header);
            FileStream file = new FileStream("test.html", FileMode.CreateNew);
            file.Write(res.Content, 0, res.Content.Length);
            file.Close();
            Console.ReadKey();
            return;
        }
    }
}
