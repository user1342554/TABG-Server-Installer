using System;

namespace TabgInstaller.ProximityChat
{
    public static class PacketProtocol
    {
        public const byte PacketTypeAudio = 0x01;
        public const byte PacketTypeHandshake = 0x02;
        public const byte PacketTypeConfig = 0x03;

        // Client → Server audio: [type:1][seq:2][len:2][data:N]
        public static byte[] WriteClientAudio(ushort sequence, byte[] opusData, int opusLength)
        {
            var packet = new byte[1 + 2 + 2 + opusLength];
            packet[0] = PacketTypeAudio;
            packet[1] = (byte)(sequence & 0xFF);
            packet[2] = (byte)(sequence >> 8);
            packet[3] = (byte)(opusLength & 0xFF);
            packet[4] = (byte)(opusLength >> 8);
            Buffer.BlockCopy(opusData, 0, packet, 5, opusLength);
            return packet;
        }

        public static byte[] WriteHandshake()
        {
            return new byte[] { PacketTypeHandshake };
        }

        // Server → Client config: [type:1][minRange:4][maxRange:4][falloff:1]
        public static byte[] WriteConfig(float minRange, float maxRange, byte falloffCurve)
        {
            var packet = new byte[10];
            packet[0] = PacketTypeConfig;
            Buffer.BlockCopy(BitConverter.GetBytes(minRange), 0, packet, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(maxRange), 0, packet, 5, 4);
            packet[9] = falloffCurve;
            return packet;
        }

        // Server → Client relay: [type:1][senderId:4][seq:2][len:2][data:N]
        public static byte[] WriteRelayAudio(int senderId, ushort sequence, byte[] opusData, int opusLength)
        {
            var packet = new byte[1 + 4 + 2 + 2 + opusLength];
            packet[0] = PacketTypeAudio;
            Buffer.BlockCopy(BitConverter.GetBytes(senderId), 0, packet, 1, 4);
            packet[5] = (byte)(sequence & 0xFF);
            packet[6] = (byte)(sequence >> 8);
            packet[7] = (byte)(opusLength & 0xFF);
            packet[8] = (byte)(opusLength >> 8);
            Buffer.BlockCopy(opusData, 0, packet, 9, opusLength);
            return packet;
        }

        public static byte ReadPacketType(byte[] data)
        {
            return data.Length > 0 ? data[0] : (byte)0;
        }

        public static bool TryReadClientAudio(byte[] data, out ushort sequence, out byte[] opusData, out int opusLength)
        {
            sequence = 0; opusData = null; opusLength = 0;
            if (data.Length < 5) return false;
            sequence = (ushort)(data[1] | (data[2] << 8));
            opusLength = (ushort)(data[3] | (data[4] << 8));
            if (data.Length < 5 + opusLength) return false;
            opusData = new byte[opusLength];
            Buffer.BlockCopy(data, 5, opusData, 0, opusLength);
            return true;
        }

        public static bool TryReadConfig(byte[] data, out float minRange, out float maxRange, out byte falloffCurve)
        {
            minRange = 0; maxRange = 0; falloffCurve = 0;
            if (data.Length < 10) return false;
            minRange = BitConverter.ToSingle(data, 1);
            maxRange = BitConverter.ToSingle(data, 5);
            falloffCurve = data[9];
            return true;
        }

        public static bool TryReadRelayAudio(byte[] data, out int senderId, out ushort sequence, out byte[] opusData, out int opusLength)
        {
            senderId = 0; sequence = 0; opusData = null; opusLength = 0;
            if (data.Length < 9) return false;
            senderId = BitConverter.ToInt32(data, 1);
            sequence = (ushort)(data[5] | (data[6] << 8));
            opusLength = (ushort)(data[7] | (data[8] << 8));
            if (data.Length < 9 + opusLength) return false;
            opusData = new byte[opusLength];
            Buffer.BlockCopy(data, 9, opusData, 0, opusLength);
            return true;
        }
    }
}
