using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SowixMessenger
{
    class Program
    {
        static SowixTransport.Transport t;
        static int messageRXChannel;
        static int messageTXChannel;
        static int position;
        static void Main(string[] args)
        {
            Console.WriteLine("SowixMessenger wersja 0\nWpisz ip serwera lub 's' aby uruchomić serwer na porcie 2137");

            while (true)
            {
                Console.Write(">");
                string s = Console.ReadLine();
                if (s == "s")
                {
                    Server();
                }
                if (s == "l")
                {
                    Client(IPAddress.Parse("127.0.0.1"));
                }
                if (s == "exit")
                {
                    return;
                }
                IPAddress addr;
                if (IPAddress.TryParse(s,out addr))
                {
                    Client(addr);
                }
                
            }
            
        }
        static void Client(IPAddress address)
        {
           
            t = new SowixTransport.Transport();
            messageRXChannel = t.AddRXChannel(SowixTransport.ChannelType.Unreliable);
            messageTXChannel = t.AddTXChannel(SowixTransport.ChannelType.Unreliable);
            t.Connect(address.ToString(),2137);
            new Thread(ClientRecvThread).Start();
            position = Console.WindowTop;
            while (true)
            {
                int x = Console.CursorLeft;
                int y = Console.CursorTop;
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write(">");
                string str = Console.ReadLine();
                byte[] message=Encoding.UTF8.GetBytes( str);
                Console.SetCursorPosition(x,y);
                if (str=="/exit")
                {
                    t.Disconnect();
                    return;
                }

                t.Send("Message",message,0,messageTXChannel);

                Thread.Sleep(100);
            }
        }
        static void ClientRecvThread()
        {
            while (true)
            {
                var events = t.Update();
                foreach (var item in events)
                {
                    //Console.WriteLine($"Received event {item.Type} type:{item.PacketType} from {item.Peer}");
                    switch (item.Type)
                    {
                        
                        case SowixTransport.EventType.Data:
                            if (item.PacketType == "Message")
                            {
                                Console.WriteLine(Encoding.UTF8.GetString( item.Data));
                            }
                            if (item.PacketType == "PConnect")
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[{DateTime.Now}] User connected");
                                Console.ResetColor();
                            }
                            if (item.PacketType == "PDisconnect")
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[{DateTime.Now}] User disconnected");
                                Console.ResetColor();
                            }
                            break;
                        default:
                            break;
                    }
                }
                Thread.Sleep(100);
            }
        }
        static void Server()
        {
            t=new SowixTransport.Transport();
            messageRXChannel= t.AddRXChannel(SowixTransport.ChannelType.Unreliable);
            messageTXChannel = t.AddTXChannel(SowixTransport.ChannelType.Unreliable);

            t.Bind(2137);

            while (true)
            {
                var events=t.Update();
                foreach (var item in events)
                {
                    Console.WriteLine($"Received event {item.Type} type:{item.PacketType} from {item.Peer}");
                    switch (item.Type)
                    {
                        case SowixTransport.EventType.PeerConnected:
                            for (int i = 0; i < t.Peers.Count; i++)
                            {
                                t.Send("PConnect", new byte[1], i, messageTXChannel);
                            }
                            break;
                        case SowixTransport.EventType.Data:
                            if (item.PacketType=="Message")
                            {
                                string message =item.Peer+" powiedział "+Encoding.UTF8.GetString(item.Data);
                                for (int i = 0; i < t.Peers.Count; i++)
                                {
                                    t.Send("Message",Encoding.UTF8.GetBytes(message),i,messageTXChannel);
                                }
                            }
                            break;
                        case SowixTransport.EventType.PeerDisconnected:
                            for (int i = 0; i < t.Peers.Count; i++)
                            {
                                t.Send("PDisconnect", new byte[1], i, messageTXChannel);
                            }
                            break;
                        default:
                            break;
                    }
                }
                Thread.Sleep(100);
            }
        }
    }
}
