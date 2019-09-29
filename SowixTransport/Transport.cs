using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SowixTransport
{
    public class Transport
    {
        UdpClient socket;

        List<Channel> TXChannels=new List<Channel>();
        List<Channel> RXChannels=new List<Channel>();

        public List<Peer> Peers = new List<Peer>();
        bool clientmode;

        int tick=0;
        public Transport()
        {
            AddTXChannel(ChannelType.Unreliable); //tu ma być reliable ale nie jest
            AddRXChannel(ChannelType.Unreliable);
        }
        public void Bind(int port)
        {
            socket = new UdpClient(port);
        }
        public void Disconnect()
        {
            Send("ST_Disconnect", new byte[1], 0, 0);
        }
        public void Connect(string hostname,int port)
        {
            socket = new UdpClient(hostname,port);
            clientmode = true;
            Send("ST_Connect",new byte[1],0,0);
        }
        public int AddTXChannel(ChannelType type) //nawet nie myśl o przydzielaniu channelID samemu sobie
        {
            Channel c = new Channel();
            c.Type = type;
            TXChannels.Add(c);
            return TXChannels.Count - 1;
        }
        public int AddRXChannel(ChannelType type)
        {
            Channel c = new Channel();
            c.Type = type;
            RXChannels.Add(c);
            return RXChannels.Count - 1;
        }
        public List<Event> Update()
        {
            tick++;
            var events = new List<Event>();
            while (socket.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = socket.Receive(ref remote);
                Event e = ProcessPacket(data, remote);
                if (e.Type != EventType.None)
                {
                    events.Add(e);
                }
            }
            foreach (var item in TXChannels)
            {
                if (item.Type == ChannelType.Reliable)
                {
                    foreach (var packet in item.Packets)
                    {
                        if (tick - 2 > packet.SendTime)
                        {
                            Resend(packet);
                        }
                    }
                }
            }
            return events;
        }

        Event ProcessPacket(byte[] data,IPEndPoint remote)
        {
            try
            {
                Packet packet = Packet.Deserialize(data);

                Peer peer = Peers.FirstOrDefault(x=>x.EndPoint.ToString()==remote.ToString()); //to porównanie to skurwiała kupa gówna i jebać EndPoint.operator==
                int peerIndex = Peers.IndexOf(peer);

                if (!clientmode)
                {
                    if (peer == null)
                    {
                        if (packet.PacketType == "ST_Connect")
                        {
                            AddPeer(remote);
                            Send("ST_ConfirmConnect", BitConverter.GetBytes(Peers.Count - 1), Peers.Count - 1, 0);
                            return new Event() { Type = EventType.PeerConnected, Peer = peerIndex };
                        }
                        else
                        {
                            return new Event() { Type = EventType.None };
                        }
                    }
                }
                if (packet.PacketType == "ST_Disconnect")
                {
                    Peers[peerIndex].Status = PeerStatus.Disconnected;
                    return new Event() { Type = EventType.PeerDisconnected,Peer=peerIndex};
                }
                //sequence check
                if (RXChannels[packet.Channel].Type==ChannelType.SequencedReliable|| RXChannels[packet.Channel].Type == ChannelType.Sequenced)
                {
                    if (Peers[peerIndex].RXChannelCurrentPackets[packet.Channel] > packet.PacketID) //packet is older than new packets
                    {
                        return new Event() { Type = EventType.None };
                    }
                    else
                    {
                        Peers[peerIndex].RXChannelCurrentPackets[packet.Channel] = packet.PacketID;
                    }
                }
                if (RXChannels[packet.Channel].Type==ChannelType.Reliable|| RXChannels[packet.Channel].Type==ChannelType.SequencedReliable)
                {
                    SendNoAck("ST_ACK",BitConverter.GetBytes(packet.PacketID),peerIndex,packet.Channel);
                }
                //ack
                if (packet.PacketType=="ST_ACK")
                {
                    TXChannels[packet.Channel].Packets.Remove(TXChannels[packet.Channel].Packets.First(x=>x.PacketID==packet.PacketID));
                    return new Event() { Type = EventType.None };
                }
                
                if (packet.Channel==0)
                {
                    //return new Event() {Type=EventType.None };
                }
                return new Event() { Type = EventType.Data, Data = packet.Data, Peer = peerIndex, PacketType = packet.PacketType };
            }
            catch
            {
                return new Event() { Type = EventType.None };
            }
        }

        void AddPeer(IPEndPoint endPoint)
        {
            Peer peer = new Peer();
            peer.EndPoint = endPoint;
            peer.RXChannelCurrentPackets = new int[RXChannels.Count];
            peer.Status = PeerStatus.Connected;
            Peers.Add(peer);
        }

        //wszystko poniżej jest do poprawy - redundantny kod

        public void Send(string type,byte[] data,int destination,int channelID)
        {
            int packetID = TXChannels[channelID].CurrentPacket++;

            Packet packet = new Packet((byte)channelID,packetID,type,data);
            packet.SendTime = tick;
            packet.Destination = destination;

            if (TXChannels[channelID].Type==ChannelType.Reliable||TXChannels[channelID].Type==ChannelType.SequencedReliable)
            {
                TXChannels[channelID].Packets.Add(packet);
            }

            byte[] p = packet.Serialize();
            if (clientmode)
            {
                socket.Send(p,p.Length);
                return;
            }
            if (Peers[destination].Status != PeerStatus.Connected)
            {
                return;
            }
            socket.Send(p,p.Length,Peers[destination].EndPoint);
        }
        void SendNoAck(string type, byte[] data, int destination, int channelID)
        {
            int packetID = TXChannels[channelID].CurrentPacket++;

            Packet packet = new Packet((byte)channelID, packetID, type, data);
            packet.SendTime = tick;
            packet.Destination = destination;

            if (TXChannels[channelID].Type == ChannelType.Reliable || TXChannels[channelID].Type == ChannelType.SequencedReliable)
            {
                TXChannels[channelID].Packets.Add(packet);
            }

            byte[] p = packet.Serialize();
            if (clientmode)
            {
                socket.Send(p, p.Length);
                return;
            }
            if (Peers[destination].Status != PeerStatus.Connected)
            {
                return;
            }
            socket.Send(p, p.Length, Peers[destination].EndPoint);
        }
        void Resend(Packet packet)
        {
            byte[] p = packet.Serialize();
            if (clientmode)
            {
                socket.Send(p, p.Length);
                return;
            }
            socket.Send(p, p.Length, Peers[packet.Destination].EndPoint);
        }
    }
    
}
