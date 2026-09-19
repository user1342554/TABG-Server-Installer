using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CitrusLib;
using HarmonyLib;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;

namespace TabgInstaller.UnusedVehicles
{
    internal static class AircraftCombat
    {
        private static readonly ConditionalWeakTable<TABGCarServer, AircraftLife> Lives = new ConditionalWeakTable<TABGCarServer, AircraftLife>();
        private static AircraftLife Life(TABGCarServer car) => Lives.GetValue(car, _ => new AircraftLife(Time.unscaledTime));
        private static bool IsAircraft(TABGCarServer car) => car != null && VehicleBalance.IsAircraft(car.CarName);

        [HarmonyPatch(typeof(TABGCarServer), MethodType.Constructor, new[] { typeof(Car), typeof(Seat[]), typeof(int), typeof(int) })]
        internal static class InitPatch
        {
            static void Postfix(TABGCarServer __instance)
            {
                if (!IsAircraft(__instance)) return;
                var life=Life(__instance);if(MissileRules.IsHeli(__instance.CarName))life.SetGrounded(true);
                foreach (var part in __instance.DamagableParts) part.TakeDamage(life.Health);
            }
        }

        [HarmonyPatch(typeof(RequestSeatCommand), "OccupySeat")]
        internal static class StartPatch
        {
            static void Postfix(TABGCarServer __1, TABGPlayerServer __2)
            {
                if (IsAircraft(__1) && __2.IsDriving) Life(__1).Start(Time.unscaledTime);
            }
        }

        [HarmonyPatch(typeof(CarDamageCommand), "Run")]
        internal static class DamagePatch
        {
            static bool Prefix(byte[] __0, ServerClient __1)
            {
                if (__0 == null || __0.Length != 10) return true;
                using (var r = new BinaryReader(new MemoryStream(__0)))
                {
                    byte attacker = r.ReadByte(), part = r.ReadByte();
                    var car = __1.GameRoomReference?.FindCar(r.ReadInt32());
                    float damage = r.ReadSingle();
                    if (!IsAircraft(car)) return true;
                    if (car.GetPart(part) == null) return false;
                    var life = Life(car);
                    float previous = life.Health;
                    if (life.Damage(damage, Time.unscaledTime)) Detonate(car, attacker, __1, $"damage={damage:0.0}, attacker={attacker}");
                    else if (life.Health != previous) BroadcastHealth(car, life.Health, __1);
                    return false;
                }
            }
        }

        internal static void GroundState(ServerClient server,TABGCarServer car,bool grounded)
        {
            if(!MissileRules.IsHeli(car.CarName))return;
            var life=Life(car);float previous=life.Health;life.SetGrounded(grounded);
            if(life.Health!=previous)BroadcastHealth(car,life.Health,server);
        }
        internal static void Crash(BinaryReader reader, TABGPlayerServer pilot, ServerClient server)
        {
            if (reader.BaseStream.Length != 14) return;
            int index = reader.ReadInt32();
            float impact = VehicleProtocol.ReadFinite(reader);
            var car = pilot.CurrentCar;
            // Physics runs on the driving client; only that seated driver may report an impact.
            if (!pilot.IsDriving || !IsAircraft(car) || car.CarIndex != index || impact < VehicleBalance.CrashSpeed || impact > 500f) return;
            if (Life(car).Damage(300, Time.unscaledTime)) Detonate(car, pilot.PlayerIndex, server, $"pilot crash speed={impact:0.0}");
        }

        internal static bool UnoccupiedImpact(TABGCarServer car,byte attacker,float speed,ServerClient server)
        {
            if(speed<VehicleBalance.CrashSpeed || !Life(car).Damage(300,Time.unscaledTime))return false;
            Detonate(car,attacker,server,$"unoccupied crash speed={speed:0.0}");return true;
        }

        private static byte[] Raw(Action<BinaryWriter> write)
        {
            using (var s = new MemoryStream()) using (var w = new BinaryWriter(s)) { write(w); return s.ToArray(); }
        }
        private static void Send(ServerClient server, EventCode code, byte[] data) => server.SendMessageToClients(code, data, server.GameRoomReference.Players.Select(p => p.PlayerIndex).ToArray(), true, false);
        internal static void SyncMaximum(ServerClient server,TABGCarServer car)
        {
            var life=Life(car);if(life.Destroyed)return;
            Send(server,(EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.AircraftHealth,w=>
            {w.Write(car.CarIndex);w.Write(life.Health);w.Write(life.Maximum);}));
        }
        private static void BroadcastHealth(TABGCarServer car, float health, ServerClient server)
        {
            SyncMaximum(server,car);
            foreach (var part in car.DamagableParts)
            {
                part.TakeDamage(health);
                Send(server, (EventCode)17, Raw(w => { w.Write(part.Index); w.Write(car.CarIndex); w.Write(health); }));
            }
        }
        private static void Detonate(TABGCarServer car, byte attacker, ServerClient server,string reason)
        {
            var passengers = Enumerable.Range(0, car.NumberOfSeats).Select(car.GetSeat).Where(s => s?.Occupant != null).ToArray();
            // Send before removing seats/visuals, with an explicit passenger list for every client.
            Send(server, (EventCode)VehicleProtocol.Event, VehicleProtocol.Message(VehicleProtocol.Detonate, w =>
            {
                w.Write(car.CarIndex); w.Write(attacker);
                w.Write(car.CarPosition.x); w.Write(car.CarPosition.y); w.Write(car.CarPosition.z);
                w.Write((byte)passengers.Length);
                foreach (var seat in passengers) w.Write(seat.Occupant.PlayerIndex);
            }));
            foreach (var seat in passengers)
            {
                var p = seat.Occupant;
                p.UpdateSeat(null, null);
                seat.EjectOccupant();
            }
            foreach (var part in car.DamagableParts) part.TakeDamage(0);
            foreach (var tracker in UnityEngine.Object.FindObjectsOfType<ServerNetworkVehicle>())
                if (ReferenceEquals(AccessTools.Field(typeof(ServerNetworkVehicle), "m_Car").GetValue(tracker), car))
                { tracker.StopAllCoroutines(); UnityEngine.Object.Destroy(tracker.gameObject); }
            ServerChunks.Instance.RemoveVehicle(car);
            server.GameRoomReference.Cars.Remove(car);
            Debug.Log($"[VehicleBalance] Detonated {car.CarName} #{car.CarIndex}; protected occupants={passengers.Length}; reason={reason}");
        }
    }
}
