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
            HttpClient httpClient = new HttpClient("images.goodsmile.info");
            var res = httpClient.SendRequest("http://images.goodsmile.info/cgm/images/product/20170529/6474/45673/large/77033d23c66d2612f66d14dc089cfe01.jpg", RequestType.GET);
            FileStream fileStream = new FileStream("test.jpg", FileMode.CreateNew);
            fileStream.Write(res, res.Length - 115154, 115154);
            fileStream.Close();
            Console.WriteLine(Encoding.ASCII.GetString(res));
            Console.ReadKey();
            return;
        }
    }
}
