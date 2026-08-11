using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabgInstaller.RangeMap.Client
{
    [BepInPlugin("tabginstaller.rangemap.client", "Range Map Client", "1.0.0")]
    public sealed class RangeMapClientPlugin : BaseUnityPlugin
    {
        internal static RangeMapClientPlugin Instance;
        internal static bool RangeServerAccepted;
        internal static bool RangeSceneActive;

        private ConfigEntry<KeyCode> _itemMenuKey;
        private Harmony _harmony;
        private bool _showItems;
        private bool _savedUsingInterface;
        private bool _hasSavedUsingInterface;
        private string _search = string.Empty;
        private Vector2 _scroll;
        private Rect _window = new Rect(30f, 60f, 530f, 650f);
        private List<ItemChoice> _items;

        internal void LogRangeInfo(string message) => Logger.LogInfo(message);

        private void Awake()
        {
            Instance = this;
            _itemMenuKey = Config.Bind("RangeMap", "ItemMenuKey", KeyCode.F6, "Open the server-authoritative all-items menu.");
            _harmony = new Harmony("tabginstaller.rangemap.client");
            _harmony.PatchAll();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("[RangeMap] Client ready. The item menu is F6 while connected to a Range server.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(_itemMenuKey.Value))
            {
                if (_showItems)
                    CloseItemMenu();
                else
                    OpenItemMenu();
            }
        }

        private void LateUpdate()
        {
            if (!_showItems)
                return;

            Player.usingInterface = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnGUI()
        {
            if (!_showItems)
                return;
            _window = GUI.Window(24201, _window, DrawItemWindow, "Range items - all server-authoritative");
        }

        private void DrawItemWindow(int id)
        {
            GUILayout.Label("Search by item name, type, or numeric ID. Ammo is granted as a full stack.");
            GUI.SetNextControlName("RangeItemSearch");
            _search = GUILayout.TextField(_search ?? string.Empty);
            var needle = (_search ?? string.Empty).Trim();
            var visible = (_items ?? new List<ItemChoice>())
                .Where(item => needle.Length == 0 || item.SearchText.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            GUILayout.Label(visible.Count + " items - click one to add it to your inventory");

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(525f));
            foreach (var item in visible)
            {
                var quantity = item.Type == Pickup.WeaponType.Ammo ? (byte)255 : (byte)1;
                if (GUILayout.Button(item.Name + "  [" + item.Id + "]  " + item.Type))
                    RequestItem(item.Id, quantity);
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Close (" + _itemMenuKey.Value + ")"))
                CloseItemMenu();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void OpenItemMenu()
        {
            _showItems = true;
            _savedUsingInterface = Player.usingInterface;
            _hasSavedUsingInterface = true;
            Player.usingInterface = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EnsureItems();
        }

        private void CloseItemMenu()
        {
            _showItems = false;
            if (_hasSavedUsingInterface)
                Player.usingInterface = _savedUsingInterface;
            _hasSavedUsingInterface = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void EnsureItems()
        {
            if (_items != null)
                return;
            try
            {
                _items = LootDatabase.Instance.GetPickups()
                    .Where(entry => entry.pickup != null)
                    .Select(entry => new ItemChoice(entry.pickup))
                    .GroupBy(item => item.Id)
                    .Select(group => group.First())
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError("[RangeMap] Could not build item catalog: " + ex);
                _items = new List<ItemChoice>();
            }
        }

        private void RequestItem(int itemId, byte quantity)
        {
            try
            {
                ServerConnector.Instance?.SendMessageToServer((EventCode)RangeProtocol.EventCode,
                    RangeProtocol.CreateGiveItem(itemId, quantity), true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[RangeMap] Item request failed: " + ex.Message);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!RangeServerAccepted || !string.Equals(scene.name, "WilhelmTest", StringComparison.Ordinal))
                return;

            RangeSceneActive = true;
            if (_showItems)
                CloseItemMenu();
            _items = null;

            try
            {
                var spawnAction = UnityEngine.Object.FindObjectOfType<GM_SpawnPlayerAction>();
                if (spawnAction != null)
                    UnityEngine.Object.DestroyImmediate(spawnAction.gameObject);

                // WilhelmTest ships with its own offline local player. Remove it before the
                // network prefab creates the authoritative local and remote player objects.
                foreach (var offlinePlayer in UnityEngine.Object.FindObjectsOfType<Player>())
                    UnityEngine.Object.DestroyImmediate(offlinePlayer.gameObject);

                if (PhotonServerHandler.instance == null)
                {
                    var networkPrefab = TABGGameModeObjectDataBase.Instance.GetNetworkGameObject();
                    if (networkPrefab == null)
                        throw new InvalidOperationException("TABG network client prefab is missing.");
                    UnityEngine.Object.Instantiate(networkPrefab);
                }

                Logger.LogInfo("[RangeMap] Loaded WilhelmTest and started the multiplayer network handler. Press F6 for all items.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[RangeMap] Range network bootstrap failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            if (_showItems)
                CloseItemMenu();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _harmony?.UnpatchSelf();
            RangeServerAccepted = false;
            RangeSceneActive = false;
            Instance = null;
        }

        private sealed class ItemChoice
        {
            internal readonly int Id;
            internal readonly string Name;
            internal readonly Pickup.WeaponType Type;
            internal readonly string SearchText;

            internal ItemChoice(Pickup pickup)
            {
                Id = pickup.m_itemIndex;
                Name = string.IsNullOrWhiteSpace(pickup.itemName) ? pickup.gameObject.name : pickup.itemName;
                Type = pickup.weaponType;
                SearchText = Name + " " + Type + " " + Id;
            }
        }
    }

    [HarmonyPatch(typeof(ServerConnector), "SendInitRequest")]
    internal static class RangeHelloPatch
    {
        private static void Prefix(ServerConnector __instance)
        {
            RangeMapClientPlugin.RangeServerAccepted = false;
            __instance.SendMessageToServer((EventCode)RangeProtocol.EventCode, RangeProtocol.CreateHello(), true);
        }
    }

    [HarmonyPatch(typeof(ServerConnector), "OnEvent")]
    internal static class RangeAcceptedPatch
    {
        private static bool Prefix(ClientPackage clientPackage)
        {
            if ((byte)clientPackage.Code != RangeProtocol.EventCode)
                return true;

            byte operation;
            int itemId;
            byte quantity;
            if (RangeProtocol.TryRead(clientPackage.Buffer, out operation, out itemId, out quantity) && operation == RangeProtocol.Accepted)
            {
                RangeMapClientPlugin.RangeServerAccepted = true;
                RangeMapClientPlugin.Instance?.LogRangeInfo("[RangeMap] Server handshake accepted.");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ServerConnector), "LoadGameScene")]
    internal static class RangeSceneRedirectPatch
    {
        private static bool Prefix(GameMode gameMode)
        {
            if (gameMode != GameMode.Test || !RangeMapClientPlugin.RangeServerAccepted)
                return true;
            RangeMapClientPlugin.RangeSceneActive = true;
            SceneManager.LoadScene("WilhelmTest");
            return false;
        }
    }
}
