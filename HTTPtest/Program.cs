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
            SimpleHttpClient httpClient = new SimpleHttpClient();
            httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            var res = await httpClient.GetStringAsync("https://cn.bing.com/?mkt=zh-CN");
            await httpClient.GetStringAsync("https://cn.bing.com/?mkt=zh-CN");
            Console.WriteLine(res);
            Console.WriteLine("waiting");
            await Task.Delay(2000);

            //if (File.Exists("test.html"))
            //    File.Delete("test.html");
            //FileStream file = new FileStream("test.html", FileMode.CreateNew);
            //file.Write(((MemoryStream)res.Content).ToArray(), 0, (int)res.Content.Length);
            //file.Close();
            Console.ReadKey();
            return;
        }
    }
}
