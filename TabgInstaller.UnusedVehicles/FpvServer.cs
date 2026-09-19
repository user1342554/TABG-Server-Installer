using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CitrusLib;
using HarmonyLib;
using Landfall.Network;
using TabgInstaller.FlyingControls;
using TabgInstaller.Vehicles;
using UnityEngine;

namespace TabgInstaller.UnusedVehicles
{
    public sealed class FpvServer : MonoBehaviour
    {
        private sealed class ThrowToken { internal TABGPlayerServer Player;internal int Before;internal float Time;internal Vector3 Origin; }
        private sealed class Drone
        {
            internal int Id,Thrown;internal TABGCarServer LaunchCar;internal TABGPlayerServer Owner;internal Vector3 Position;internal Quaternion Rotation;
            internal float Seen,Born;internal readonly DroneSequence Sequence=new DroneSequence();
            internal readonly MissileArmor Armor=new MissileArmor();
            internal readonly Queue<Vector3> History=new Queue<Vector3>();
        }
        private static readonly Dictionary<byte,ThrowToken> Pending=new Dictionary<byte,ThrowToken>();
        private static readonly Dictionary<byte,Drone> Drones=new Dictionary<byte,Drone>();
        private static GameRoom _room;
        private static int _id;
        internal static void Reset(GameRoom room){_room=room;Pending.Clear();Drones.Clear();}
        private static bool Alive(TABGPlayerServer p)=>p!=null && !p.IsDead && !p.IsDowned && p.Health>0;
        private static Vector3 Read(BinaryReader r)=>new Vector3(VehicleProtocol.ReadFinite(r),VehicleProtocol.ReadFinite(r),VehicleProtocol.ReadFinite(r));
        private static void Write(BinaryWriter w,Vector3 p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
        private static void Broadcast(ServerClient server,byte kind,Action<BinaryWriter> write)
            =>server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(kind,write),_room.Players.Select(p=>p.PlayerIndex).ToArray(),true,false);
        private static void Deny(ServerClient server,TABGPlayerServer p)
            =>server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.DroneDenied),new[]{p.PlayerIndex},true,false);
        [HarmonyPatch(typeof(ItemThrownCommand),"Run")]
        internal static class ThrowPatch
        {
            static void Prefix(ref byte[] __0,ServerClient __1,byte __2,out ThrowToken __state)
            {
                __state=null;
                if(__0==null || __0.Length<33)return;
                using(var r=new BinaryReader(new MemoryStream(__0)))
                {
                    r.ReadByte();int item=r.ReadInt32();int quantity=r.ReadInt32();
                    if(item!=DroneRules.ItemId || quantity<1)return;
                    var p=__1.GameRoomReference?.Players.Find(t=>t.PlayerIndex==__2);
                    if(!Alive(p) || p.IsDriving || p.HasLoot(item)<1)return;
                    // Without CountEvent vanilla throws the entire stack. Consume exactly one.
                    if(quantity!=1){__0=(byte[])__0.Clone();Array.Copy(BitConverter.GetBytes(DroneRules.ThrowQuantity(item,quantity)),0,__0,5,4);}
                    if(_room!=__1.GameRoomReference)Reset(__1.GameRoomReference);
                    __state=new ThrowToken{Player=p,Before=p.HasLoot(item),Origin=Read(r),Time=UnityEngine.Time.unscaledTime};
                }
            }
            static void Postfix(ThrowToken __state)
            {
                // Native inventory consumption must have succeeded before a drone can be launched.
                if(__state!=null && DroneRules.ConsumedOne(__state.Before,__state.Player.HasLoot(DroneRules.ItemId)))
                    Pending[__state.Player.PlayerIndex]=__state;
            }
        }
        private static bool Ignore(Collider c,TABGPlayerServer owner,TABGCarServer carrier=null)
        {
            if(c.isTrigger)return true;
            var vehicle=c.GetComponentInParent<ServerNetworkVehicle>();
            if(carrier!=null && vehicle && ReferenceEquals(AccessTools.Field(typeof(ServerNetworkVehicle),"m_Car").GetValue(vehicle),carrier))return true;
            return owner.PlayerObject && c.transform.IsChildOf(owner.PlayerObject.transform);
        }
        private static bool Collision(Vector3 start,Vector3 end,TABGPlayerServer owner,out Vector3 impact,TABGCarServer carrier=null,float radius=DroneRules.Radius)
        {
            impact=end;Vector3 delta=end-start;float first=1;bool found=false;
            if(delta.sqrMagnitude>.00001f)
                foreach(var hit in Physics.SphereCastAll(start,radius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.collider.GetComponentInParent<ServerNetworkVehicle>() && !Ignore(hit.collider,owner,carrier) && hit.distance/delta.magnitude<=first){first=hit.distance/delta.magnitude;found=true;}
            // Server vehicle objects are debug proxies. Sweep the same prefab hulls
            // used by missiles, including zero-length overlap at a client-reported impact.
            TABGCarServer struck=null;
            if(_room.Cars!=null)foreach(var car in _room.Cars)
            {
                if(car==carrier)continue;
                if(VehicleGeometry.Get(car.CarTypeIdentifier).Hit(start,end,car.CarPosition,car.CarRotation,radius,out float t) && t<=first)
                {first=t;found=true;struck=car;}
            }
            foreach(var p in _room.Players)
            {
                if(p==owner || p.IsDead || (carrier!=null && p.CurrentCar==carrier))continue;
                if(MissilePoint.SegmentHit(P(start),P(end),P(p.PlayerPosition),.8f,out float t) && t<first){first=t;found=true;struck=null;}
            }
            if(found)impact=Vector3.Lerp(start,end,first);
            if(struck!=null)Debug.Log($"[FPV] Vehicle contact #{struck.CarIndex} {struck.CarName}, point={impact}");
            return found;
        }
        private static MissilePoint P(Vector3 v)=>new MissilePoint(v.x,v.y,v.z);
        internal static void Receive(byte kind,BinaryReader r,TABGPlayerServer player,ServerClient server)
        {
            if(_room!=server.GameRoomReference)Reset(server.GameRoomReference);
            float now=Time.unscaledTime;
            if(kind==VehicleProtocol.DroneHit)
            {
                int droneId=r.ReadInt32(),bullet=r.ReadInt32();Vector3 point=Read(r);
                if(r.BaseStream.Position!=r.BaseStream.Length || !Alive(player))return;
                var hitDrone=Drones.Values.FirstOrDefault(d=>d.Id==droneId);
                if(hitDrone==null || Vector3.Distance(player.PlayerPosition,point)>450)return;
                bool close=Vector3.Distance(hitDrone.Position,point)<1.6f || hitDrone.History.Any(p=>Vector3.Distance(p,point)<1.6f);
                var start=player.PlayerPosition+Vector3.up*.5f;var delta=point-start;
                bool covered=Physics.RaycastAll(start,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                    .Any(h=>!Ignore(h.collider,player,player.CurrentCar) && h.distance<delta.magnitude-1);
                if(!close || covered)return;
                if(hitDrone.Armor.Hit(player.PlayerIndex,bullet))
                {
                    Debug.Log($"[FPV] Drone #{droneId}: {hitDrone.Armor.Hits}/3 bullet hits");
                    if(hitDrone.Armor.Destroyed)Finish(server,hitDrone,true);
                }
                return;
            }
            if(kind==VehicleProtocol.DroneLaunch)
            {
                int thrown=r.ReadInt32();Vector3 position=Read(r);var rotation=Quaternion.Euler(Read(r));
                if(r.BaseStream.Position!=r.BaseStream.Length || !DroneRules.CanControl(Alive(player),player.IsDowned,player.IsDriving,Drones.ContainsKey(player.PlayerIndex)) ||
                    !Pending.TryGetValue(player.PlayerIndex,out var token) || now-token.Time>4 || Vector3.Distance(token.Origin,position)>25)
                {Deny(server,player);return;}
                Pending.Remove(player.PlayerIndex);
                if(Collision(token.Origin,position,player,out _,player.CurrentCar)){Deny(server,player);return;}
                var drone=new Drone{Id=++_id,Thrown=thrown,Owner=player,Position=position,Rotation=rotation,Seen=now,Born=now,LaunchCar=player.CurrentCar};
                drone.History.Enqueue(position);
                Drones[player.PlayerIndex]=drone;State(server,drone,0);
                Debug.Log($"[FPV] Launch #{drone.Id}, player={player.PlayerIndex}, position={position}");
                return;
            }
            int id=r.ReadInt32();
            if(!Drones.TryGetValue(player.PlayerIndex,out var current) || id!=current.Id)return;
            if(kind==VehicleProtocol.DroneEnd)
            {
                bool crash=r.ReadBoolean();Vector3 point=Read(r);
                if(r.BaseStream.Position!=r.BaseStream.Length)return;
                if(!crash){Finish(server,current,false);return;}
                if(Vector3.Distance(current.Position,point)>5)return;
                var carrier=now-current.Born<.75f?current.LaunchCar:null;
                if(Collision(current.Position,point,player,out Vector3 impact,carrier))current.Position=impact;
                else
                {
                    bool surface=Collision(point,point,player,out _,carrier,DroneRules.Radius+.4f) ||
                        Physics.OverlapSphere(point,DroneRules.Radius+.4f,~0,QueryTriggerInteraction.Ignore)
                            .Any(c=>!c.GetComponentInParent<ServerNetworkVehicle>() && !Ignore(c,player,carrier));
                    if(!surface && !_room.Players.Any(p=>p!=player && !p.IsDead && (carrier==null || p.CurrentCar!=carrier) && Vector3.Distance(p.PlayerPosition,point)<1.5f))return;
                    current.Position=point;
                }
                Finish(server,current,true);return;
            }
            int sequence=r.ReadInt32();Vector3 next=Read(r);Quaternion nextRotation=Quaternion.Euler(Read(r));
            if(r.BaseStream.Position!=r.BaseStream.Length || !Alive(player) || player.IsDriving){Finish(server,current,false);return;}
            if(DroneRules.Remaining(current.Born,now)<=0 || Vector3.Distance(next,player.PlayerPosition)>=DroneRules.MaxRange){Finish(server,current,false);return;}
            if(!DroneRules.ValidStep(Vector3.Distance(current.Position,next),now-current.Seen) || !current.Sequence.Accept(sequence))return;
            if(Collision(current.Position,next,player,out Vector3 collision,now-current.Born<.75f?current.LaunchCar:null))
            {current.Position=collision;Finish(server,current,true);return;}
            current.Position=next;current.Rotation=nextRotation;current.Seen=now;
            current.History.Enqueue(next);while(current.History.Count>6)current.History.Dequeue();
            State(server,current,sequence);
        }
        private static void State(ServerClient server,Drone d,int sequence)
        {
            Broadcast(server,VehicleProtocol.DroneState,w=>{w.Write(d.Id);w.Write(d.Owner.PlayerIndex);w.Write(d.Thrown);w.Write(sequence);Write(w,d.Position);Write(w,d.Rotation.eulerAngles);});
        }
        private static void Finish(ServerClient server,Drone d,bool explode)
        {
            if(!d.Sequence.Finish())return;
            Drones.Remove(d.Owner.PlayerIndex);
            if(explode){BlastFeedback.Register(server,d.Owner,d.Position,false,d.Id);BotExplosionDamage.Apply(server,d.Owner,d.Position,false,d.Id);}
            Broadcast(server,VehicleProtocol.DroneFinish,w=>{w.Write(d.Id);w.Write(d.Owner.PlayerIndex);Write(w,d.Position);w.Write(explode);});
            Debug.Log($"[FPV] End #{d.Id}, explosion={explode}, position={d.Position}");
        }
        internal static void CheckBotDamage(ServerClient server,TABGPlayerServer bot)
        {
            if(_room!=server.GameRoomReference)Reset(server.GameRoomReference);
            if(Drones.Count!=0)throw new InvalidOperationException("Refusing self-check during active drone flight");
            var oldPosition=bot.PlayerPosition;float oldHealth=bot.Health;
            float damage=BotBlastRules.DynamiteDamage;
            try
            {
                bot.UpdatePosition(new Vector3(0,5000,0));bot.UpdateHealth(damage*2+10);
                var drone=new Drone{Id=-15000,Owner=bot,Position=bot.PlayerPosition};
                Drones[bot.PlayerIndex]=drone;
                Finish(server,drone,true);float after=bot.Health;
                if(Mathf.Abs(after-(damage+10))>.01f)throw new InvalidOperationException("FPV damage did not reach authoritative bot HP: "+after);
                Finish(server,drone,true);
                if(bot.Health!=after)throw new InvalidOperationException("Duplicate FPV finish applied damage twice");
                Debug.Log($"[BotCheck] PASS real FPV Finish -> native dynamite -> bot HP {damage*2+10} -> {after}; duplicate finish ignored. Synthetic empty-server check; no flight collision tested.");
            }
            finally{bot.UpdatePosition(oldPosition);bot.UpdateHealth(oldHealth);Drones.Remove(bot.PlayerIndex);}
        }
        private void Start()
        {
            Citrus.AddCommand("fpv",(args,player)=>
            {
                if(!FpvItem.Register(LootDatabase.Instance)){Citrus.SelfParrot(player,"FPV item registration failed.");return;}
                Citrus.World.GivePlayerWeapon(player.PlayerIndex,DroneRules.ItemId,1);
                Citrus.SelfParrot(player,"FPV Drone added. Select it as a grenade and throw upward; Escape exits control.");
            },"UnusedVehicles","Give one FPV Drone grenade","",2);
        }
        private void Update()
        {
            var server=Citrus.World;
            if(server?.GameRoomReference==null)return;
            if(_room!=server.GameRoomReference)Reset(server.GameRoomReference);
            if(_room.Players==null){Drones.Clear();Pending.Clear();return;}
            foreach(var d in Drones.Values.ToArray())
                if(!Alive(d.Owner) || !_room.Players.Contains(d.Owner) || Time.unscaledTime-d.Seen>DroneRules.LinkTimeout || DroneRules.Remaining(d.Born,Time.unscaledTime)<=0 || Vector3.Distance(d.Position,d.Owner.PlayerPosition)>=DroneRules.MaxRange)Finish(server,d,false);
        }
    }
}
