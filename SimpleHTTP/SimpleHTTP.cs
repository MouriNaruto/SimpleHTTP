using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SimpleHTTP
{

    public class HttpResponse
    {
        public string Header { get; internal set; }
        public System.IO.Stream Content { get; internal set; }
    }

    /// <summary>
    /// 用于Http（非Https）访问
    /// </summary>
    public class SinpleHttpRequest:IDisposable
    {
        #region 目标信息
        public string Hostname { get; private set; }
        public IPAddress HostIP { get; private set; }
        public int Hostport { get; set; } = 80;
        public bool IsBusy { get; private set; } = false;
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
        protected TcpClient socket = new TcpClient();
        #endregion

        #region 构造函数
        /// <summary>
        /// 透过自定义IP构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        /// <param name="HostIP">目标IP地址</param>
        public SinpleHttpRequest(string Hostname, IPAddress HostIP)
        {
            this.Hostname = Hostname;
            this.HostIP = HostIP;
        }

        /// <summary>
        /// 透过DNS构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        public SinpleHttpRequest(string Hostname)
        {
            this.Hostname = Hostname;
            this.HostIP = Dns.GetHostEntry(Hostname).AddressList[0];
        }
        #endregion

        /// <summary>
        /// 用于发送Http请求
        /// </summary>
        /// <param name="Content">内容</param>
        /// <param name="RequestType">请求类型</param>
        /// <returns>请求回应</returns>
        public async Task<HttpResponse> SendRequestAsync(string Content, System.Net.Http.HttpMethod RequestType, byte[] PostContent = null)
        {
            IsBusy = true;
            try
            {
                return await Sendrequestinner(Content, RequestType, PostContent??Array.Empty<byte>());
            }
            finally
            {
                IsBusy = false;
            }
            
        }
        protected async Task SendMessageAsync(System.IO.Stream networkStream, HttpResponse response, System.Collections.Generic.IEnumerable<byte[]> message,
            string Content, System.Net.Http.HttpMethod RequestType, byte[] PostContent)
        {
            foreach (var one in message)
            {
                await networkStream.WriteAsync(one, 0, one.Length);
            }
            System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
            byte[] buffer = new byte[8192];
            int bytes;
            bool ishead = true;
            int contentlength = 0;
            do
            {
                try
                {
                    bytes = await networkStream.ReadAsync(buffer, 0, 8192);
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

                            response.Header = Encoding.UTF8.GetString(buffer, 0, i);
#if DEBUG
                            Debug.WriteLine("头接受完成");
                            Debug.WriteLine(response.Header);
#endif
                            var lengthindex = response.Header.IndexOf("Content-Length: ");
                            if (lengthindex >= 0)
                            {
                                var tmpstring = response.Header.Substring(lengthindex + 16);
                                var lengthendindex = tmpstring.IndexOf("\r\n");
                                if (lengthendindex > -1)
                                {
                                    var contentlengthstr = tmpstring.Substring(0, lengthendindex);
                                    contentlength = Convert.ToInt32(contentlengthstr);
#if DEBUG
                                    Debug.WriteLine("预计的内容长度：" + contentlengthstr);
#endif
                                }
                            }
                            await memoryStream.WriteAsync(buffer, i + 4, bytes - i - 4);
                            break;
                        }
                    }
                    if (!ishead) continue;
                }
                await memoryStream.WriteAsync(buffer, 0, bytes);
            } while (bytes > 0 && memoryStream.Length != contentlength);
#if DEBUG
            Debug.WriteLine("接受内容字节:" + memoryStream.Length);
#endif
            memoryStream.Seek(0, System.IO.SeekOrigin.Begin);

            if (!ishead) response.Content = memoryStream;
        }

        protected virtual async Task<HttpResponse> Sendrequestinner(string Content, System.Net.Http.HttpMethod RequestType, byte[] PostContent)
        {
            await ConnectAsync();
            SEND(Content, RequestType, PostContent, out HttpResponse response, out System.Collections.Generic.IEnumerable<byte[]> message);
            await SendMessageAsync(socket.GetStream(), response, message, Content, RequestType, PostContent);
            return response;
        }

        protected async Task ConnectAsync()
        {
            socket.ReceiveTimeout = 5000;
            //await Task.Factory.FromAsync((a, b) => socket.BeginConnect(HostIP, Hostport, a, b), socket.EndConnect, null).ConfigureAwait(false);
            await socket.ConnectAsync(HostIP, Hostport);
        }
        protected void SEND(string Content, System.Net.Http.HttpMethod RequestType, byte[] PostContent, out HttpResponse response, out System.Collections.Generic.IEnumerable<byte[]> message)
        {
            response = new HttpResponse();
            var request = (RequestType.Method + " " + Content + " HTTP/1.1\r\n" +
                "Host: " + this.Hostname + "\r\n" +
                "Content-Length: " +
                PostContent.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "\r\n" + CustomizeHeader +
                "\r\n");
            message = new byte[][] { Encoding.UTF8.GetBytes(request), PostContent };
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // 释放托管状态(托管对象)。
                    socket.Close();
                }

                // 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        ~SinpleHttpRequest()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(false);
        }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // 如果在以上内容中替代了终结器，则取消注释以下行。
            GC.SuppressFinalize(this);
        }
        #endregion

    }

    /// <summary>
    /// 用于Https访问
    /// </summary>
    public class SinpleHttpsRequest: SinpleHttpRequest
    {

        #region 构造函数
        /// <summary>
        /// 透过自定义IP构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        /// <param name="HostIP">目标IP地址</param>
        public SinpleHttpsRequest(string Hostname, IPAddress HostIP):base(Hostname,HostIP)
        {
            Init();
        }

        /// <summary>
        /// 透过DNS构造客户端
        /// </summary>
        /// <param name="Hostname">目标主机名</param>
        public SinpleHttpsRequest(string Hostname):base(Hostname)
        {
            Init();
        }
        private void Init()
        {
            Hostport = 443;
        }
        #endregion

        /// <summary>
        /// 用于发送Https请求
        /// </summary>
        /// <param name="Content">内容</param>
        /// <param name="RequestType">请求类型</param>
        /// <returns>请求回应</returns>
        protected override async Task<HttpResponse> Sendrequestinner(string Content, System.Net.Http.HttpMethod RequestType, byte[] PostContent = null)
        {
            await ConnectAsync();//.ConfigureAwait(false);
            //socket.ReceiveTimeout = 500;
            //await Task.Factory.FromAsync((a, b) => socket.BeginConnect(HostIP, Hostport, a, b), socket.EndConnect, null).ConfigureAwait(false);
            //socket.Connect(HostIP, Hostport);
            SEND(Content, RequestType, PostContent, out var response, out var message);
            using (SslStream sslStream = new SslStream(socket.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
            {
                sslStream.AuthenticateAsClient(this.Hostname);
                sslStream.ReadTimeout = 5000;
                await SendMessageAsync(sslStream, response, message, Content, RequestType, PostContent);
            }
            //socket.Disconnect(true);
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
