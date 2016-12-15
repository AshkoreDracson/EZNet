using EZNet;
using System;
using System.Text;
class Program
{
    static void Main(string[] args)
    {
        string s = Console.ReadLine();
        switch (s)
        {
            case "host":
                ServerMode();
                break;
            case "connect":
                ClientMode();
                break;
            default:
                break;
        }

        System.Diagnostics.Process.GetCurrentProcess().WaitForExit();
    }

    static void ServerMode()
    {
        Server.ClientConnected += Server_ClientConnected;
        Server.ClientDisconnected += Server_ClientDisconnected;
        Server.PacketReceived += Server_PacketRecieved;

        Server.Start();
        Console.WriteLine("Server started");
    }

    private static void Server_PacketRecieved(NetworkClient client, Packet packet)
    {
        Console.WriteLine(packet);
    }

    private static void Server_ClientDisconnected(NetworkClient client, DisconnectionReason reason)
    {
        Console.WriteLine($"Client disconnected! ID: {client.ID}, Reason: {reason}");
    }

    private static void Server_ClientConnected(NetworkClient client)
    {
        Console.WriteLine($"Client connected! ID: {client.ID}");
    }

    static void ClientMode()
    {
        Console.Write("IP: ");
        string ip = Console.ReadLine();

        Client c = new Client();
        c.OnConnected += new Client.OnConnectedEvent(C_OnConnected);
        c.OnDisconnected += new Client.OnDisconnectedEvent(C_OnDisconnected);
        c.PacketRecieved += new Client.PacketReceivedEvent(C_PacketRecieved);
        if (c.Connect(ip))
        {
            Console.WriteLine("Press any key to disconnect");
            Console.ReadKey();
            c.Disconnect();
        }
    }

    private static void C_PacketRecieved(Packet packet)
    {
        Console.WriteLine(packet);
    }

    private static void C_OnDisconnected(DisconnectionReason reason)
    {
        Console.WriteLine($"Disconnected, reason: {reason}");
    }

    private static void C_OnConnected()
    {
        Console.WriteLine("Connected!");
    }
}