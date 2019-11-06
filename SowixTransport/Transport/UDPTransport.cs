using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SowixTransport
{
    public class UDPTransport : ITransport
    {
        UdpClient socket;

        public void Bind(ushort port)
        {
            socket = new UdpClient(port);
        }

        public void Connect(IPEndPoint ep)
        {
            socket = new UdpClient(ep.Address.ToString(),ep.Port);
        }

        public bool IsConnected(IPEndPoint ep)
        {
            throw new NotImplementedException();
        }

        public bool IsReliable()
        {
            return false;
        }

        public bool PacketsAvailable()
        {
            return socket.Available > 0;
        }

        public TPacket Read()
        {
            IPEndPoint remote=null;
            byte[] data=socket.Receive(ref remote);
            return new TPacket() { data=data,endpoint=remote };
        }

        public void Write(IPEndPoint ep, byte[] data)
        {
            if (ep==null)
            {
                socket.Send(data,data.Length);
            }
            else
            {
                socket.Send(data, data.Length,ep);
            }
        }
    }
}
