using System.Net;

namespace SowixTransport
{
    public class Peer
    {
        public IPEndPoint EndPoint;

        public int[] RXChannelCurrentPackets; //nie interesuj sie

        public int RTT; //kiedyś to zrobie
        public int LastTick; //nie pytaj

        public PeerStatus Status;
    }
    public enum PeerStatus
    {
        Connected,
        Disconnected //peery nie są usuwane po rozłączeniu więc będą wisieć w statusie disconnected do OutOfMemoryException
    }
}
