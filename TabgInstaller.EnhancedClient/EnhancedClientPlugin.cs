using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TabgInstaller.ModSettings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TabgInstaller.EnhancedClient
{
    [BepInPlugin("tabginstaller.enhancedclient", "TABG Enhanced Client", "1.0.0")]
    [BepInDependency("tabginstaller.modsettings", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class EnhancedClientPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<KeyCode> ToggleLodKey;
        internal static ConfigEntry<KeyCode> ToggleUiKey;
        internal static ConfigEntry<KeyCode> ToggleHazeKey;
        internal static ConfigEntry<float> ItemDrawDistance;
        internal static ConfigEntry<bool> StartWithLodUnlocked;
        internal static ConfigEntry<bool> StartWithHazeDisabled;
        internal static ConfigEntry<bool> BlockChunkUnloads;
        internal static ConfigEntry<bool> RenameOfflineButton;

        internal static bool LodUnlocked;
        internal static bool UiHidden;
        internal static bool HazeDisabled;
        internal static WilhelmStreaming Streaming;

        private static Harmony _harmony;

        private void Awake()
        {
            ToggleLodKey = Config.Bind("Keybinds", "ToggleLodUnlock", KeyCode.F1, "Toggle full map/object LOD loading.");
            ToggleUiKey = Config.Bind("Keybinds", "ToggleUi", KeyCode.F2, "Toggle in-game UI visibility.");
            ToggleHazeKey = Config.Bind("Keybinds", "ToggleHaze", KeyCode.F3, "Toggle atmospheric haze.");
            ItemDrawDistance = Config.Bind("Visuals", "ItemDrawDistance", 250f,
                new ConfigDescription("Pickup/item draw distance in meters.", new AcceptableValueRange<float>(25f, 2500f)));
            StartWithLodUnlocked = Config.Bind("Visuals", "StartWithLodUnlocked", false, "Load all map/object chunks when the client camera is ready.");
            StartWithHazeDisabled = Config.Bind("Visuals", "StartWithHazeDisabled", false, "Disable haze when the client camera is ready.");
            BlockChunkUnloads = Config.Bind("Visuals", "BlockChunkUnloadsWhenUnlocked", false, "Prevent streamed chunks from unloading while LOD unlock is enabled. Experimental and disabled by default.");
            RenameOfflineButton = Config.Bind("Interface", "RenameOfflineButton", true, "Rename the main menu Play Offline button to LAN Mode.");

            LodUnlocked = StartWithLodUnlocked.Value;
            HazeDisabled = StartWithHazeDisabled.Value;

            InstallMenuLabelRewriter();
            RegisterSettings();

            _harmony = new Harmony("tabginstaller.enhancedclient");
            _harmony.PatchAll(typeof(EnhancedClientPlugin).Assembly);

            Logger.LogInfo("[EnhancedClient] Loaded. F1=LOD, F2=UI, F3=haze.");
        }

        private static void RegisterSettings()
        {
            try
            {
                ModSettingsUI.Register("Enhanced Client", "Toggle LOD", "Key to toggle full map/object loading", ToggleLodKey);
                ModSettingsUI.Register("Enhanced Client", "Toggle UI", "Key to hide/show the in-game HUD", ToggleUiKey);
                ModSettingsUI.Register("Enhanced Client", "Toggle Haze", "Key to disable/enable haze", ToggleHazeKey);
                ModSettingsUI.Register("Enhanced Client", "Item Draw Distance", "Pickup/item draw distance in meters", ItemDrawDistance);
                ModSettingsUI.Register("Enhanced Client", "Start LOD Unlocked", "Enable LOD unlock when the camera loads", StartWithLodUnlocked);
                ModSettingsUI.Register("Enhanced Client", "Start Haze Disabled", "Disable haze when the camera loads", StartWithHazeDisabled);
                ModSettingsUI.Register("Enhanced Client", "Block Chunk Unloads", "Keep chunks loaded while LOD unlock is active", BlockChunkUnloads);
                ModSettingsUI.Register("Enhanced Client", "LAN Menu Label", "Rename Play Offline to LAN Mode in the main menu", RenameOfflineButton);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EnhancedClient] ModSettings registration failed: " + ex.Message);
            }
        }

        private static void InstallMenuLabelRewriter()
        {
            try
            {
                if (Object.FindObjectOfType<MainMenuLabelRewriter>() != null)
                    return;

                var host = new GameObject("TabgInstaller.EnhancedClient.MenuLabelRewriter");
                host.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(host);
                host.AddComponent<MainMenuLabelRewriter>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EnhancedClient] Could not install menu label rewriter: " + ex.Message);
            }
        }
    }

    internal sealed class MainMenuLabelRewriter : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.5f;
        private const string ReplacementLabel = "LAN Mode";

        private Type _unityUiTextType;
        private PropertyInfo _unityUiTextProperty;
        private Type _tmpTextType;
        private PropertyInfo _tmpTextProperty;
        private float _nextScanAt;
        private bool _loggedReplacement;

        private void Awake()
        {
            _unityUiTextType = FindType("UnityEngine.UI.Text");
            _unityUiTextProperty = _unityUiTextType?.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            _tmpTextType = FindType("TMPro.TMP_Text");
            _tmpTextProperty = _tmpTextType?.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
        }

        private void Update()
        {
            if (!EnhancedClientPlugin.RenameOfflineButton.Value || Time.unscaledTime < _nextScanAt)
                return;

            _nextScanAt = Time.unscaledTime + ScanIntervalSeconds;
            RenameLabels(_unityUiTextType, _unityUiTextProperty);
            RenameLabels(_tmpTextType, _tmpTextProperty);
        }

        private void RenameLabels(Type textType, PropertyInfo textProperty)
        {
            if (textType == null || textProperty == null)
                return;

            Object[] textObjects;
            try
            {
                textObjects = Resources.FindObjectsOfTypeAll(textType);
            }
            catch
            {
                return;
            }

            foreach (var textObject in textObjects)
            {
                if (textObject == null || !IsSceneComponent(textObject))
                    continue;

                var currentText = textProperty.GetValue(textObject, null) as string;
                if (!IsOfflineButtonText(currentText))
                    continue;

                textProperty.SetValue(textObject, ReplacementLabel, null);
                if (!_loggedReplacement)
                {
                    Debug.Log("[EnhancedClient] Renamed Play Offline menu label to LAN Mode.");
                    _loggedReplacement = true;
                }
            }
        }

        private static bool IsSceneComponent(Object textObject)
        {
            var component = textObject as Component;
            return component != null && component.gameObject.scene.IsValid();
        }

        private static bool IsOfflineButtonText(string text)
        {
            var normalized = NormalizeWhitespace(text);
            return normalized.Length <= 32 &&
                   normalized.IndexOf("Play Offline", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static Type FindType(string fullName)
        {
            var type = AccessTools.TypeByName(fullName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }

    internal sealed class EnhancedCameraController : MonoBehaviour
    {
        private static readonly FieldInfo LoadedChunksField = AccessTools.Field(typeof(WilhelmChunker), "loadedChunks");

        private Behaviour _hazeView;
        private Type _hazeViewType;

        private void Start()
        {
            ApplyHaze();
            ApplyItemDrawDistance();

            if (EnhancedClientPlugin.LodUnlocked)
                ForceLoadAllChunks();
        }

        private void Update()
        {
            if (Input.GetKeyDown(EnhancedClientPlugin.ToggleLodKey.Value))
                SetLodUnlocked(!EnhancedClientPlugin.LodUnlocked);

            if (Input.GetKeyDown(EnhancedClientPlugin.ToggleHazeKey.Value))
            {
                EnhancedClientPlugin.HazeDisabled = !EnhancedClientPlugin.HazeDisabled;
                ApplyHaze();
            }

            ApplyItemDrawDistance();
            ApplyHaze();
        }

        internal static void SetLodUnlocked(bool enabled)
        {
            EnhancedClientPlugin.LodUnlocked = enabled;
            var streaming = EnhancedClientPlugin.Streaming ?? WilhelmStreaming.instance;

            if (enabled)
            {
                ForceLoadAllChunks();
            }
            else if (streaming != null)
            {
                streaming.ResetStream();
            }
        }

        internal static void ForceLoadAllChunks()
        {
            var streaming = EnhancedClientPlugin.Streaming ?? WilhelmStreaming.instance;
            if (streaming == null)
                return;

            EnhancedClientPlugin.Streaming = streaming;
            LoadAll(streaming.worldChunks);
            LoadAll(streaming.propChunks);
            LoadAll(streaming.midChunks);
        }

        private static void LoadAll(WilhelmChunker chunker)
        {
            if (chunker == null || chunker.chunks == null)
                return;

            var loaded = new List<WilhelmChunkPiece>();
            var chunks = chunker.chunks;
            for (int x = chunks.GetLowerBound(0); x <= chunks.GetUpperBound(0); x++)
            {
                for (int y = chunks.GetLowerBound(1); y <= chunks.GetUpperBound(1); y++)
                {
                    var piece = chunks[x, y];
                    if (piece == null)
                        continue;

                    piece.EnterChunk();
                    loaded.Add(piece);
                }
            }

            LoadedChunksField?.SetValue(chunker, loaded);
        }

        private void ApplyItemDrawDistance()
        {
            if (PickupManager.instance == null)
                return;

            PickupManager.instance.m_DistanceThreshold = Mathf.Max(0f, EnhancedClientPlugin.ItemDrawDistance.Value);
        }

        private void ApplyHaze()
        {
            var haze = FindHazeView();
            if (haze != null)
                haze.enabled = !EnhancedClientPlugin.HazeDisabled;
        }

        private Behaviour FindHazeView()
        {
            if (_hazeView != null)
                return _hazeView;

            _hazeViewType ??= AccessTools.TypeByName("DeepSky.Haze.DS_HazeView");
            if (_hazeViewType != null)
                _hazeView = GetComponent(_hazeViewType) as Behaviour;

            if (_hazeView != null)
                return _hazeView;

            foreach (var behaviour in GetComponents<Behaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "DS_HazeView")
                {
                    _hazeView = behaviour;
                    break;
                }
            }

            return _hazeView;
        }
    }

    internal sealed class EnhancedUiController : MonoBehaviour
    {
        private GameObject _gameUi;
        private GameObject _screenSpaceCanvas;

        private void Start()
        {
            RefreshTargets();
            ApplyVisibility();
        }

        private void Update()
        {
            if (Input.GetKeyDown(EnhancedClientPlugin.ToggleUiKey.Value))
            {
                EnhancedClientPlugin.UiHidden = !EnhancedClientPlugin.UiHidden;
                ApplyVisibility();
            }
        }

        private void OnDestroy()
        {
            EnhancedClientPlugin.UiHidden = false;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            RefreshTargets();
            SetActive(_gameUi, !EnhancedClientPlugin.UiHidden);
            SetActive(_screenSpaceCanvas, !EnhancedClientPlugin.UiHidden);
        }

        private void RefreshTargets()
        {
            if (_gameUi == null)
                _gameUi = FindChild("GameUI");

            if (_screenSpaceCanvas == null)
                _screenSpaceCanvas = FindChild("ScreenSpaceCanvas");
        }

        private GameObject FindChild(string name)
        {
            var child = transform.Find(name);
            return child == null ? null : child.gameObject;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "Start")]
    internal static class PlayerCameraStartPatch
    {
        private static void Postfix(PlayerCamera __instance)
        {
            try
            {
                var player = __instance.transform.root.GetComponent<Player>();
                if (player != null && player == Player.localPlayer && __instance.GetComponent<EnhancedCameraController>() == null)
                    __instance.gameObject.AddComponent<EnhancedCameraController>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EnhancedClient] Could not attach camera controller: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryUI), "Start")]
    internal static class InventoryUiStartPatch
    {
        private static readonly FieldInfo IsLocalPlayerField = AccessTools.Field(typeof(InventoryUI), "isLocalPlayer");

        private static void Postfix(InventoryUI __instance)
        {
            try
            {
                var isLocal = IsLocalPlayerField == null || (bool)IsLocalPlayerField.GetValue(__instance);
                if (isLocal && __instance.GetComponent<EnhancedUiController>() == null)
                    __instance.gameObject.AddComponent<EnhancedUiController>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EnhancedClient] Could not attach UI controller: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(WilhelmStreaming), "Awake")]
    internal static class WilhelmStreamingAwakePatch
    {
        private static void Postfix(WilhelmStreaming __instance)
        {
            EnhancedClientPlugin.Streaming = __instance;
            if (EnhancedClientPlugin.LodUnlocked)
                EnhancedCameraController.ForceLoadAllChunks();
        }
    }

    [HarmonyPatch(typeof(WilhelmStreaming), "ResetStream")]
    internal static class WilhelmStreamingResetPatch
    {
        private static void Postfix(WilhelmStreaming __instance)
        {
            EnhancedClientPlugin.Streaming = __instance;
            if (EnhancedClientPlugin.LodUnlocked)
                EnhancedCameraController.ForceLoadAllChunks();
        }
    }

    [HarmonyPatch(typeof(WilhelmChunkPiece), "LeaveChunk")]
    internal static class WilhelmChunkLeavePatch
    {
        private static bool Prefix()
        {
            return !EnhancedClientPlugin.LodUnlocked || !EnhancedClientPlugin.BlockChunkUnloads.Value;
        }
    }
}
