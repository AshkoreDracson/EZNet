using System;
using System.IO;
using System.Collections.Generic;
namespace EZNet
{
    public class Packet
    {
        public enum Header : byte
        {
            HEARTBEAT,
            CLIENT_HEARTBEAT,
            MESSAGE,
            DISCONNECT
        }
        public byte[] Bytes { get; set; }
        public Header PacketHeader { get; set; }
        public int Length
        {
            get
            {
                return Bytes.Length;
            }
        }
        public int TotalLength
        {
            get
            {
                return Length + 5;
            }
        }

        public Packet(Header header, byte[] bytes)
        {
            this.PacketHeader = header;
            this.Bytes = bytes;
        }

        public byte[] Serialize()
        {
            using (MemoryStream mS = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(mS);
                bw.Write(Length);
                bw.Write((byte)PacketHeader);
                bw.Write(Bytes);
                return mS.ToArray();
            }
        }

        public static Packet Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length <= 0)
                return null;

            using (MemoryStream mS = new MemoryStream(bytes))
            {
                BinaryReader br = new BinaryReader(mS);
                int length = br.ReadInt32();
                Header header = (Header)br.ReadByte();
                byte[] finalBytes = br.ReadBytes(length);
                return new Packet(header, finalBytes);
            }
        }

        public static Packet[] GetPackets(byte[] bytes)
        {
            List<Packet> packets = new List<Packet>();
            List<byte> curBytes = new List<byte>(bytes);

            while (true)
            {
                if (curBytes.Count <= 0) break;

                Packet p = Packet.Deserialize(curBytes.ToArray());
                if (p.Length <= 0) break;

                packets.Add(p);
                curBytes.RemoveRange(0, p.TotalLength);
            }

            return packets.ToArray();
        }

        public override string ToString()
        {
            return $"[Header={PacketHeader}, Length={Length}]";
        }
    }
}