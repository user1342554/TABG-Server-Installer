using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CitrusLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Server
{
    public class VoiceServer : IDisposable
    {
        private readonly int _port;
        private readonly float _minRange;
        private readonly float _maxRange;
        private readonly byte _falloffCurve;
        private readonly PlayerRegistry _registry;
        private UdpClient _udp;
        private Thread _receiveThread;
        private volatile bool _running;
        private readonly Action<string> _log;

        public VoiceServer(int port, float minRange, float maxRange, string falloffCurve, Action<string> log)
        {
            _port = port;
            _minRange = minRange;
            _maxRange = maxRange;
            _falloffCurve = falloffCurve.Equals("Logarithmic", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;
            _registry = new PlayerRegistry();
            _log = log;
        }

        public void Start()
        {
            try
            {
                _udp = new UdpClient(_port);
                _running = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "VoiceServer-Recv"
                };
                _receiveThread.Start();
                _log($"[ProximityChat] Voice server started on port {_port}");
            }
            catch (Exception ex)
            {
                _log($"[ProximityChat] Failed to start voice server on port {_port}: {ex.Message}");
            }
        }

        public void OnPlayerDisconnected(int playerId)
        {
            _registry.Remove(playerId);
        }

        public void ClearAll()
        {
            _registry.Clear();
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

                    if (packetType == PacketProtocol.PacketTypeHandshake)
                    {
                        HandleHandshake(remoteEp);
                    }
                    else if (packetType == PacketProtocol.PacketTypeAudio)
                    {
                        HandleAudio(data, remoteEp);
                    }
                }
                catch (SocketException) when (!_running)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        _log($"[ProximityChat] Receive error: {ex.Message}");
                }
            }
        }

        private void HandleHandshake(IPEndPoint remoteEp)
        {
            int playerId = FindPlayerByIp(remoteEp.Address.ToString());
            if (playerId < 0)
            {
                _log($"[ProximityChat] Handshake from unknown IP: {remoteEp.Address}");
                return;
            }

            _registry.TryRegister(remoteEp, playerId);
            _log($"[ProximityChat] Player {playerId} registered for voice from {remoteEp}");

            byte[] configPacket = PacketProtocol.WriteConfig(_minRange, _maxRange, _falloffCurve);
            try { _udp.Send(configPacket, configPacket.Length, remoteEp); }
            catch { }
        }

        private void HandleAudio(byte[] data, IPEndPoint remoteEp)
        {
            if (!_registry.TryGetPlayerIdByIp(remoteEp, out int senderId))
                return;

            if (!PacketProtocol.TryReadClientAudio(data, out ushort sequence, out byte[] opusData, out int opusLength))
                return;

            Vector3 senderPos;
            try
            {
                var senderPlayer = FindPlayerById(senderId);
                if (senderPlayer == null) return;
                senderPos = senderPlayer.PlayerPosition;
            }
            catch { return; }

            byte[] relayPacket = PacketProtocol.WriteRelayAudio(senderId, sequence, opusData, opusLength);

            foreach (int receiverId in _registry.GetAllPlayerIds())
            {
                if (receiverId == senderId) continue;

                if (!_registry.TryGetEndpoint(receiverId, out IPEndPoint receiverEp))
                    continue;

                try
                {
                    var receiverPlayer = FindPlayerById(receiverId);
                    if (receiverPlayer == null) continue;

                    if (!DistanceCalculator.IsInRange(senderPos, receiverPlayer.PlayerPosition, _maxRange))
                        continue;

                    _udp.Send(relayPacket, relayPacket.Length, receiverEp);
                }
                catch { }
            }
        }

        private int FindPlayerByIp(string ip)
        {
            try
            {
                var world = Citrus.World;
                if (world == null) return -1;
                var players = Citrus.players;
                if (players == null) return -1;

                foreach (var playerRef in players)
                {
                    if (playerRef == null || playerRef.player == null) continue;
                    var player = playerRef.player;
                    try
                    {
                        string playerIp = null;
                        var netPlayerProp = player.GetType().GetProperty("NetworkPlayer");
                        if (netPlayerProp != null)
                        {
                            var netPlayer = netPlayerProp.GetValue(player);
                            if (netPlayer != null)
                            {
                                var ipProp = netPlayer.GetType().GetProperty("IP");
                                playerIp = ipProp?.GetValue(netPlayer) as string;
                            }
                        }
                        if (playerIp == null)
                        {
                            var epField = player.GetType().GetField("m_endPoint",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (epField != null)
                            {
                                var ep = epField.GetValue(player) as IPEndPoint;
                                playerIp = ep?.Address.ToString();
                            }
                        }
                        if (playerIp != null && playerIp == ip)
                            return player.PlayerIndex;
                    }
                    catch { }
                }
            }
            catch { }
            return -1;
        }

        private TABGPlayerServer FindPlayerById(int playerId)
        {
            try
            {
                var players = Citrus.players;
                if (players == null) return null;
                foreach (var playerRef in players)
                {
                    if (playerRef != null && playerRef.player != null && playerRef.player.PlayerIndex == playerId)
                        return playerRef.player;
                }
            }
            catch { }
            return null;
        }

        public void Dispose()
        {
            _running = false;
            try { _udp?.Close(); } catch { }
            _registry.Clear();
        }
    }
}
