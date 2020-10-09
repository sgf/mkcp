namespace mkcp {
    public partial class Kcp {

        //暂时用不上的
        /// <summary>
        /// 貌似涉及到这这个变量的代码目前已经被废弃，或者被优化掉了。（已经变成了默认行为）
        /// https://github.com/ruleless/cill/blob/3b9d85a80d12fdffd24ef846696b8916bda9470f/asyncnet/src/inetkcp.c#L424
        /// </summary>
        //public static int IKCP_ACK_FAST => 3;

        ///// <summary>
        ///// 发送队列snd_queue中的Segment数量
        ///// </summary>
        //uint nsnd_que_ => (uint)snd_queue_.Count;


        //改变量控制的部分已经全部改为 true的情况的代码，即默认都是nodelay的情况，因此弃用该变量
        /// <summary>
        /// 是否启动无延迟模式。无延迟模式rtomin将设置为0，拥塞控制不启动；
        /// </summary>
        //bool nodelay_ { get; set; } = false;
    }
}
