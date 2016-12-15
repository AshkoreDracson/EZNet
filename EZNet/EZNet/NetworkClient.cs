using System;
using System.Net.Sockets;
namespace EZNet
{
    public class NetworkClient
    {
        public enum HeartbeatState : byte
        {
            Idle,
            Awaiting
        }

        public uint ID { get; private set; }
        public TcpClient Client { get; private set; }
        public NetworkStream Stream { get; private set; }
        public HeartbeatState State { get; set; }
        public DateTime NextHeartbeat { get; set; }

        private static uint curID = 0;

        public NetworkClient(TcpClient client)
        {
            this.ID = curID++;
            this.Client = client;
            this.Stream = client.GetStream();
            this.State = HeartbeatState.Idle;
            this.NextHeartbeat = DateTime.Now.AddSeconds(Server.HeartbeatTimer);
        }
    }
}