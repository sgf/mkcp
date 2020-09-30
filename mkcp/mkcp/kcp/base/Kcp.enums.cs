using System;
using System.Collections.Generic;
using System.Text;

namespace mkcp {
    public partial class Kcp {

        internal enum Cmd : byte {

            /// <summary>
            /// 空命令
            /// </summary>
            Default = 0,

            /// <summary>
            /// cmd: push data
            /// </summary>
            IKCP_CMD_PUSH = 81,
            /// <summary>
            /// cmd: ack
            /// </summary>
            IKCP_CMD_ACK = 82,
            /// <summary>
            /// cmd: window probe (ask) 询问对方当前剩余窗口大小 请求
            /// </summary>
            IKCP_CMD_WASK = 83,
            /// <summary>
            /// cmd: window size (tell) 返回本地当前剩余窗口大小
            /// </summary>
            IKCP_CMD_WINS = 84,
        }

        internal enum Porbe {

            /// <summary>
            /// 默认值(仅用于初始化占位)
            /// </summary>
            Default = 0,

            /// <summary>
            /// need to send IKCP_CMD_WASK
            /// </summary>
            IKCP_ASK_SEND = 1,
            /// <summary>
            /// need to send IKCP_CMD_WINS
            /// </summary>
            IKCP_ASK_TELL = 2
        }

        internal enum kLog {
            OUTPUT = 0x1,
            INPUT = 0x2,
            SEND = 0x4,
            RECV = 0x8,
            IN_DATA = 0x10,
            IN_ACK = 0x20,
            IN_PROBE = 0x40,
            IN_WINS = 0x80,
            OUT_DATA = 0x100,
            OUT_ACK = 0x200,
            OUT_PROBE = 0x400,
            OUT_WINS = 0x800,
        }

    }
}
