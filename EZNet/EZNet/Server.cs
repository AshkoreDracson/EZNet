using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace EZNet
{
    public static class Server
    {
        public static List<NetworkClient> Clients { get; private set; } = new List<NetworkClient>();
        public static float HeartbeatTimer { get; set; } = 5f;
        public static bool IsRunning { get; private set; }

        public delegate void ClientConnectedEvent(NetworkClient client);
        public delegate void ClientDisconnectedEvent(NetworkClient client, DisconnectionReason reason);
        public delegate void PacketReceivedEvent(NetworkClient client, Packet packet);
        public delegate void MessageReceivedEvent(NetworkClient client, byte[] bytes);
        public static event ClientConnectedEvent ClientConnected;
        public static event ClientDisconnectedEvent ClientDisconnected;
        public static event PacketReceivedEvent PacketReceived;
        public static event MessageReceivedEvent MessageReceived;

        static TcpListener listener;

        public static void Start(ushort port = 2555)
        {
            if (IsRunning) return;

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            IsRunning = true;

            Thread t = new Thread(new ThreadStart(Listen));
            t.Start();
        }
        public static void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            listener.Stop();
            Clients.Clear();
        }

        public static void Send(NetworkClient client, byte[] bytes)
        {
            if (!IsRunning) return;

            Packet p = new Packet(Packet.Header.MESSAGE, bytes);
            byte[] serialized = p.Serialize();
            client.Stream.Write(serialized, 0, serialized.Length);
        }

        private static bool Heartbeat(NetworkClient client)
        {
            if (!IsRunning) return false;

            try
            {
                Packet p = new Packet(Packet.Header.HEARTBEAT, new byte[1] { 0 });
                byte[] serialized = p.Serialize();
                client.Stream.Write(serialized, 0, serialized.Length);
                client.State = NetworkClient.HeartbeatState.Awaiting;
                client.NextHeartbeat = DateTime.Now.AddSeconds(HeartbeatTimer);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Listen()
        {
            while (IsRunning)
            {
                // Accept pending clients
                if (listener.Pending())
                {
                    NetworkClient client = new NetworkClient(listener.AcceptTcpClient());
                    Clients.Add(client);

                    ClientConnected?.Invoke(client);
                }

                Dictionary<NetworkClient, DisconnectionReason> disconnectedClients = new Dictionary<NetworkClient, DisconnectionReason>();

                // Per client processing
                foreach (NetworkClient client in Clients)
                {
                    if (!client.Client.Connected)
                    {
                        disconnectedClients.Add(client, DisconnectionReason.Timeout);
                        continue;
                    }

                    // Check heartbeat
                    if (DateTime.Now >= client.NextHeartbeat)
                    {
                        if (client.State == NetworkClient.HeartbeatState.Idle)
                        {
                            bool sentHeartbeatSuccessfully = Server.Heartbeat(client);

                            if (!sentHeartbeatSuccessfully)
                                disconnectedClients.Add(client, DisconnectionReason.Timeout);
                        }
                        else if (client.State == NetworkClient.HeartbeatState.Awaiting)
                        {
                            disconnectedClients.Add(client, DisconnectionReason.Timeout);
                        }   
                    }

                    if (!client.Stream.DataAvailable) { continue; }

                    // Recieve all bytes of the packet
                    List<byte> finalBytes = new List<byte>(512);
                    int offset = 0;
                    byte[] buffer = new byte[512];

                    while (client.Stream.DataAvailable)
                    {
                        offset = client.Stream.Read(buffer, offset, buffer.Length);
                        finalBytes.AddRange(buffer);
                    }

                    // Deserialize the packet(s)
                    Packet[] packets = Packet.GetPackets(finalBytes.ToArray());
                    foreach (Packet p in packets)
                    {
                        if (p.PacketHeader == Packet.Header.HEARTBEAT)
                            client.State = NetworkClient.HeartbeatState.Idle;
                        else if (p.PacketHeader == Packet.Header.DISCONNECT)
                            disconnectedClients.Add(client, DisconnectionReason.Disconnected);

                        PacketReceived?.Invoke(client, p);

                        if (p.PacketHeader == Packet.Header.MESSAGE) MessageReceived?.Invoke(client, p.Bytes);
                    }
                }

                // Remove disconnected clients
                foreach (KeyValuePair<NetworkClient, DisconnectionReason> kvp in disconnectedClients)
                {
                    Clients.Remove(kvp.Key);
                    ClientDisconnected?.Invoke(kvp.Key, kvp.Value);
                }

                Thread.Sleep(1);
            }
        }
    }
}