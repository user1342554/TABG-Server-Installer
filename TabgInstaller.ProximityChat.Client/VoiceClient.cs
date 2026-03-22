using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TabgInstaller.ProximityChat.Client
{
    public class VoiceClient : IDisposable
    {
        private UdpClient _udp;
        private Thread _receiveThread;
        private volatile bool _running;
        private IPEndPoint _serverEndpoint;
        private ushort _sequence;

        public float MinRange { get; private set; } = 5f;
        public float MaxRange { get; private set; } = 50f;
        public byte FalloffCurve { get; private set; } = 0;
        public bool IsConnected { get; private set; }

        public event Action<int, ushort, byte[], int> OnAudioReceived;

        private readonly Action<string> _log;

        public VoiceClient(Action<string> log)
        {
            _log = log;
        }

        public void Connect(string serverIp, int serverPort, byte playerIndex)
        {
            try
            {
                _serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
                _udp = new UdpClient();
                _running = true;

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "VoiceClient-Recv"
                };
                _receiveThread.Start();

                byte[] handshake = PacketProtocol.WriteHandshake(playerIndex);
                _udp.Send(handshake, handshake.Length, _serverEndpoint);
                _log($"[ProximityChat] Handshake sent to {_serverEndpoint} as player {playerIndex}");
            }
            catch (Exception ex)
            {
                _log($"[ProximityChat] Connection failed: {ex.Message}");
            }
        }

        public void SendAudio(byte[] opusData, int opusLength)
        {
            if (!_running || _udp == null || _serverEndpoint == null) return;
            try
            {
                byte[] packet = PacketProtocol.WriteClientAudio(_sequence++, opusData, opusLength);
                _udp.Send(packet, packet.Length, _serverEndpoint);
            }
            catch { }
        }

        private void ReceiveLoop()
        {
            var remoteEp = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref remoteEp);
                    if (data.Length == 0) continue;

                    byte packetType = PacketProtocol.ReadPacketType(data);

                    if (packetType == PacketProtocol.PacketTypeConfig)
                    {
                        if (PacketProtocol.TryReadConfig(data, out float min, out float max, out byte falloff))
                        {
                            MinRange = min;
                            MaxRange = max;
                            FalloffCurve = falloff;
                            IsConnected = true;
                            _log($"[ProximityChat] Connected. Range: {min}-{max}m, Falloff: {(falloff == 0 ? "Linear" : "Logarithmic")}");
                        }
                    }
                    else if (packetType == PacketProtocol.PacketTypeAudio)
                    {
                        if (PacketProtocol.TryReadRelayAudio(data, out int senderId, out ushort seq, out byte[] opusData, out int opusLen))
                        {
                            OnAudioReceived?.Invoke(senderId, seq, opusData, opusLen);
                        }
                    }
                }
                catch (SocketException) when (!_running) { break; }
                catch { }
            }
        }

        public void Dispose()
        {
            _running = false;
            IsConnected = false;
            try { _udp?.Close(); } catch { }
        }
    }
}
