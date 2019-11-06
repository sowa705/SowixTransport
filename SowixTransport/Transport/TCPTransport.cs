using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SowixTransport
{
    public class TCPTransport : ITransport
    {
        TcpListener server;
        public List<TcpClient> clients=new List<TcpClient>();
        TcpClient client;
        bool servermode;
        public void Bind(ushort port)
        {
            server = new TcpListener(IPAddress.Any,port);
            server.Start();
            servermode = true;
        }

        public void Connect(IPEndPoint ep)
        {
            client=new TcpClient(ep.Address.ToString(), ep.Port);
        }

        public bool IsReliable()
        {
            return true;
        }

        public bool PacketsAvailable()
        {
            if (servermode)
            {
                foreach (var item in clients)
                {
                    if (item.Available>0)
                    {
                        return true;
                    }
                }
                return server.Pending();
            }
            else
            {
                return client.Available>0;
            }
        }

        public TPacket Read()
        {
            if (servermode)
            {
                while(server.Pending())
                {
                    clients.Add(server.AcceptTcpClient());
                }
                foreach (var item in clients)
                {
                    if (!item.Connected)
                    {
                        clients.Remove(client);
                    }
                    if (item.GetStream().DataAvailable)
                    {
                        byte[] data=new byte[item.Available];
                        item.GetStream().Read(data, 0, item.Available);
                        return new TPacket { data=data,endpoint=(IPEndPoint)item.Client.RemoteEndPoint};
                    }
                }
            }
            else
            {
                if (client.GetStream().DataAvailable)
                {
                    byte[] data = new byte[client.Available];
                    client.GetStream().Read(data, 0, client.Available);
                    return new TPacket { data = data, endpoint = (IPEndPoint)client.Client.RemoteEndPoint };
                }
            }
            return null;
        }

        public void Write(IPEndPoint ep, byte[] data)
        {
            if (servermode)
            {
                var c = clients.FirstOrDefault(x => x.Client.RemoteEndPoint.Equals(ep));
                if (c==null)
                {
                    return;
                }
                c.GetStream().Write(data, 0, data.Length);
            }
            else
            {
                client.GetStream().Write(data,0,data.Length);
            }
        }

        public bool IsConnected(IPEndPoint ep)
        {
            TcpClient c = clients.FirstOrDefault(x=>x.Client.RemoteEndPoint.Equals(ep));
            if (c==null)
            {
                return false;
            }
            return c.Connected;
        }
    }
}
