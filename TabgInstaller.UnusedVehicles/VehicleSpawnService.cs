using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CitrusLib;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;

namespace TabgInstaller.UnusedVehicles
{
    internal static class VehicleSpawnService
    {
        private static GameRoom _room;
        private static readonly Dictionary<byte, float> Clients = new Dictionary<byte, float>();
        private static readonly Dictionary<byte, float> LastSpawn = new Dictionary<byte, float>();
        private static readonly HashSet<int> CustomSpawns = new HashSet<int>();
        private static readonly Dictionary<int, byte> Pending = new Dictionary<int, byte>();
        internal static void Reset(GameRoom room) { _room = room; HelicopterMissiles.Reset(room); FpvServer.Reset(room); Clients.Clear(); LastSpawn.Clear(); Pending.Clear(); CustomSpawns.Clear(); }

        [HarmonyPatch(typeof(ServerClient), "HandleNetorkEvent")]
        internal static class ReceivePatch
        {
            static bool Prefix(ServerPackage networkEvent, ServerClient __instance)
            {
                if ((byte)networkEvent.Code != VehicleProtocol.Event) return true;
                try
                {
                    var room = __instance.GameRoomReference;
                    if (room != _room) Reset(room);
                    var player = room?.Players.Find(p => p.PlayerIndex == networkEvent.SenderPlayerID);
                    if (player == null) return false;
                    using (var reader = VehicleProtocol.Open(networkEvent.Buffer, out byte kind))
                    {
                        if (reader == null) return false;
                        if (kind == VehicleProtocol.Hello && reader.BaseStream.Position == reader.BaseStream.Length)
                            Clients[player.PlayerIndex] = Time.unscaledTime;
                        else if(kind==VehicleProtocol.TaxiDestination || kind==VehicleProtocol.TaxiBoost) ServerHoverTaxi.Receive(kind,reader,player,__instance);
                        else if(kind==VehicleProtocol.EjectRequest) AircraftSafety.Eject(reader,player,__instance);
                        else if (kind == VehicleProtocol.DroneLaunch || kind == VehicleProtocol.DroneMove || kind == VehicleProtocol.DroneEnd || kind == VehicleProtocol.DroneHit)
                            FpvServer.Receive(kind, reader, player, __instance);
                        else if (kind == VehicleProtocol.MissileAim || kind == VehicleProtocol.MissileFire || kind == VehicleProtocol.MissileHit)
                            HelicopterMissiles.Receive(kind, reader, player, __instance);
                        else if (kind == VehicleProtocol.Crash)
                            AircraftCombat.Crash(reader, player, __instance);
                        else if (kind == VehicleProtocol.Ack && reader.BaseStream.Length == 10)
                        {
                            int index = reader.ReadInt32();
                            if (Pending.TryGetValue(index, out byte owner) && owner == player.PlayerIndex)
                            {
                                Pending.Remove(index);
                                Citrus.SelfParrot(player, $"New vehicle #{index} spawned. Walk toward it and enter a seat.");
                                Debug.Log($"[VehicleSpawn] Client confirmed vehicle {index}");
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[VehicleSpawn] Invalid message: {ex.Message}"); }
                return false;
            }
        }

        internal static void SpawnForPlayer(TABGPlayerServer player, string name, int type, TABGPlayerServer bot = null)
        {
            var room = Citrus.World?.GameRoomReference;
            if (room?.Cars == null) { Citrus.SelfParrot(player, "Server not ready."); return; }
            if (room != _room) Reset(room);
            byte owner = player.PlayerIndex;
            if (!Clients.TryGetValue(owner, out float seen) || Time.unscaledTime - seen > 12f)
            { Citrus.SelfParrot(player, "Flying Controls 1.2 is required. Restart your updated game and wait 3 seconds."); return; }
            if (LastSpawn.TryGetValue(owner, out float last) && Time.unscaledTime - last < 3f)
            { Citrus.SelfParrot(player, "Wait 3 seconds before spawning another vehicle."); return; }
            // Bound extra admin vehicles without counting the vanilla map fleet.
            CustomSpawns.RemoveWhere(id=>room.FindCar(id)==null);
            if (CustomSpawns.Count >= 16)
            { Citrus.SelfParrot(player, "Extra vehicle limit reached (16). Destroy old vehicles before adding more."); return; }
            GameObject template = null;
            GameObject networkProxy = null;
            TABGCarServer added = null;
            try
            {
                var prefab = CarDatabase.Instance.GetDataEntry(type).prefab;
                if (prefab == null) throw new InvalidOperationException("Vehicle prefab is missing.");
                var anchor=bot??player;
                var rotation = Quaternion.Euler(0, anchor.PlayerRotation.y, 0);
                template = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 10000, 0), rotation);
                var car = template.GetComponent<Car>();
                if (car?.mainRig == null) throw new InvalidOperationException("Vehicle has no rigidbody.");
                var colliders = template.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
                if (colliders.Length == 0) throw new InvalidOperationException("Vehicle has no collision bounds.");
                var bounds = colliders[0].bounds;
                foreach (var collider in colliders) bounds.Encapsulate(collider.bounds);
                var relativeCenter = bounds.center - car.mainRig.position;
                float bottomOffset = car.mainRig.position.y - bounds.min.y;
                if (!FindSpace(room, anchor.PlayerPosition, rotation, bounds.extents, relativeCenter, bottomOffset, out Vector3 position))
                { Citrus.SelfParrot(player, "No clear, flat ground nearby. Move to an open area and try again."); return; }
                var seats = car.GetComponentsInChildren<Seat>();
                for (int i = 0; i < seats.Length; i++) seats[i].SetNetworkIndex(i);
                int index = room.Cars.Count == 0 ? 0 : room.Cars.Max(c => c.CarIndex) + 1;
                added = new TABGCarServer(car, seats, type, index);
                added.UpdatePosition(position); added.UpdateRotation(rotation);
                byte[] payload = VehicleProtocol.Message(VehicleProtocol.Spawn, w =>
                {
                    w.Write(index); w.Write(type); w.Write(owner);
                    w.Write(position.x); w.Write(position.y); w.Write(position.z); w.Write(rotation.eulerAngles.y);
                    w.Write(checked((byte)added.DamagableParts.Count));
                    foreach (var part in added.DamagableParts) { w.Write(part.Index); w.Write(part.Health); w.Write(part.PartName); }
                });
                if (payload.Length > 4096) throw new InvalidOperationException("Vehicle packet is too large.");
                // This object owns spatial registration and follows chunk changes while driving.
                // Despite its DebugVisuals location, it is required for seat and vehicle updates.
                var proxyPrefab = Citrus.World.DebugVisuals?.VehicleVisualPrefab;
                networkProxy = proxyPrefab != null
                    ? UnityEngine.Object.Instantiate(proxyPrefab)
                    : new GameObject("Network vehicle " + index);
                var tracker = networkProxy.GetComponent<ServerNetworkVehicle>() ?? networkProxy.AddComponent<ServerNetworkVehicle>();
                tracker.Init(added);
                if (ServerChunks.Instance.GetWatchers(added.ChunkData) == null)
                    throw new InvalidOperationException("Vehicle spatial registration failed.");
                room.Cars.Add(added); CustomSpawns.Add(index);
                byte[] recipients = room.Players.Where(p => Clients.TryGetValue(p.PlayerIndex, out float t) && Time.unscaledTime - t < 12f).Select(p => p.PlayerIndex).ToArray();
                Pending[index] = owner;
                Citrus.World.SendMessageToClients((EventCode)VehicleProtocol.Event, payload, recipients, true, false);
                added.GiveTemporaryOwner(owner, 3f);
                LastSpawn[owner] = Time.unscaledTime;
                if(bot!=null)BotVehicleBrain.Assign(Citrus.World,bot,added);
                Citrus.SelfParrot(player, $"Spawning NEW {name} {Vector3.Distance(anchor.PlayerPosition, position):0}m away...");
                Debug.Log($"[VehicleSpawn] Spawned NEW {name} index={index} at {position}; recipients={recipients.Length}; seats={seats.Length}");
                added = null;
                networkProxy = null;
            }
            catch (Exception ex)
            {
                if (added != null)
                {
                    room.Cars.Remove(added); Pending.Remove(added.CarIndex); CustomSpawns.Remove(added.CarIndex);
                    if (networkProxy != null) ServerChunks.Instance.RemoveVehicle(added);
                }
                if (networkProxy != null) UnityEngine.Object.Destroy(networkProxy);
                Citrus.SelfParrot(player, "Spawn failed: " + ex.Message);
                Debug.LogError($"[VehicleSpawn] Spawn failed: {ex}");
            }
            finally { if (template != null) { template.SetActive(false); UnityEngine.Object.Destroy(template); } }
        }

        private static bool FindSpace(GameRoom room, Vector3 player, Quaternion facing, Vector3 extents, Vector3 centerOffset, float bottomOffset, out Vector3 result)
        {
            float radius = Mathf.Max(extents.x, extents.z);
            foreach (float distance in new[] { Mathf.Max(8f, radius + 4f), Mathf.Max(14f, radius + 10f), Mathf.Max(22f, radius + 18f) })
            foreach (float angle in new[] { 0f, -35f, 35f, -70f, 70f })
            {
                var candidate = player + facing * Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (!Physics.Raycast(candidate + Vector3.up * 12f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.9f || hit.collider.GetComponentInParent<Car>() != null) continue;
                var position = hit.point + Vector3.up * (bottomOffset + 0.6f);
                // The headless server has data-only cars: explicitly avoid their stored positions too.
                if (room.Cars.Any(c => Vector3.Distance(c.CarPosition, position) < radius + 5f)) continue;
                var half = Vector3.Max(extents - Vector3.one * 0.05f, Vector3.one * 0.1f);
                if (Physics.CheckBox(position + centerOffset, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                bool supported = true;
                foreach (float x in new[] { -extents.x * 0.7f, extents.x * 0.7f })
                foreach (float z in new[] { -extents.z * 0.7f, extents.z * 0.7f })
                {
                    if (!Physics.Raycast(hit.point + new Vector3(x, 2f, z), Vector3.down, out RaycastHit support, 4f, ~0, QueryTriggerInteraction.Ignore)
                        || support.normal.y < 0.85f || Mathf.Abs(support.point.y - hit.point.y) > 0.8f) supported = false;
                }
                if (!supported) continue;
                result = position; return true;
            }
            result = default(Vector3); return false;
        }
    }
}
