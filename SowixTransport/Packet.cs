using System;
using System.Text;

namespace SowixTransport
{
    class Packet
    {
        public byte Channel;
        public int PacketID;
        public string PacketType;
        public byte[] Data;

        public int SendTime;
        public int Destination;

        public Packet()
        {

        }
        public Packet(byte channel,int packetID,string packetType,byte[] data)
        {
            Channel = channel;
            PacketID = packetID;
            PacketType = packetType;
            Data = data;
        }


        public static Packet Deserialize(byte[] packet)
        {
            Packet p = new Packet();

            p.Channel = packet[0];
            p.PacketID = BitConverter.ToInt32(packet,1);
            p.PacketType = Encoding.ASCII.GetString(packet,6,packet[5]);
            p.Data = new byte[packet.Length-6-packet[5]];

            Array.Copy(packet,6+packet[5],p.Data,0,p.Data.Length);

            return p;
        }
        public byte[] Serialize()
        {
            byte[] data = new byte[6+PacketType.Length+Data.Length];
            data[0] = Channel;

            Array.Copy(BitConverter.GetBytes(PacketID),0,data,1,4);

            data[5] = (byte)PacketType.Length;

            var type=Encoding.ASCII.GetBytes(PacketType);

            Array.Copy(type,0,data,6,type.Length);

            Array.Copy(Data,0,data,6+type.Length,Data.Length);

            return data;
        }
    }
}
