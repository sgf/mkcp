using System;
using System.Collections.Generic;
using System.Net;

namespace mkcp {


    public delegate void NewSessionHandler(KcpSession session);



    public interface IKcpIO {
        void OnUdpReceive(Span<byte> data, IPEndPoint endPoint);
    }

    public class KcpSessionManager : IKcpIO {
        //private readonly ILogger? logger;
        private readonly Dictionary<uint, KcpSession> Clients = new Dictionary<uint, KcpSession>();
        private readonly HashSet<EndPoint> ConList = new HashSet<EndPoint>(1000);
        private readonly HashSet<EndPoint> BadList = new HashSet<EndPoint>(100);
        private readonly Queue<uint> SIDPool;

        private void PreGenerateSIDS(int maxUser = 1000) {
            Random random = new Random();
            List<uint> numrangs = new List<uint>(maxUser);
            for (uint i = 1; i <= maxUser; i++)
                numrangs.Add(i);
            for (int i = 1; i <= maxUser; i++) {
                var rdx = random.Next(0, maxUser - i);
                SIDPool.Enqueue(numrangs[rdx]);
                numrangs.RemoveAt(i);
            }
        }

        public KcpSessionManager(int maxUser = 1000) {
            SIDPool = new Queue<uint>(maxUser);
            PreGenerateSIDS(maxUser);
        }

        public event NewSessionHandler OnNewSession;

        public void AddBad(IPEndPoint endPoint) => BadList.Add(endPoint);
        public bool InBad(IPEndPoint endPoint) => BadList.Contains(endPoint);

        public KcpSession DetermineIsBadOrNewConnection(uint conv, IPEndPoint endPoint) {
            KcpSession session = null;
            //2 包合法IP端口存在（老连接的数据） 这里还需要KCP底层继续校验 例如：SN号是否合法还应该考虑客户端如果IP没变但是端口变了（这个可能需要兼容）
            //（这里暂时认为IP和端口号必须一致就OK，其他情况暂时认为都不合法）
            if (conv == 0)
                return CheckingNewOrOld(endPoint);
            else if (Clients.TryGetValue(conv, out session)) {//因为conv是uint 因此这里默认隐含 conv>0
                if (session.Peer == endPoint) //暂时要求端口也必须一致
                    return session;
                else {
                    //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                    AddBad(endPoint);
                    return null;
                }
            } else
                return null;
        }


        private KcpSession AddNewSession(IPEndPoint endPoint) {
            var sid = SIDPool.Dequeue();
            var news = KcpSession.CreateSvrSeesion(sid, endPoint, this);
            Clients.Add(sid, news);
            ConList.Add(endPoint);
            OnNewSession?.Invoke(news);
            return news;
        }

        private KcpSession CheckingNewOrOld(IPEndPoint endPoint) {
            //3 包合法IP端口不存在（检查ConV是否为0，为0为新连接，不为0拉黑）
            if (!ConList.Contains(endPoint)) {
                return AddNewSession(endPoint);
            } else {//老客户端新连接（要T下线 或者协商 让对方更新连接ID，继续服务，这个得看客户端超时时间了 这个要不要做有没有风险有待商榷，比如客户端正在搞协议分析或者破解什么的）
#warning 老客户端新连接（要T下线）

                return null;
            }
        }

        /// <summary>
        /// 移除Session
        /// </summary>
        /// <param name="session2remove"></param>
        public void Remove(KcpSession session) {
            if (Clients.ContainsKey(session.SID)) {
                Clients.Remove(session.SID);
                SIDPool.Enqueue(session.SID);
            }
        }

        public void OnUdpReceive(Span<byte> data, IPEndPoint endPoint) {
            //检查黑名单
            if (this.InBad(endPoint)) return;
            //检查格式
            var (isBad, conv) = KcpSession.IsBadFormat(data);
            if (isBad) { this.AddBad(endPoint); return; }

            var session = this.DetermineIsBadOrNewConnection(conv, endPoint);
            if (session == null) return;
            KcpSession.KCPInput(session, data);
        }
    }


    public class KcpClient : IKcpIO {



    }


}
