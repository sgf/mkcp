using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace mkcp {

    public class KcpSessionManager {
        //private readonly ILogger? logger;
        private readonly Dictionary<uint, KcpSession> Clients = new Dictionary<uint, KcpSession>();
        private readonly HashSet<EndPoint> BadList = new HashSet<EndPoint>(100);
        private readonly Queue<uint> SIDPool;
        private void GenerateSIDS(int maxUser = 1000) {
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
            GenerateSIDS(maxUser);
        }

        public (bool, KcpSession) DetermineBadOrNewConnection(Span<byte> pk, IPEndPoint endPoint) {
            if (BadList.Contains(endPoint)) return (true, null);//已经被拉黑

            if (pk.Length < Kcp.IKCP_OVERHEAD) { //包数据太小
                //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                BadList.Add(endPoint);
                return (true, null);
            }
            var dataSize = pk.Length - Kcp.IKCP_OVERHEAD;
            ref var segHead = ref pk.Read<Kcp.SegmentHead>();
            if (dataSize < segHead.len //Data数据太小
               || segHead.cmd < Kcp.IKCP_CMD_PUSH || segHead.cmd > Kcp.IKCP_CMD_WINS) { //cmd命令不存在
                //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                BadList.Add(endPoint);
                return (true, null);
            }

            if (Clients.TryGetValue(segHead.conv, out KcpSession existsSession) &&
                (existsSession.Peer.Address != endPoint.Address
                    || existsSession.Peer.Port != endPoint.Port)) { //暂时要求端口也必须一致
                //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                BadList.Add(endPoint);
                return (true, null);
            }

            var sid = SIDPool.Dequeue();
            existsSession ??= new KcpSession(sid, endPoint, this);
            Clients.Add(sid, existsSession);
            return (false, existsSession);
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


    }





}
