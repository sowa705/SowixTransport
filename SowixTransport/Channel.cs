using System.Collections.Generic;

namespace SowixTransport
{
    class Channel
    {
        public List<Packet> Packets=new List<Packet>();
        public ChannelType Type;
        public int CurrentPacket;
    }
    public enum ChannelType
    {
        Unreliable, //działa
        Sequenced, //nie testowałem ale powinno działać
        Reliable, //nie działa
        SequencedReliable //baaaaaaardzo nie działa
    }
}
