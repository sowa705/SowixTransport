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

        List<Channel> Channels=new List<Channel>();

        int ControlChannel;
        int PingChannel;

        public List<Peer> Peers = new List<Peer>();
        bool clientmode;

        int CModeTick;

        public ClientState connectionState;

        int tick=0;
        public Transport()
        {
            ControlChannel=AddChannel(ChannelType.Reliable);
            PingChannel=AddChannel(ChannelType.Unreliable);
        }
        public void Bind(int port)
        {
            socket = new UdpClient(port);
        }
        public void Disconnect()
        {
            Send("ST_Disconnect", new byte[1], 0, 0);
            connectionState = ClientState.Disconnected;
        }
        public void Connect(string hostname,int port)
        {
            socket = new UdpClient(hostname,port);
            clientmode = true;
            Send("ST_Connect",new byte[1],0,ControlChannel);

        }
        public int AddChannel(ChannelType type) //nawet nie myśl o przydzielaniu channelID samemu sobie
        {
            Channel c = new Channel();
            c.Type = type;
            Channels.Add(c);
            return Channels.Count - 1;
        }
        public List<Event> Update()
        {
            tick++;
            var events = new List<Event>();
            while (socket.Available > 0)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = socket.Receive(ref remote);
                    Event e = ProcessPacket(data, remote);
                    if (e.Type != EventType.None)
                    {
                        events.Add(e);
                    }
                }
                catch
                {
                    continue;
                }
                
            }
            if (clientmode)
            {
                if (tick-CModeTick>20)
                {
                    connectionState = ClientState.Disconnected;
                }
            }
            else
            {
                foreach (var item in Peers)
                {
                    if (tick-item.LastTick>20&&item.Status!=PeerStatus.Disconnected)
                    {
                        item.Status = PeerStatus.Disconnected;
                        events.Add(new Event() { Type = EventType.PeerDisconnected,Peer = Peers.IndexOf(item)});
                    }
                }
                if (tick % 5 == 0)
                {
                    for (int i = 0; i < Peers.Count; i++)
                    {
                        Send("ST_Ping", BitConverter.GetBytes(tick), i, PingChannel);
                    }
                }
            }
            
            foreach (var item in Channels)
            {
                if (item.Type == ChannelType.Reliable||item.Type==ChannelType.SequencedReliable)
                {
                    for(int i=item.Packets.Count-1;i>=0;i--)
                    {
                        var packet = item.Packets[i];
                        if (tick - 2 > packet.SendTime)
                        {
                            SendRaw(packet);
                            if (tick - 2 > packet.SendTime)
                            {
                                item.Packets.RemoveAt(i);
                            }
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

                //Console.WriteLine($"<{packet.Channel} {packet.PacketType} {packet.PacketID}");

                Peer peer = Peers.FirstOrDefault(x=>x.EndPoint.ToString()==remote.ToString());
                int peerIndex = Peers.IndexOf(peer);

                if (!clientmode)
                {
                    if (peer == null)
                    {
                        if (packet.PacketType == "ST_Connect")
                        {
                            AddPeer(remote);
                            Send("ST_ConfirmConnect", BitConverter.GetBytes(Peers.Count - 1), Peers.Count - 1, 0);
                            return new Event() { Type = EventType.PeerConnected, Peer = Peers.Count-1 };
                        }
                        else
                        {
                            return new Event() { Type = EventType.None };
                        }
                    }
                    if (packet.PacketType == "ST_Ping")
                    {
                        Peers[peerIndex].RTT = tick - BitConverter.ToInt32(packet.Data, 0);
                        Peers[peerIndex].LastTick = tick;
                        Console.WriteLine("RTT: " + Peers[peerIndex].RTT);
                        return new Event() { Type = EventType.None };
                    }
                }
                else
                {
                    if (packet.PacketType == "ST_Ping")
                    {
                        Send("ST_Ping",packet.Data,0,packet.Channel);
                        CModeTick = tick;
                        connectionState = ClientState.Connected;
                        return new Event() { Type = EventType.None };
                    }
                }
                if (packet.PacketType == "ST_Disconnect")
                {
                    Peers[peerIndex].Status = PeerStatus.Disconnected;
                    return new Event() { Type = EventType.PeerDisconnected,Peer=peerIndex};
                }
                
                //ack
                if (packet.PacketType == "ST_ACK")
                {
                    //Console.WriteLine("Received ACK");
                    Channels[packet.Channel].Packets.Remove(Channels[packet.Channel].Packets.First(x => x.PacketID == packet.PacketID));
                    return new Event() { Type = EventType.None };
                }
                if (Channels[packet.Channel].Type == ChannelType.Reliable || Channels[packet.Channel].Type == ChannelType.SequencedReliable)
                {
                    //Console.WriteLine("Sent");
                    SendAck(packet.Channel, packet.PacketID, peerIndex);
                }
                //sequence check
                if (Channels[packet.Channel].Type==ChannelType.SequencedReliable|| Channels[packet.Channel].Type == ChannelType.Sequenced)
                {
                    if (Peers[peerIndex].RXChannelCurrentPackets[packet.Channel] >= packet.PacketID) //packet is older than new packets
                    {
                        //Console.WriteLine("Dropped by sequencer");
                        return new Event() { Type = EventType.None };
                    }
                    else
                    {
                        //Console.WriteLine("Accepted by sequencer");
                        Peers[peerIndex].RXChannelCurrentPackets[packet.Channel] = packet.PacketID;
                    }
                }
                return new Event() { Type = EventType.Data, Data = packet.Data, Peer = peerIndex, PacketType = packet.PacketType };
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return new Event() { Type = EventType.None };
            }
        }

        void AddPeer(IPEndPoint endPoint)
        {
            Peer peer = new Peer();
            peer.EndPoint = endPoint;
            peer.RXChannelCurrentPackets = new int[Channels.Count];
            peer.Status = PeerStatus.Connected;
            Peers.Add(peer);
        }

        //wszystko poniżej jest do poprawy - redundantny kod

        public void Send(string type,byte[] data,int destination,int channelID)
        {
            int packetID = Channels[channelID].CurrentPacket++;

            Packet packet = new Packet((byte)channelID,packetID,type,data);
            packet.SendTime = tick;
            packet.Destination = destination;

            if (Channels[channelID].Type==ChannelType.Reliable||Channels[channelID].Type==ChannelType.SequencedReliable)
            {
                Channels[channelID].Packets.Add(packet);
            }

            SendRaw(packet);
        }
        void SendAck(int channelID,int packetID,int destination)
        {
            Packet packet = new Packet((byte)channelID, packetID, "ST_ACK", new byte[1]);
            packet.SendTime = tick;
            packet.Destination = destination;

            SendRaw(packet);
        }
        void SendRaw(Packet packet)
        {
            byte[] p = packet.Serialize();
            if (clientmode)
            {
                socket.Send(p, p.Length);
                return;
            }
            if (Peers[packet.Destination].Status != PeerStatus.Connected)
            {
                return;
            }
            //Console.WriteLine($">{packet.Channel} {packet.PacketType} {packet.PacketID}");
            socket.Send(p, p.Length, Peers[packet.Destination].EndPoint);
        }
    }
    
}
