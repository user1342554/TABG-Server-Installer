using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
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

        private MicCapture _micCapture;
        private VoicePlayback _playback;
        private SpeakerIcon _speakerIcon;
        private Harmony _harmony;
        private bool _started;

        internal static ProximityChatClientPlugin Instance;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("ProximityChat", "Enabled", true, "Enable/disable voice chat");
            MicSensitivity = Config.Bind("ProximityChat", "MicSensitivity", 0.01f, "Voice activity detection threshold (RMS)");
            MasterVolume = Config.Bind("ProximityChat", "MasterVolume", 1.0f, "Overall voice chat volume");
            MicrophoneDevice = Config.Bind("ProximityChat", "MicrophoneDevice", "", "Microphone device name (empty = system default)");

            try
            {
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Enabled", "Toggle voice chat on/off", Enabled);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Mic Sensitivity", "VAD threshold", MicSensitivity);
                TabgInstaller.ModSettings.ModSettingsUI.Register("Proximity Chat", "Master Volume", "Voice chat volume", MasterVolume);
            }
            catch { }

            _speakerIcon = new SpeakerIcon();
            _playback = new VoicePlayback(MasterVolume.Value);

            // Patch client message receiving to intercept voice packets
            _harmony = new Harmony("tabginstaller.proximitychat.client");
            _harmony.PatchAll(typeof(VoiceReceivePatch));

            Logger.LogInfo("[ProximityChat] Client plugin loaded — using game network.");
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            // Start mic when entering a game session
            if (!_started && IsInGameSession())
            {
                StartVoice();
            }

            if (_started)
            {
                _micCapture?.ProcessMicData(MicSensitivity.Value);
                _playback?.Tick();

                if (_playback != null)
                {
                    _speakerIcon.UpdatePlayerCache(_playback.GetPlayerTransformCache());
                    foreach (int id in _playback.GetTalkingPlayerIds())
                        _speakerIcon.SetTalking(id, true);
                }
            }

            // Cleanup when leaving
            if (_started && !IsInGameSession())
            {
                StopVoice();
            }
        }

        private bool IsInGameSession()
        {
            try
            {
                var handler = PhotonServerHandler.instance;
                return handler != null && handler.AllPlayers != null;
            }
            catch { return false; }
        }

        private void StartVoice()
        {
            try
            {
                if (Microphone.devices.Length > 0)
                {
                    _micCapture = new MicCapture(MicrophoneDevice.Value);
                    _micCapture.OnFrameEncoded += OnVoiceFrameEncoded;
                    _micCapture.StartRecording();
                    Logger.LogInfo("[ProximityChat] Microphone started.");
                }
                else
                {
                    Logger.LogWarning("[ProximityChat] No microphone — receive-only mode.");
                }
                _started = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ProximityChat] Start failed: {ex.Message}");
            }
        }

        private void OnVoiceFrameEncoded(byte[] opusData, int opusLength)
        {
            // Send voice data to server through the game's network
            try
            {
                var connector = ServerConnector.Instance;
                if (connector == null) return;

                byte[] buffer = new byte[opusLength];
                Buffer.BlockCopy(opusData, 0, buffer, 0, opusLength);
                connector.SendMessageToServer((EventCode)240, buffer, false); // Unreliable
            }
            catch { }
        }

        /// <summary>
        /// Harmony prefix patch on ServerConnector.OnEvent to intercept incoming voice packets (EventCode 240).
        /// Extracts sender index and opus data, then enqueues for playback.
        /// </summary>
        [HarmonyPatch(typeof(ServerConnector), "OnEvent")]
        internal static class VoiceReceivePatch
        {
            static bool Prefix(ClientPackage clientPackage)
            {
                if ((byte)clientPackage.Code != 240) return true;

                try
                {
                    if (Instance == null || !Instance._started || Instance._playback == null) return false;

                    byte[] data = clientPackage.Buffer;
                    if (data == null || data.Length < 2) return false;

                    byte senderIndex = data[0];
                    byte[] opusData = new byte[data.Length - 1];
                    Buffer.BlockCopy(data, 1, opusData, 0, opusData.Length);

                    // EnqueueAudio is safe to call from any thread
                    Instance._playback.EnqueueAudio(senderIndex, 0, opusData, opusData.Length);
                }
                catch { }

                return false; // Consume the event
            }
        }

        private void StopVoice()
        {
            _micCapture?.Dispose();
            _micCapture = null;
            _playback?.Dispose();
            _playback = new VoicePlayback(MasterVolume.Value); // Reset for next game
            _started = false;
        }

        private void OnGUI()
        {
            if (Enabled.Value && _started)
                _speakerIcon?.OnGUI();
        }

        private void OnDestroy()
        {
            _micCapture?.Dispose();
            _playback?.Dispose();
            _harmony?.UnpatchSelf();
        }
    }
}
