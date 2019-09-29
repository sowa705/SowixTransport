namespace SowixTransport
{
    public class Event
    {
        public EventType Type;
        public int Peer;
        public string PacketType;
        public byte[] Data;
    }
    public enum EventType
    {
        PeerConnected,
        Data,
        None,
        PeerDisconnected
    }
}
