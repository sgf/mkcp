using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets.Kcp;
using System.Text;
using System.Threading.Tasks;


public class Handle : IKcpCallback
{
    //public void Output(ReadOnlySpan<byte> buffer)
    //{
    //    var frag = new byte[buffer.Length];
    //    buffer.CopyTo(frag);
    //    Out(frag);
    //}

    public Action<Memory<byte>> Out;
    public Action<byte[]> Recv;
    public void Receive(byte[] buffer)
    {
        Recv(buffer);
    }

    public IMemoryOwner<byte> RentBuffer(int lenght)
    {
        return null;
    }

    public void Output(IMemoryOwner<byte> buffer, int avalidLength)
    {
        using (buffer)
        {
            Out(buffer.Memory.Slice(0, avalidLength));
        }
    }
}

