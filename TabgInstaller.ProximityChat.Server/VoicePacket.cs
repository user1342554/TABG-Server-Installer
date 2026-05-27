using System;

namespace TabgInstaller.ProximityChat.Server
{
    internal static class VoicePacket
    {
        public const byte EventCode = 240;
        public const byte Version = 1;
        public const byte FormatPcmU8 = 1;
        public const int SampleRate = 16000;
        public const int Channels = 1;
        public const int FrameSamples = 320;
        public const int HeaderSize = 10;
        public const int MaxPcmBytes = 1024;

        public static bool TryRead(byte[] packet, out byte senderId, out ushort sequence, out int pcmOffset, out int pcmLength)
        {
            senderId = 0;
            sequence = 0;
            pcmOffset = 0;
            pcmLength = 0;

            if (packet == null || packet.Length < HeaderSize || packet[0] != Version)
                return false;

            ushort sampleRate = ReadUInt16(packet, 4);
            ushort frameSamples = ReadUInt16(packet, 8);
            int payloadLength = packet.Length - HeaderSize;
            if (packet[6] != FormatPcmU8 ||
                packet[7] != Channels ||
                sampleRate != SampleRate ||
                frameSamples != FrameSamples ||
                payloadLength <= 0 ||
                payloadLength > MaxPcmBytes)
            {
                return false;
            }

            senderId = packet[1];
            sequence = ReadUInt16(packet, 2);
            pcmOffset = HeaderSize;
            pcmLength = payloadLength;
            return true;
        }

        public static byte[] WithSender(byte[] packet, byte senderId)
        {
            byte[] relayPacket = new byte[packet.Length];
            Buffer.BlockCopy(packet, 0, relayPacket, 0, packet.Length);
            relayPacket[1] = senderId;
            return relayPacket;
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }
    }
}
