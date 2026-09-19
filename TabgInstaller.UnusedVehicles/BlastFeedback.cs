using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    internal static class BlastFeedback
    {
        private sealed class Blast
        {
            internal TABGPlayerServer Owner;internal Vector3 Position;internal bool Missile;internal float Expires;
            internal BlastConfirmationWindow Confirmations;
            internal readonly HashSet<int> ServerDamagedVehicles=new HashSet<int>();
        }
        private static readonly Dictionary<long,Blast> Recent=new Dictionary<long,Blast>();
        private static GameRoom _room;
        private static long Key(bool missile,int id)=>((long)id<<1)|(missile?1L:0L);
        internal static void Register(ServerClient server,TABGPlayerServer owner,Vector3 position,bool missile,int id)
        {
            if(_room!=server.GameRoomReference){Recent.Clear();_room=server.GameRoomReference;}
            var expired=new List<long>();foreach(var p in Recent)if(p.Value.Expires<Time.unscaledTime)expired.Add(p.Key);
            foreach(long key in expired)Recent.Remove(key);
            if(id<=0)return;
            Recent[Key(missile,id)]=new Blast{Owner=owner,Position=position,Missile=missile,Expires=Time.unscaledTime+5,Confirmations=new BlastConfirmationWindow(owner.PlayerIndex,Time.unscaledTime)};
        }
        internal static void MarkBotVehicle(bool missile,int id,int car)
        {if(Recent.TryGetValue(Key(missile,id),out var blast))blast.ServerDamagedVehicles.Add(car);}
        internal static void Confirm(ServerClient server,byte attacker,bool missile,int id,bool vehicle,int target,Vector3 position,float amount)
        {
            if(server?.GameRoomReference!=_room || !(amount>0) || float.IsInfinity(amount) ||
                !Recent.TryGetValue(Key(missile,id),out var blast) || Time.unscaledTime>blast.Expires || blast.Owner.PlayerIndex!=attacker ||
                blast.Owner.Bot || !_room.Players.Contains(blast.Owner) || (!vehicle && target==attacker))return;
            float radius=(missile?BotBlastRules.HandRadius:BotBlastRules.DynamiteRadius)+(vehicle?15:3);
            if((position-blast.Position).sqrMagnitude>radius*radius || !blast.Confirmations.Accept(attacker,vehicle,target,amount,Time.unscaledTime))return;
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.BlastConfirmed,w=>
            {w.Write(missile);w.Write(vehicle);w.Write(amount);w.Write(id);}),new[]{attacker},true,false);
            Debug.Log($"[HitConfirm] {(missile?"missile":"FPV")} #{id}: owner={attacker}, {(vehicle?"car":"player")}={target}, HP lost={amount:0.0}");
        }
        private sealed class DamageState
        {
            internal byte Attacker;internal bool Missile;internal int Id;
            internal TABGPlayerServer Player;internal TABGCarServer Car;internal TABGCarDamagable Part;
            internal float Before;internal Vector3 Position;
        }
        [HarmonyPatch(typeof(PlayerDamageCommand),"Run")]
        internal static class PlayerDamage
        {
            [HarmonyPriority(Priority.First)]
            static void Prefix(ref byte[] __0,ServerClient __1,out DamageState __state)
            {
                __state=null;
                if(!BlastReceipt.Read(__0,out bool missile,out int id,out var native))return;
                __0=native;if(native.Length<20)return;
                var victim=__1.GameRoomReference?.Players.Find(p=>p.PlayerIndex==native[1]);
                if(victim==null)return;
                __state=new DamageState{Attacker=native[0],Missile=missile,Id=id,Player=victim,Before=victim.Health,Position=victim.PlayerPosition};
            }
            static void Postfix(ServerClient __1,DamageState __state)
            {
                var s=__state;if(s==null)return;
                Confirm(__1,s.Attacker,s.Missile,s.Id,false,s.Player.PlayerIndex,s.Position,BlastReceipt.HealthLost(s.Before,s.Player.Health));
            }
        }
        [HarmonyPatch(typeof(CarDamageCommand),"Run")]
        internal static class CarDamage
        {
            [HarmonyPriority(Priority.First)]
            static bool Prefix(ref byte[] __0,ServerClient __1,out DamageState __state)
            {
                __state=null;if(!BlastReceipt.Read(__0,out bool missile,out int id,out var native))return true;
                __0=native;if(native.Length!=10)return true;
                using(var r=new BinaryReader(new MemoryStream(native)))
                {
                    byte attacker=r.ReadByte(),part=r.ReadByte();var car=__1.GameRoomReference?.FindCar(r.ReadInt32());var damageable=car?.GetPart(part);
                    // Server already damaged bot-owned cars for this exact blast. A client visual must not repeat it.
                    if(car!=null && __1.GameRoomReference==_room && Recent.TryGetValue(Key(missile,id),out var blast) &&
                        blast.Owner.PlayerIndex==attacker && blast.ServerDamagedVehicles.Contains(car.CarIndex))return false;
                    if(damageable!=null)__state=new DamageState{Attacker=attacker,Missile=missile,Id=id,Car=car,Part=damageable,Before=damageable.Health,Position=car.CarPosition};
                }
                return true;
            }
            static void Postfix(ServerClient __1,DamageState __state)
            {
                var s=__state;if(s==null)return;
                Confirm(__1,s.Attacker,s.Missile,s.Id,true,s.Car.CarIndex,s.Position,BlastReceipt.HealthLost(s.Before,s.Part.Health));
            }
        }
    }
}
