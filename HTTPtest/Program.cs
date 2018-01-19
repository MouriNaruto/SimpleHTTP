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
        async static Task Main(string[] args)
        {
            SinpleHttpsRequest httpClient = new SinpleHttpsRequest("cn.bing.com");
            var res = httpClient.SendRequest("https://cn.bing.com/?mkt=zh-CN", System.Net.Http.HttpMethod.Get);
            Console.WriteLine(res.Header);
            if (File.Exists("test.html"))
                File.Delete("test.html");
            FileStream file = new FileStream("test.html", FileMode.CreateNew);
            file.Write(((MemoryStream)res.Content).ToArray(), 0, (int)res.Content.Length);
            file.Close();
            Console.ReadKey();
            return;
        }
    }
}
