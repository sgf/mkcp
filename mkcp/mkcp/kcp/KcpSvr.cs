using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace mkcp {

    public class KcpSvr {
        private KcpSvr(IPEndPoint svrIpPort, bool autoOnService = true) {
            socket = KcpSocket.CreateSvr(svrIpPort);
            AutoOnService = autoOnService;//自动启动就是什么也不做就接收消息了。（否则，在接收到消息时 应该果断丢弃，这样客户端发过来的消息就得不到反馈，就意味着服务没有开启）

        }


        internal readonly KcpSocket socket;
        private readonly bool AutoOnService;

        public KcpSvr Start(string svrIpPort, bool autoOnService = true) {
            //转换IP地址和端口的操作，应该放在服务器启动的时候就暴漏出来（抛出异常）
            if (!IPEndPoint.TryParse(svrIpPort, out IPEndPoint ifport))
                throw new FormatException("IP以及端口格式有问题，请检查");
            return new KcpSvr(ifport, autoOnService);
        }

        private KcpSessionManager SessionManager = new KcpSessionManager();

    }
}
