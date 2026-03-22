using System;
using System.Reflection;
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

                _voiceClient.Connect(serverIp, voicePort);

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
            // Try multiple reflection paths to find the server IP
            try
            {
                var serverClient = UnityEngine.Object.FindObjectOfType<Landfall.Network.ServerClient>();
                if (serverClient != null)
                {
                    foreach (string fieldName in new[] { "m_serverIP", "m_ip", "serverIP", "ip" })
                    {
                        var field = typeof(Landfall.Network.ServerClient).GetField(fieldName,
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (field != null)
                        {
                            var val = field.GetValue(serverClient) as string;
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                    foreach (string propName in new[] { "IP", "ServerIP", "ConnectedIP" })
                    {
                        var prop = typeof(Landfall.Network.ServerClient).GetProperty(propName,
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            var val = prop.GetValue(serverClient) as string;
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                }
            }
            catch { }

            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-connect" || args[i] == "-ip")
                        return args[i + 1];
                }
            }
            catch { }

            return null;
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
