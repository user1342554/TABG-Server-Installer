using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace TabgInstaller.ProximityChat.Client
{
    [BepInPlugin("tabginstaller.proximitychat.client", "Proximity Voice Chat Client", "1.0.0")]
    [BepInDependency("tabginstaller.modsettings", BepInDependency.DependencyFlags.SoftDependency)]
    public class ProximityChatClientPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MicSensitivity;
        public static ConfigEntry<float> MasterVolume;
        public static ConfigEntry<string> MicrophoneDevice;
        public static ConfigEntry<int> VoicePort;
        public static ConfigEntry<string> ServerIP;

        private VoiceClient _voiceClient;
        private MicCapture _micCapture;
        private VoicePlayback _playback;
        private SpeakerIcon _speakerIcon;
        private bool _started;
        private float _retryTimer;
        private int _retryCount;
        private const int MaxRetries = 3;

        private void Awake()
        {
            Enabled = Config.Bind("ProximityChat", "Enabled", true, "Enable/disable voice chat");
            MicSensitivity = Config.Bind("ProximityChat", "MicSensitivity", 0.01f, "Voice activity detection threshold (RMS)");
            MasterVolume = Config.Bind("ProximityChat", "MasterVolume", 1.0f, "Overall voice chat volume");
            MicrophoneDevice = Config.Bind("ProximityChat", "MicrophoneDevice", "", "Microphone device name (empty = system default)");
            VoicePort = Config.Bind("ProximityChat", "VoicePort", 7778, "UDP port for voice traffic (must match server)");
            ServerIP = Config.Bind("ProximityChat", "ServerIP", "127.0.0.1", "Voice server IP address (server machine's IP)");

            try
            {
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Enabled", "Toggle voice chat on/off", Enabled);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Mic Sensitivity", "VAD threshold", MicSensitivity);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Master Volume", "Voice chat volume", MasterVolume);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Voice Port", "Must match server config", VoicePort);
            }
            catch { }

            _speakerIcon = new SpeakerIcon();
            Logger.LogInfo("[ProximityChat] Client plugin loaded.");
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            // Try to connect when we are in a game session
            if (!_started && IsInGameSession())
            {
                TryConnect();
            }

            if (_voiceClient != null && _voiceClient.IsConnected)
            {
                _micCapture?.ProcessMicData(MicSensitivity.Value);
                _playback?.Tick();

                if (_playback != null)
                {
                    // Sync transform cache to SpeakerIcon so it can render icons above talking players
                    _speakerIcon.UpdatePlayerCache(_playback.GetPlayerTransformCache());

                    foreach (int id in _playback.GetTalkingPlayerIds())
                        _speakerIcon.SetTalking(id, true);

                    _playback.UpdateConfig(_voiceClient.MinRange, _voiceClient.MaxRange, _voiceClient.FalloffCurve);
                }
            }
            else if (_started && _retryCount < MaxRetries)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0)
                {
                    _retryTimer = 5f;
                    _retryCount++;
                    Logger.LogInfo($"[ProximityChat] Retry {_retryCount}/{MaxRetries}...");
                    TryConnect();
                }
            }

            if (_started && !IsInGameSession())
            {
                Cleanup();
            }
        }

        private bool IsInGameSession()
        {
            try
            {
                var handler = Landfall.Network.PhotonServerHandler.instance;
                return handler != null && handler.AllPlayers != null;
            }
            catch
            {
                return false;
            }
        }

        private void TryConnect()
        {
            try
            {
                string serverIp = GetServerIp();
                if (serverIp == null)
                {
                    Logger.LogWarning("[ProximityChat] Cannot determine server IP yet.");
                    return;
                }

                int voicePort = VoicePort.Value;

                _playback = new VoicePlayback(MasterVolume.Value);

                _voiceClient = new VoiceClient(msg => Logger.LogInfo(msg));
                _voiceClient.OnAudioReceived += (senderId, seq, opusData, opusLen) =>
                {
                    _playback.EnqueueAudio(senderId, seq, opusData, opusLen);
                };

                // Get local player index
                byte localPlayerIndex = GetLocalPlayerIndex();
                _voiceClient.Connect(serverIp, voicePort, localPlayerIndex);

                try
                {
                    if (Microphone.devices.Length > 0)
                    {
                        _micCapture = new MicCapture(MicrophoneDevice.Value);
                        _micCapture.OnFrameEncoded += (data, len) => _voiceClient.SendAudio(data, len);
                        _micCapture.StartRecording();
                    }
                    else
                    {
                        Logger.LogWarning("[ProximityChat] No microphone found — receive-only mode.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[ProximityChat] Mic init failed, receive-only: {ex.Message}");
                }

                _started = true;
                Logger.LogInfo($"[ProximityChat] Connected to {serverIp}:{voicePort}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ProximityChat] Connect failed: {ex.Message}");
            }
        }

        private string GetServerIp()
        {
            return ServerIP.Value;
        }

        private byte GetLocalPlayerIndex()
        {
            try
            {
                var handler = Landfall.Network.PhotonServerHandler.instance;
                if (handler != null && handler.LocalPlayer != null)
                    return handler.LocalPlayer.PlayerIndex;
            }
            catch { }
            return 0;
        }

        private void OnGUI()
        {
            if (Enabled.Value)
                _speakerIcon?.OnGUI();
        }

        private void Cleanup()
        {
            _micCapture?.Dispose();
            _micCapture = null;
            _voiceClient?.Dispose();
            _voiceClient = null;
            _playback?.Dispose();
            _playback = null;
            _started = false;
            _retryCount = 0;
            _retryTimer = 0;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
