﻿using System;
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
    /// 一些琐碎的静态方法
    /// </summary>
    public static class HttpHelper
    {
        /// <summary>
        /// 用于从请求里获取Hostname
        /// </summary>
        /// <param name="Content"></param>
        /// <returns></returns>
        public static string GetHostname(string Content)
        {
            var firstdslash = Content.IndexOf("//");
            if (firstdslash == -1) firstdslash = -2;
            string tmp0 = Content.Substring(firstdslash + 2);
            var firstslash = tmp0.IndexOf("/");
            if (firstslash == -1) firstslash = tmp0.Length;
            string tmp1 = tmp0.Substring(0, firstslash);
            var firstq = tmp1.IndexOf(":");
            if (firstq == -1) return tmp1;
            else return tmp1.Substring(0, firstq);
        }
    }

    public class HttpResponse
    {
        public string Header { get; internal set; }
        public byte[] Content { get; internal set; }
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

        #region 自定义
        /// <summary>
        /// 这是用于自定义的Header部分；
        /// 不要在里面包含Host和Content-Length；
        /// 确保以单\r\n结尾（或保持为空）。
        /// </summary>
        public string CustomizeHeader { get; set; }
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
        public HttpResponse SendRequest(string Content, RequestType RequestType, string PostContent = null)
        {
            HttpResponse response = new HttpResponse();
            byte[] toreturn = new byte[0];
            socket.Connect(IPAddress.Parse(HostIP), Hostport);
            socket.ReceiveTimeout = 500;
            var request = ((RequestType == RequestType.GET) ? "GET" : "POST") + " " + Content + " HTTP/1.1\r\n" +
                "Host: " + this.Hostname + "\r\n" +
                "Content-Length: " +
                ((RequestType == RequestType.GET) ? "0" :
                (PostContent == null ? "0" :
                PostContent.Length.ToString())) +
                "\r\n" + CustomizeHeader +
                "\r\n";
            if (RequestType == RequestType.POST)
                request += PostContent;
            var socketresponse = socket.Send(Encoding.UTF8.GetBytes(request));
            byte[] buffer = new byte[8192];
            int bytes;
            bool ishead = true;
            int contentlength = 0;
            do
            {
                try
                {
                    bytes = socket.Receive(buffer, 8192, SocketFlags.None);
                }
                catch
                {
                    break;
                }
                if (ishead)
                {
                    for (int i = 0; i < bytes - 3; i++)
                    {
                        //通过检查第一个双回车来判断分割点
                        if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                        {
                            ishead = false;
                            var tmp0 = toreturn.Clone() as byte[];
                            toreturn = new byte[tmp0.Length + i];
                            tmp0.CopyTo(toreturn, 0);
                            Array.Copy(buffer, 0, toreturn, tmp0.Length, i);
                            response.Header = Encoding.ASCII.GetString(toreturn);
#if DEBUG
                            Debug.WriteLine("头接受完成");
                            Debug.WriteLine(response.Header);
#endif
                            var lengthindex = response.Header.IndexOf("Content-Length: ");
                            if (lengthindex >= 0)
                            {
                                var tmpstring = response.Header.Substring(lengthindex + 16);
                                var lengthendindex = tmpstring.IndexOf("\r\n");
                                var contentlengthstr = tmpstring.Substring(0, lengthendindex);
                                contentlength = Convert.ToInt32(contentlengthstr);
#if DEBUG
                                Debug.WriteLine("预计的内容长度：" + contentlengthstr);
#endif
                            }
                            toreturn = new byte[bytes - i - 4];
                            Array.Copy(buffer, i + 4, toreturn, 0, bytes - i - 4);
                            break;
                        }
                    }
                    if (!ishead) continue;
                }
                var tmp = toreturn.Clone() as byte[];
                toreturn = new byte[tmp.Length + bytes];
                tmp.CopyTo(toreturn, 0);
                Array.Copy(buffer, 0, toreturn, tmp.Length, bytes);
            } while (bytes > 0 && toreturn.Length != contentlength);
#if DEBUG
            Debug.WriteLine("接受内容字节:" + toreturn.Length);
#endif
            if (!ishead) response.Content = toreturn;
            socket.Close();
            return response;
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

        #region 自定义
        /// <summary>
        /// 这是用于自定义的Header部分；
        /// 不要在里面包含Host和Content-Length；
        /// 确保以单\r\n结尾（或保持为空）。
        /// </summary>
        public string CustomizeHeader { get; set; }
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
        public HttpResponse SendRequest(string Content, RequestType RequestType, string PostContent = null)
        {
            HttpResponse response = new HttpResponse();
            byte[] toreturn = new byte[0];
            socket.Connect(IPAddress.Parse(HostIP), Hostport);
            var request = ((RequestType == RequestType.GET) ? "GET" : "POST") + " " + Content + " HTTP/1.1\r\n" +
                "Host: " + this.Hostname + "\r\n" +
                "Content-Length: " +
                ((RequestType == RequestType.GET) ? "0" :
                (PostContent == null ? "0" :
                PostContent.Length.ToString())) +
                "\r\n" + CustomizeHeader +
                "\r\n";
            if (RequestType == RequestType.POST)
                request += PostContent;
            using (NetworkStream networkStream = new NetworkStream(socket))
            {
                using (SslStream sslStream = new SslStream(networkStream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    sslStream.AuthenticateAsClient(this.Hostname);
                    sslStream.ReadTimeout = 500;
                    var a = Encoding.UTF8.GetBytes(request);
                    sslStream.Write(Encoding.UTF8.GetBytes(request));
                    byte[] buffer = new byte[8192];
                    int bytes;
                    bool ishead = true;
                    int contentlength = 0;
                    do
                    {
                        try
                        {
                            bytes = sslStream.Read(buffer, 0, 8192);
                        }
                        catch
                        {
                            break;
                        }
                        if (ishead)
                        {
                            for (int i = 0; i < bytes - 3; i++)
                            {
                                //通过检查第一个双回车来判断分割点
                                if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                                {
                                    ishead = false;
                                    var tmp0 = toreturn.Clone() as byte[];
                                    toreturn = new byte[tmp0.Length + i];
                                    tmp0.CopyTo(toreturn, 0);
                                    Array.Copy(buffer, 0, toreturn, tmp0.Length, i);
                                    response.Header = Encoding.ASCII.GetString(toreturn);
#if DEBUG
                                    Debug.WriteLine("头接受完成");
                                    Debug.WriteLine(response.Header);
#endif
                                    var lengthindex = response.Header.IndexOf("Content-Length: ");
                                    if (lengthindex >= 0)
                                    {
                                        var tmpstring = response.Header.Substring(lengthindex + 16);
                                        var lengthendindex = tmpstring.IndexOf("\r\n");
                                        var contentlengthstr = tmpstring.Substring(0, lengthendindex);
                                        contentlength = Convert.ToInt32(contentlengthstr);
#if DEBUG
                                        Debug.WriteLine("预计的内容长度：" + contentlengthstr);
#endif
                                    }
                                    toreturn = new byte[bytes - i - 4];
                                    Array.Copy(buffer, i + 4, toreturn, 0, bytes - i - 4);
                                    break;
                                }
                            }
                            if (!ishead) continue;
                        }
                        var tmp = toreturn.Clone() as byte[];
                        toreturn = new byte[tmp.Length + bytes];
                        tmp.CopyTo(toreturn, 0);
                        Array.Copy(buffer, 0, toreturn, tmp.Length, bytes);
                    } while (bytes > 0 && toreturn.Length != contentlength);
#if DEBUG
                    Debug.WriteLine("接受内容字节:" + toreturn.Length);
#endif
                    if (!ishead) response.Content = toreturn;
                }
            }
            socket.Close();
            return response;
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
