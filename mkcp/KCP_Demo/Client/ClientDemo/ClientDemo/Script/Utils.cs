using System;

public class Utils
{
    /* decode 32 bits unsigned int (lsb) */
    public static int ikcp_decode32u(byte[] p, int offset, ref UInt32 c)
    {
        UInt32 result = 0;
        result |= (UInt32)p[0 + offset];
        result |= (UInt32)(p[1 + offset] << 8);
        result |= (UInt32)(p[2 + offset] << 16);
        result |= (UInt32)(p[3 + offset] << 24);
        c = result;
        return 4;
    }
    public static int ikcp_decode32u(byte[] p, uint offset, ref UInt32 c)
    {
        UInt32 result = 0;
        result |= (UInt32)p[0 + offset];
        result |= (UInt32)(p[1 + offset] << 8);
        result |= (UInt32)(p[2 + offset] << 16);
        result |= (UInt32)(p[3 + offset] << 24);
        c = result;
        return 4;
    }
    public static int ikcp_encode32u(byte[] p, int offset, UInt32 l)
    {
        p[0 + offset] = (byte)(l >> 0);
        p[1 + offset] = (byte)(l >> 8);
        p[2 + offset] = (byte)(l >> 16);
        p[3 + offset] = (byte)(l >> 24);
        return 4;
    }
    /// <summary>
    /// 获取时间戳
    /// </summary>
    /// <returns></returns>
    public static long GetTimestamp()
    {
        return DateTime.Now.Ticks / 10000;
    }
    /// <summary>
    /// 获取时间戳
    /// </summary>
    /// <returns></returns>
    public static long GetDateTimeNowTicks()
    {
        return DateTime.Now.Ticks;
    }
    /// <summary>
    /// 获取时间对象
    /// </summary>
    /// <returns></returns>
    public static DateTime GetDateTime()
    {
        return DateTime.Now;
    }
   
}