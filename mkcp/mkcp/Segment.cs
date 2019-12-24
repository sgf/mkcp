namespace mkcp {


    struct Segment {
        struct IQUEUEHEAD node; // 节点用来串接多个 KCP segment，也就是前向后向指针；

    // 通用链表实现的队列.
    // node是一个通用链表，用于管理Segment队列，
    // 通用链表可以支持在不同类型的链表中做转移，
    // 通用链表实际上管理的就是一个最小的链表节点，
    // 具体该链表节点所在的数据块可以通过该数据块在链表中的位置反向解析出来。见 iqueue_entry 宏

    IUINT32 conv;     // Conversation, 会话序号: 接收到的数据包与发送的一致才接收此数据包
        IUINT32 cmd;      // Command, 指令类型: 代表这个Segment的类型
        IUINT32 frg;      // Fragment 记录了分片时的倒序序号, 当输出数据大于 MSS 时，需要将数据进行分片；
        IUINT32 wnd;      // Window, 己方的可用窗口大小, 也就是 rcv_queue 的可用大小即 rcv_wnd - nrcv_que, 见 ikcp_wnd_unused 函数
        IUINT32 ts;       // Timestamp, 记录了发送时的时间戳，用来估计 RTT
        IUINT32 sn;       // Sequence Number, Segment序号
        IUINT32 una;      // Unacknowledged, 当前未收到的序号: 即代表这个序号之前的包均收到
        IUINT32 len;      // Length, 数据长度
        IUINT32 resendts;   // 即 resend timestamp, 指定重发的时间戳，当当前时间超过这个时间时，则再重发一次这个包。
        IUINT32 rto;            // 即 Retransmit Timeout, 用于记录超时重传的时间间隔
        IUINT32 fastack;    // 记录ack跳过的次数，用于快速重传, 由函数 ikcp_parse_fastack 更新
        IUINT32 xmit;           // 记录发送的次数
        char data[1];           // 应用层要发送出去的数据
    };
}
