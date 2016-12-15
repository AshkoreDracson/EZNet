using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
namespace EZNet
{
    public class Client
    {
        public NetworkStream Stream { get; private set; }
        public bool IsConnected
        {
            get
            {
                return client?.Connected ?? false;
            }
        }

        public delegate void OnConnectedEvent();
        public delegate void OnDisconnectedEvent(DisconnectionReason reason);
        public delegate void PacketReceivedEvent(Packet packet);
        public delegate void MessageReceivedEvent(byte[] bytes);
        public event OnConnectedEvent OnConnected;
        public event OnDisconnectedEvent OnDisconnected;
        public event PacketReceivedEvent PacketRecieved;
        public event MessageReceivedEvent MessageReceived;

        DateTime NextHeartbeat = DateTime.Now;
        bool isDisconnecting = false;
        TcpClient client = new TcpClient();

        public bool Connect(string hostname, ushort port = 2555)
        {
            Disconnect();
            try
            {
                client.Connect(hostname, port);
                Stream = client.GetStream();

                OnConnected?.Invoke();

                NextHeartbeat = DateTime.Now.AddSeconds(5);
                Thread t = new Thread(new ThreadStart(Listen));
                t.Start();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                Packet p = new Packet(Packet.Header.DISCONNECT, new byte[1] { 0 });
                byte[] serialized = p.Serialize();
                Stream.Write(serialized, 0, serialized.Length);

                isDisconnecting = true;
                client.Close();
            }
        }

        public void Send(byte[] bytes)
        {
            if (!IsConnected) return;

            Packet p = new Packet(Packet.Header.MESSAGE, bytes);
            byte[] serialized = p.Serialize();
            Stream.Write(serialized, 0, serialized.Length);
        }

        private bool ClientHeartbeat()
        {
            if (!IsConnected) return false;

            try
            {
                Packet p = new Packet(Packet.Header.CLIENT_HEARTBEAT, new byte[1] { 0 });
                byte[] serialized = p.Serialize();
                Stream.Write(serialized, 0, serialized.Length);
                NextHeartbeat = DateTime.Now.AddSeconds(5);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool Heartbeat()
        {
            if (!IsConnected) return false;

            try
            {
                Packet p = new Packet(Packet.Header.HEARTBEAT, new byte[1] { 0 });
                byte[] serialized = p.Serialize();
                Stream.Write(serialized, 0, serialized.Length);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Listen()
        {
            while (IsConnected)
            {
                // Check if still connected, aka client heartbeat (fire and forget, just for the sake to check if socket is closed)
                if (DateTime.Now >= NextHeartbeat)
                {
                    bool sentHeartbeatSuccessfully = ClientHeartbeat();
                    if (!sentHeartbeatSuccessfully)
                        Disconnect();
                }

                if (!Stream.DataAvailable) { Thread.Sleep(1); continue; }

                List<byte> finalBytes = new List<byte>(512);
                int offset = 0;
                
                byte[] buffer = new byte[512];

                while (Stream.DataAvailable)
                {
                    int bytesRead = Stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == buffer.Length)
                    {
                        finalBytes.AddRange(buffer);
                    }
                    else if (bytesRead > 0)
                    {
                        finalBytes.AddRange(buffer.Take(bytesRead));
                    }
                }

                Packet[] packets = Packet.GetPackets(finalBytes.ToArray());
                foreach (Packet p in packets)
                {
                    if (p.PacketHeader == Packet.Header.HEARTBEAT)
                    {
                        bool sentHeartbeatSuccessfully = Heartbeat();

                        if (!sentHeartbeatSuccessfully)
                            Disconnect();
                    }

                    PacketRecieved?.Invoke(p);

                    if (p.PacketHeader == Packet.Header.MESSAGE) MessageReceived?.Invoke(p.Bytes);
                }
            }

            OnDisconnected?.Invoke(isDisconnecting ? DisconnectionReason.Disconnected : DisconnectionReason.Timeout);
            isDisconnecting = false;
        }
    }
}