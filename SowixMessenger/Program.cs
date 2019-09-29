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
            Console.WriteLine("SowixMessenger wersja 0.1\nWpisz ip serwera lub 's' aby uruchomić serwer na porcie 2137");

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
            Console.Clear();
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
                Console.Write(">                                                  ");
                Console.SetCursorPosition(1, Console.WindowHeight - 1);
                string str = Console.ReadLine();
                byte[] message=Encoding.UTF8.GetBytes( str);
                Console.SetCursorPosition(x,y);
                if (str == "/exit")
                {
                    t.Disconnect();
                    return;
                }
                else if (str.StartsWith("/nickname "))
                {
                    t.Send("Nickname",Encoding.UTF8.GetBytes( str.Substring(10)), 0, messageTXChannel);
                }
                else if (str=="/users")
                {
                    t.Send("GetUsers", new byte[1], 0, messageTXChannel);
                }
                else
                {
                    t.Send("Message", message, 0, messageTXChannel);
                }
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
                    Console.SetCursorPosition(0,position++);
                    switch (item.Type)
                    {
                        
                        case SowixTransport.EventType.Data:
                            if (item.PacketType == "Message")
                            {
                                Console.WriteLine(Encoding.UTF8.GetString(item.Data));
                            }
                            if (item.PacketType == "SMessage")
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[Server]: {Encoding.UTF8.GetString(item.Data)}");
                                position+= Encoding.UTF8.GetString(item.Data).Count(f => f == '\n');
                                Console.ResetColor();
                            }
                            break;
                        default:
                            break;
                    }
                    Console.SetCursorPosition(1, Console.WindowHeight - 1);
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

            List<string> Nicknames = new List<string>();

            while (true)
            {
                var events=t.Update();
                foreach (var item in events)
                {
                    Console.WriteLine($"Received event {item.Type} type:{item.PacketType} from {item.Peer}");
                    switch (item.Type)
                    {
                        case SowixTransport.EventType.PeerConnected:
                            Nicknames.Add($"User {item.Peer}");
                            for (int i = 0; i < t.Peers.Count; i++)
                            {
                                t.Send("SMessage", Encoding.UTF8.GetBytes($"Connected {Nicknames[i]}"), i, messageTXChannel);
                            }
                            break;
                        case SowixTransport.EventType.Data:
                            if (item.PacketType == "Nickname")
                            {
                                if (Nicknames.Contains(Encoding.UTF8.GetString(item.Data)))
                                {
                                    t.Send("SMessage", Encoding.UTF8.GetBytes("This username is already taken"), item.Peer, messageTXChannel);
                                }
                                else
                                {
                                    for (int i = 0; i < t.Peers.Count; i++)
                                    {
                                        t.Send("SMessage", Encoding.UTF8.GetBytes($"{Nicknames[item.Peer]} changed nickname to {Encoding.UTF8.GetString(item.Data)}"), i, messageTXChannel);
                                    }
                                    Nicknames[item.Peer] = Encoding.UTF8.GetString(item.Data);
                                }
                               
                            }
                            if (item.PacketType=="Message")
                            {
                                string message = $"{Nicknames[item.Peer]}: "+Encoding.UTF8.GetString(item.Data);
                                for (int i = 0; i < t.Peers.Count; i++)
                                {
                                    t.Send("Message",Encoding.UTF8.GetBytes(message),i,messageTXChannel);
                                }
                            }
                            if (item.PacketType == "GetUsers")
                            {
                                string a="";
                                for(int i=0;i<t.Peers.Count;i++)
                                {
                                    var user = t.Peers[i];
                                    if (user.Status!=SowixTransport.PeerStatus.Connected)
                                    {
                                        continue;
                                    }
                                    a += $"{user.EndPoint} as {Nicknames[i]}\n";
                                }
                                t.Send("SMessage", Encoding.UTF8.GetBytes("User list:\n"+a), item.Peer, messageTXChannel);
                            }
                            break;
                        case SowixTransport.EventType.PeerDisconnected:
                            for (int i = 0; i < t.Peers.Count; i++)
                            {
                                t.Send("SMessage",Encoding.UTF8.GetBytes( $"Disconnected {Nicknames[i]}"), i, messageTXChannel);
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
