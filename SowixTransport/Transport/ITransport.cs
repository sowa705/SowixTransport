using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SowixTransport
{
    public interface ITransport
    {
        bool IsReliable();
        TPacket Read();
        bool PacketsAvailable();
        void Write(IPEndPoint ep,byte[] data);
        void Connect(IPEndPoint ep);
        void Bind(ushort port);
        bool IsConnected(IPEndPoint ep);
    }
    public class TPacket
    {
        public IPEndPoint endpoint;
        public byte[] data;
    }
}
