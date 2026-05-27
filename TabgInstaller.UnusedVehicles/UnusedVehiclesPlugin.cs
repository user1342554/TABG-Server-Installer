using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using CitrusLib;

namespace TabgInstaller.UnusedVehicles
{
    [BepInPlugin("tabginstaller.unusedvehicles", "TABG Unused Vehicles Revival", "1.0.0")]
    [BepInDependency("com.cyrusthelesser.citruslib", BepInDependency.DependencyFlags.SoftDependency)]
    public class UnusedVehiclesPlugin : BaseUnityPlugin
    {
        internal static Dictionary<string, int> VehicleIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        internal static List<int> UnusedVehicleIndices = new List<int>();
        internal static Dictionary<string, List<Vector3>> SpawnedVehiclePositions = new Dictionary<string, List<Vector3>>(StringComparer.OrdinalIgnoreCase);

        internal static readonly HashSet<int> StandardMotorcycles = new HashSet<int> { 7, 8, 9, 10, 11, 12 };

        // Skip these — they crash the server (Gun.Awake NullRef) or are broken
        internal static readonly HashSet<string> SkipVehicles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CannonCar",        // Gun.Awake NullReferenceException
            "DeceptionBossCar", // Deception mode only, broken outside it
        };

        internal static float SpawnChance = 0.20f;
        internal static int MaxSpawns = 15;
        internal static bool EnableCommands = true;
        internal static bool EnableLegacySpawnAlias;
        internal static bool EnableHeadlessAudioPatches = true;
        private static readonly FieldInfo GameRoomServerField = typeof(GameRoom).GetField("m_server", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo PopulateSeatNetworkIndexesMethod = typeof(GameRoom).GetMethod("PopulateSeatNetworkIndexes", BindingFlags.NonPublic | BindingFlags.Instance);

        private ConfigEntry<float> _spawnChance;
        private ConfigEntry<int> _maxSpawns;
        private ConfigEntry<bool> _enableCommands;
        private ConfigEntry<bool> _enableLegacySpawnAlias;
        private ConfigEntry<string> _skipVehicles;
        private ConfigEntry<bool> _enableHeadlessAudioPatches;

        private Harmony _harmony;

        private void Awake()
        {
            _spawnChance = Config.Bind("Spawning", "SpawnChance", 0.20f, "Chance to add an unused vehicle near each normal vehicle spawn.");
            _maxSpawns = Config.Bind("Spawning", "MaxSpawns", 15, "Maximum unused vehicles to add per match.");
            _skipVehicles = Config.Bind("Spawning", "SkipVehicles", "CannonCar,DeceptionBossCar", "Comma-separated prefab names to keep disabled. Defaults skip known crash/broken vehicles.");
            _enableCommands = Config.Bind("Commands", "EnableCommands", true, "Register /vehicle, /vehicles, and /vehiclehelp commands.");
            _enableLegacySpawnAlias = Config.Bind("Commands", "EnableLegacySpawnAlias", false, "Also register legacy /spawn as an alias for /vehicle. Off by default to avoid admin command collisions.");
            _enableHeadlessAudioPatches = Config.Bind("Compatibility", "EnableHeadlessAudioPatches", true, "Disable vehicle audio hooks only on headless dedicated servers.");
            SpawnChance = Mathf.Clamp01(_spawnChance.Value);
            MaxSpawns = Mathf.Max(0, _maxSpawns.Value);
            EnableCommands = _enableCommands.Value;
            EnableLegacySpawnAlias = _enableLegacySpawnAlias.Value;
            EnableHeadlessAudioPatches = _enableHeadlessAudioPatches.Value;
            LoadSkipVehicles(_skipVehicles.Value);

            _harmony = new Harmony("tabginstaller.unusedvehicles");
            _harmony.PatchAll(typeof(SearchForCarsPatch));
            PatchHeadlessAudioHooks();
            Logger.LogInfo("[UnusedVehicles] Plugin loaded. Patch applied.");
        }

        private void Start()
        {
            // Defer discovery and command registration to Start() so Citruslib is ready
            try { DiscoverVehicles(); }
            catch (Exception ex) { Logger.LogError($"[UnusedVehicles] Discovery failed: {ex}"); }

            if (EnableCommands)
            {
                try { RegisterCommands(); }
                catch (Exception ex) { Logger.LogWarning($"[UnusedVehicles] Commands failed: {ex.Message}"); }
            }
        }

        private static void RegisterCommands()
        {
            Action<string[], TABGPlayerServer> vehicleCommand = (string[] prms, TABGPlayerServer player) =>
            {
                if (prms.Length == 0)
                {
                    string list = "Available: ";
                    foreach (var kvp in VehicleIndices)
                        if (!StandardMotorcycles.Contains(kvp.Value) && !SkipVehicles.Contains(kvp.Key))
                            list += kvp.Key + ", ";
                    Citrus.SelfParrot(player, list.TrimEnd(',', ' '));
                    return;
                }

                string search = string.Join(" ", prms);
                string matchedName = null;
                int matchedIdx = -1;
                foreach (var kvp in VehicleIndices)
                {
                    if (kvp.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchedName = kvp.Key;
                        matchedIdx = kvp.Value;
                        break;
                    }
                }

                if (matchedName == null) { Citrus.SelfParrot(player, $"No vehicle matching '{search}'."); return; }

                // Find the nearest car of this type on the server and move it to the player
                var room = Citrus.World?.GameRoomReference;
                if (room == null || room.Cars == null) { Citrus.SelfParrot(player, "Server not ready."); return; }

                Vector3 playerPos = player.PlayerPosition;
                TABGCarServer nearestCar = null;
                float nearestDist = float.MaxValue;

                foreach (var car in room.Cars)
                {
                    if (car.CarTypeIdentifier == matchedIdx)
                    {
                        float dist = Vector3.Distance(playerPos, car.CarPosition);
                        if (dist < nearestDist) { nearestCar = car; nearestDist = dist; }
                    }
                }

                if (nearestCar != null)
                {
                    // Move the nearest matching server-side vehicle to 5m in front of the player.
                    Vector3 forward = Quaternion.Euler(0, player.PlayerRotation.y, 0) * Vector3.forward;
                    Vector3 spawnPos = playerPos + forward * 5f;
                    // Raycast to find ground
                    if (Physics.Raycast(spawnPos + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 100f))
                        spawnPos = hit.point + Vector3.up * 1f;
                    else
                        spawnPos.y = playerPos.y;

                    nearestCar.UpdatePosition(spawnPos);
                    Citrus.SelfParrot(player, $"Moved nearest {matchedName} in front of you.");
                }
                else
                {
                    Citrus.SelfParrot(player, $"No spawned {matchedName} exists to move. Try another vehicle.");
                }
            };

            Citrus.AddCommand("vehicle", vehicleCommand, "UnusedVehicles", "Move nearest spawned vehicle to your position", "<name>", 2);
            if (EnableLegacySpawnAlias)
                Citrus.AddCommand("spawn", vehicleCommand, "UnusedVehicles", "Legacy alias for /vehicle; moves an existing vehicle", "<name>", 2);

            Citrus.AddCommand("vehicles", (string[] prms, TABGPlayerServer player) =>
            {
                string msg = "Vehicles: ";
                foreach (var kvp in SpawnedVehiclePositions)
                    if (kvp.Value.Count > 0) msg += $"{kvp.Key}({kvp.Value.Count}) ";
                Citrus.SelfParrot(player, msg);
            }, "UnusedVehicles", "List vehicles on map", "", 1);

            Citrus.AddCommand("vehiclehelp", (string[] prms, TABGPlayerServer player) =>
            {
                Citrus.SelfParrot(player, "=== Vehicle Commands ===");
                Citrus.SelfParrot(player, "/vehicle <name> - Move nearest spawned vehicle to you");
                Citrus.SelfParrot(player, "/vehicle - List all vehicle names");
                Citrus.SelfParrot(player, "/vehicles - List spawned vehicles on map");
                if (EnableLegacySpawnAlias)
                    Citrus.SelfParrot(player, "/spawn <name> - Legacy alias for /vehicle");
                Citrus.SelfParrot(player, "=== Admin Commands ===");
                Citrus.SelfParrot(player, "/give <id> <amount> - Give item");
                Citrus.SelfParrot(player, "/goto <name> - Teleport to player");
                Citrus.SelfParrot(player, "/bring <name> - Bring player to you");
                Citrus.SelfParrot(player, "/start [time] - Force start match");
                Citrus.SelfParrot(player, "/list - List all players");
                Citrus.SelfParrot(player, "/kill [name] - Kill player");
                Citrus.SelfParrot(player, "/curse <name> <id> - Give curse");
                Citrus.SelfParrot(player, "/ban <name> - Ban player");
                Citrus.SelfParrot(player, "=== Vehicle Notes ===");
                Citrus.SelfParrot(player, "Ground vehicles (Mustang, VW, Bike, BoxCar) drive like motorcycles");
                Citrus.SelfParrot(player, "Flying vehicles (Heli, UFO, Hover) need a client mod to steer");
            }, "UnusedVehicles", "Show vehicle command help", "", 1);
        }

        private static void LoadSkipVehicles(string configuredSkipVehicles)
        {
            SkipVehicles.Clear();
            if (string.IsNullOrWhiteSpace(configuredSkipVehicles))
                return;

            foreach (var name in configuredSkipVehicles.Split(','))
            {
                var trimmed = name.Trim();
                if (trimmed.Length > 0)
                    SkipVehicles.Add(trimmed);
            }
        }

        internal static void DiscoverVehicles()
        {
            var carDb = CarDatabase.Instance;
            if (carDb == null) return;

            int count = carDb.ItemCount;
            Debug.Log($"[UnusedVehicles] CarDatabase has {count} entries");

            VehicleIndices.Clear();
            UnusedVehicleIndices.Clear();

            for (int i = 0; i < count; i++)
            {
                var entry = carDb.GetDataEntry(i);
                if (entry.Index == int.MinValue || entry.prefab == null) continue;

                string name = entry.prefab.name;
                VehicleIndices[name] = i;
                Debug.Log($"[UnusedVehicles] Vehicle [{i}] = {name}");

                if (!StandardMotorcycles.Contains(i) && !SkipVehicles.Contains(name))
                {
                    UnusedVehicleIndices.Add(i);
                    Debug.Log($"[UnusedVehicles] >>> Will spawn: {name} idx={i}");
                }
                else if (SkipVehicles.Contains(name))
                {
                    Debug.Log($"[UnusedVehicles] >>> Skipping (broken): {name} idx={i}");
                }
            }
        }

        private void PatchHeadlessAudioHooks()
        {
            if (!EnableHeadlessAudioPatches)
                return;

            if (!IsHeadlessDedicatedServer())
                return;

            var prefixMethod = typeof(UnusedVehiclesPlugin).GetMethod(nameof(SkipHeadlessAudioPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            if (prefixMethod == null)
            {
                Logger.LogWarning("[UnusedVehicles] Headless audio prefix not found; audio hooks were not patched.");
                return;
            }

            var prefix = new HarmonyMethod(prefixMethod);
            PatchOptionalHeadlessAudioMethod("VehicleSoundHandler", "stopBrake", prefix);
            PatchOptionalHeadlessAudioMethod("VehicleSoundHandler", "OnDestroy", prefix);
            PatchOptionalHeadlessAudioMethod("VehicleSoundHandler", "Crash", prefix);
            PatchOptionalHeadlessAudioMethod("CollisionChecker", "Collide", prefix);
            PatchOptionalHeadlessAudioMethod("PillarSounds", "Start", prefix);
        }

        private void PatchOptionalHeadlessAudioMethod(string typeName, string methodName, HarmonyMethod prefix)
        {
            var type = AccessTools.TypeByName(typeName);
            var method = type == null ? null : AccessTools.Method(type, methodName);
            if (method == null)
            {
                Logger.LogDebug($"[UnusedVehicles] Headless audio patch target not found: {typeName}.{methodName}");
                return;
            }

            try
            {
                _harmony.Patch(method, prefix: prefix);
                Logger.LogInfo($"[UnusedVehicles] Disabled {typeName}.{methodName} on headless server.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[UnusedVehicles] Failed to patch {typeName}.{methodName}: {ex.Message}");
            }
        }

        private static bool SkipHeadlessAudioPrefix()
        {
            return !EnableHeadlessAudioPatches || !IsHeadlessDedicatedServer();
        }

        private static bool IsHeadlessDedicatedServer()
        {
            if (!Application.isBatchMode)
                return false;

            try
            {
                return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            }
            catch
            {
                return true;
            }
        }

        [HarmonyPatch(typeof(GameRoom), "SearchForCars")]
        internal static class SearchForCarsPatch
        {
            static void Postfix(GameRoom __instance)
            {
                try
                {
                    if (UnusedVehicleIndices.Count == 0) DiscoverVehicles();
                    if (UnusedVehicleIndices.Count == 0) return;

                    var cars = __instance.Cars;
                    if (cars == null || cars.Count == 0) return;

                    var carDb = CarDatabase.Instance;
                    var server = GameRoomServerField?.GetValue(__instance) as ServerClient;

                    SpawnedVehiclePositions.Clear();

                    foreach (var existingCar in cars)
                    {
                        var existingEntry = carDb.GetDataEntry(existingCar.CarTypeIdentifier);
                        if (existingEntry.prefab != null)
                        {
                            string eName = existingEntry.prefab.name;
                            if (!SpawnedVehiclePositions.ContainsKey(eName))
                                SpawnedVehiclePositions[eName] = new List<Vector3>();
                            SpawnedVehiclePositions[eName].Add(existingCar.CarPosition);
                        }
                    }

                    int addedCount = 0;
                    int totalSpawns = cars.Count;

                    for (int i = 0; i < totalSpawns && addedCount < MaxSpawns; i++)
                    {
                        if (UnityEngine.Random.value > SpawnChance) continue;

                        Vector3 basePos = cars[i].CarPosition;
                        int vehicleIdx = UnusedVehicleIndices[UnityEngine.Random.Range(0, UnusedVehicleIndices.Count)];
                        var entry = carDb.GetDataEntry(vehicleIdx);
                        if (entry.prefab == null) continue;

                        Vector3 offset = new Vector3(UnityEngine.Random.Range(-15f, 15f), 0f, UnityEngine.Random.Range(-15f, 15f));
                        Vector3 spawnPos = basePos + offset;

                        if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f))
                            spawnPos = hit.point + Vector3.up * 0.5f;

                        GameObject vehicleGO = null;
                        bool addedToServer = false;
                        string vName = entry.prefab.name;
                        // Wrap in try/catch per vehicle so one broken prefab doesn't kill all spawning.
                        try
                        {
                            vehicleGO = UnityEngine.Object.Instantiate(entry.prefab, spawnPos, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
                            var carComponent = vehicleGO.GetComponent<Car>();
                            if (carComponent == null) continue;

                            carComponent.transform.position -= carComponent.transform.position - carComponent.mainRig.position;

                            PopulateSeatNetworkIndexesMethod?.Invoke(__instance, new object[] { carComponent });

                            Seat[] seats = carComponent.GetComponentsInChildren<Seat>();
                            int carIndex = AllocateUniqueCarIndex(cars);

                            var tabgCar = new TABGCarServer(carComponent, seats, vehicleIdx, carIndex);
                            cars.Add(tabgCar);
                            tabgCar.UpdatePosition(carComponent.transform.position);
                            addedToServer = true;

                            if (!SpawnedVehiclePositions.ContainsKey(vName))
                                SpawnedVehiclePositions[vName] = new List<Vector3>();
                            SpawnedVehiclePositions[vName].Add(spawnPos);

                            if (server != null)
                            {
                                try
                                {
                                    var vis = server.DebugVisuals;
                                    if (vis?.VehicleVisualPrefab != null)
                                    {
                                        var visualGO = UnityEngine.Object.Instantiate(vis.VehicleVisualPrefab);
                                        visualGO.FetchComponent<ServerNetworkVehicle>().Init(tabgCar);
                                    }
                                }
                                catch (Exception ex) { Debug.LogWarning($"[UnusedVehicles] Vehicle visual init failed: {ex.Message}"); }
                            }

                            addedCount++;
                            Debug.Log($"[UnusedVehicles] Spawned {vName} at {spawnPos}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[UnusedVehicles] Failed to spawn vehicle idx={vehicleIdx}: {ex.Message}");
                        }
                        finally
                        {
                            if (vehicleGO != null && !addedToServer)
                                UnityEngine.Object.Destroy(vehicleGO);
                        }
                    }

                    Debug.Log($"[UnusedVehicles] Added {addedCount} unused vehicles");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnusedVehicles] SearchForCars error: {ex}");
                }
            }

            private static int AllocateUniqueCarIndex(List<TABGCarServer> cars)
            {
                var usedIndices = new HashSet<int>();
                int nextIndex = 0;
                foreach (var car in cars)
                {
                    if (car == null) continue;

                    usedIndices.Add(car.CarIndex);
                    if (car.CarIndex >= nextIndex)
                        nextIndex = car.CarIndex + 1;
                }

                while (usedIndices.Contains(nextIndex))
                    nextIndex++;

                return nextIndex;
            }
        }
    }
}
