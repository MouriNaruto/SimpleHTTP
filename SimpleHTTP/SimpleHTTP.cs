using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace SimpleHTTP
{
    public enum RequestType
    {
        GET,
        POST
    }

    /// <summary>
    /// 用于Http（非Https）访问
    /// </summary>
    public class HttpClient
    {
        #region 目标信息
        public string Hostname { get; private set; }
        public string HostIP { get; private set; }
        public int Hostport { get; set; } = 80;
        #endregion

        #region 私有字段
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        #endregion

        #region 构造函数
        /// <summary>
        /// 透过自定义IP构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        /// <param name="HostIP">目标IP地址</param>
        public HttpClient(string Hostname, string HostIP)
        {
            this.Hostname = Hostname;
            this.HostIP = HostIP;
        }

        /// <summary>
        /// 透过DNS构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        public HttpClient(string Hostname)
        {
            this.Hostname = Hostname;
            this.HostIP = Dns.GetHostEntry(Hostname).AddressList[0].ToString();
        }
        #endregion

        /// <summary>
        /// 用于发送Http请求
        /// </summary>
        /// <param name="Content">内容</param>
        /// <param name="RequestType">请求类型</param>
        /// <returns>请求回应</returns>
        public byte[] SendRequest(string Content, RequestType RequestType)
        {
            byte[] toreturn = new byte[0];
            socket.Connect(IPAddress.Parse(HostIP), Hostport);
            socket.ReceiveTimeout = 500;
            var request = ((RequestType == RequestType.GET) ? "GET" : "POST") + " " + Content + " HTTP/1.1\r\n" +
                "Host: " + this.Hostname + "\r\n" +
                "Content-Length: 0\r\n" +
                "\r\n";
            var response = socket.Send(Encoding.UTF8.GetBytes(request));
            byte[] buffer = new byte[8192];
            int bytes;
            do
            {
                try
                {
                    bytes = socket.Receive(buffer, 8192, SocketFlags.None);
                    var tmp = toreturn.Clone() as byte[];
                    toreturn = new byte[tmp.Length + bytes];
                    tmp.CopyTo(toreturn, 0);
                    Array.Copy(buffer, 0, toreturn, tmp.Length, bytes);
#if DEBUG
                    Debug.WriteLine("接受字节:" + toreturn.Length);
#endif
                }
                catch
                {
                    break;
                }
            } while (bytes > 0);
            socket.Close();
            return toreturn;
        }
    }

    /// <summary>
    /// 用于Https访问
    /// </summary>
    public class HttpsClient
    {
        #region 目标信息
        public string Hostname { get; private set; }
        public string HostIP { get; private set; }
        public int Hostport { get; set; } = 443;
        #endregion

        #region 私有字段
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        #endregion

        #region 构造函数
        /// <summary>
        /// 透过自定义IP构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        /// <param name="HostIP">目标IP地址</param>
        public HttpsClient(string Hostname, string HostIP)
        {
            this.Hostname = Hostname;
            this.HostIP = HostIP;
        }

        /// <summary>
        /// 透过DNS构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        public HttpsClient(string Hostname)
        {
            this.Hostname = Hostname;
            this.HostIP = Dns.GetHostEntry(Hostname).AddressList[0].ToString();
        }
        #endregion

        /// <summary>
        /// 用于发送Https请求
        /// </summary>
        /// <param name="Content">内容</param>
        /// <param name="RequestType">请求类型</param>
        /// <returns>请求回应</returns>
        public byte[] SendRequest(string Content, RequestType RequestType)
        {
            byte[] toreturn = new byte[0];
            socket.Connect(IPAddress.Parse(HostIP), Hostport);
            var request = ((RequestType == RequestType.GET) ? "GET" : "POST") + " " + Content + " HTTP/1.1\r\n" +
                "Host: " + this.Hostname + "\r\n" +
                "Content-Length: 0\r\n" +
                "\r\n";
            using (NetworkStream networkStream = new NetworkStream(socket))
            {
                using (SslStream sslStream = new SslStream(networkStream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    sslStream.AuthenticateAsClient(this.Hostname);
                    sslStream.ReadTimeout = 500;
                    sslStream.Write(Encoding.UTF8.GetBytes(request));
                    byte[] buffer = new byte[8192];
                    int bytes;
                    do
                    {
                        try
                        {
                            bytes = sslStream.Read(buffer, 0, 8192);
                            var tmp = toreturn.Clone() as byte[];
                            toreturn = new byte[tmp.Length + bytes];
                            tmp.CopyTo(toreturn, 0);
                            Array.Copy(buffer, 0, toreturn, tmp.Length, bytes);
#if DEBUG
                            Debug.WriteLine("接受字节:" + toreturn.Length);
#endif
                        }
                        catch
                        {
                            break;
                        }
                    } while (bytes > 0);
                }
            }
            socket.Close();
            return toreturn;
        }

        //证书校验，使用.NET提供的手段
        static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            X509Certificate2 x509Certificate2 = new X509Certificate2(certificate);
            chain.Build(x509Certificate2);
            return x509Certificate2.Verify();
        }
    }
}
