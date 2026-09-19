using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CitrusLib;
using HarmonyLib;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;

namespace TabgInstaller.UnusedVehicles
{
    public sealed class HelicopterMissiles : MonoBehaviour
    {
        private sealed class Missile
        {
            internal int Id, Target;
            internal TABGPlayerServer Owner;
            internal TABGCarServer Car;
            internal Vector3 Position, Direction;
            internal bool Guided;
            internal float Born;
            internal readonly MissileArmor Armor = new MissileArmor();
            internal readonly Queue<Vector3> History = new Queue<Vector3>();
        }
        private static readonly Dictionary<TABGPlayerServer, MissileWeapon> Weapons = new Dictionary<TABGPlayerServer, MissileWeapon>();
        private static readonly Dictionary<TABGPlayerServer, float> AimRate = new Dictionary<TABGPlayerServer, float>();
        private static readonly List<Missile> Missiles = new List<Missile>();
        private static GameRoom _room;
        private static int _serial;
        private float _nextSend;
        internal static void Reset(GameRoom room)
        {
            Missiles.Clear(); Weapons.Clear(); AimRate.Clear(); _room = room;
        }
        private static bool Alive(TABGPlayerServer p) => p != null && !p.IsDead && p.Health > 0;
        private static bool Eligible(TABGPlayerServer p) => Alive(p) && !p.IsDowned && p.IsInsideCar && p.CurrentCar != null && MissileRules.IsHeli(p.CurrentCar.CarName);
        private static bool Enemy(TABGPlayerServer p, TABGPlayerServer owner) => Alive(p) && p != owner && BotIntegration.CanAttack(owner,p) && (p.CurrentCar == null || p.CurrentCar != owner.CurrentCar);
        private static Vector3 Read(BinaryReader r) => new Vector3(VehicleProtocol.ReadFinite(r), VehicleProtocol.ReadFinite(r), VehicleProtocol.ReadFinite(r));
        private static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static MissilePoint P(Vector3 v) => new MissilePoint(v.x, v.y, v.z);
        private static Vector3 V(MissilePoint p) => new Vector3(p.X, p.Y, p.Z);
        private static MissileWeapon Weapon(TABGPlayerServer p)
        {
            if (!Weapons.TryGetValue(p, out var weapon)) Weapons[p] = weapon = new MissileWeapon();
            return weapon;
        }
        private static bool Ignored(Collider c, TABGCarServer car, TABGCarServer targetCar=null)
        {
            var proxy = c.GetComponentInParent<ServerNetworkVehicle>();
            if(proxy)return true; // All debug proxies are replaced with prefab hull sweeps.
            foreach (var player in _room.Players)
                if (player.PlayerObject && c.transform.IsChildOf(player.PlayerObject.transform)) return true;
            return false;
        }
        private static float WallDistance(Vector3 start, Vector3 direction, float distance, TABGCarServer car, float radius = .12f, TABGCarServer targetCar=null)
        {
            float nearest = distance;
            foreach (var hit in Physics.SphereCastAll(start, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignored(hit.collider, car,targetCar)) nearest = Mathf.Min(nearest, hit.distance);
            if(_room.Cars!=null)foreach(var other in _room.Cars)
            {
                if(other==car || other==targetCar)continue;
                if(VehicleGeometry.Get(other.CarTypeIdentifier).Hit(start,start+direction*distance,other.CarPosition,other.CarRotation,radius,out float t))
                    nearest=Mathf.Min(nearest,distance*t);
            }
            return nearest;
        }
        private static bool TargetPoint(int id,TABGPlayerServer owner,out Vector3 point,out TABGCarServer car)
        {
            point=Vector3.zero;car=null;
            if(MissileRules.IsVehicleTarget(id))
            {
                car=_room.FindCar(MissileRules.CarIndex(id));
                if(car==null || car==owner.CurrentCar)return false;
                point=VehicleGeometry.Get(car.CarTypeIdentifier).Aim(car.CarPosition,car.CarRotation);return true;
            }
            var player=_room.Players.Find(p=>p.PlayerIndex==id);
            if(!Enemy(player,owner))return false;
            point=player.PlayerPosition;car=player.CurrentCar;return true;
        }
        private static bool CanLock(TABGPlayerServer p, int id, Vector3 direction)
        {
            if(MissileRules.IsVehicleTarget(id))
            {
                var vehicle=_room.FindCar(MissileRules.CarIndex(id));
                if(vehicle==null || vehicle==p.CurrentCar)return false;
                var origin=p.PlayerPosition+Vector3.up*.5f;
                foreach(var aim in VehicleGeometry.Get(vehicle.CarTypeIdentifier).AimPoints(origin,direction,vehicle.CarPosition,vehicle.CarRotation))
                {
                    var difference=aim-origin;
                    if(difference.magnitude<=MissileRules.Range && Vector3.Dot(difference.normalized,direction)>.95f &&
                        WallDistance(origin,difference.normalized,difference.magnitude,p.CurrentCar,.12f,vehicle)>=difference.magnitude-.5f)return true;
                }
                return false;
            }
            if(!TargetPoint(id,p,out Vector3 point,out var targetCar))return false;
            Vector3 start=p.PlayerPosition+Vector3.up*.5f,delta=point-start;
            return delta.magnitude<=MissileRules.Range && Vector3.Dot(delta.normalized,direction)>.965f &&
                WallDistance(start,delta.normalized,delta.magnitude,p.CurrentCar,.12f,targetCar)>=delta.magnitude-.5f;
        }
        internal static void Receive(byte kind, BinaryReader reader, TABGPlayerServer player, ServerClient server)
        {
            if (_room != server.GameRoomReference) Reset(server.GameRoomReference);
            float now = Time.unscaledTime;
            if (kind == VehicleProtocol.MissileHit)
            {
                int id = reader.ReadInt32(), bullet = reader.ReadInt32();
                Vector3 point = Read(reader);
                if (reader.BaseStream.Position != reader.BaseStream.Length || !Alive(player)) return;
                var missile = Missiles.Find(m => m.Id == id);
                if (missile == null || Vector3.Distance(player.PlayerPosition, point) > 450) return;
                // Modded clients report only bullets they own. Check recent authoritative positions
                // (up to 0.35 s latency), cover, and duplicate projectile IDs before accepting a hit.
                bool close = Vector3.Distance(missile.Position, point) < 2;
                foreach (var past in missile.History) close |= Vector3.Distance(past, point) < 2;
                Vector3 delta = point - (player.PlayerPosition + Vector3.up * .5f);
                if (!close || WallDistance(player.PlayerPosition + Vector3.up * .5f, delta.normalized, delta.magnitude, player.CurrentCar) < delta.magnitude - 1) return;
                if (missile.Armor.Hit(player.PlayerIndex, bullet))
                {
                    Debug.Log($"[HeliMissiles] Missile {id} intercepted: {missile.Armor.Hits}/3 hits");
                    if (missile.Armor.Destroyed) Remove(server, missile, 1);
                }
                return;
            }
            int targetId = reader.ReadInt32(); Vector3 direction = Read(reader);
            bool guided = kind == VehicleProtocol.MissileAim || reader.ReadBoolean();
            if (reader.BaseStream.Position != reader.BaseStream.Length || direction.sqrMagnitude < .9f || direction.sqrMagnitude > 1.1f) return;
            direction.Normalize();
            var weapon = Weapon(player);
            bool eligible = Eligible(player);
            bool canLock = eligible && CanLock(player, targetId, direction);
            if (kind == VehicleProtocol.MissileAim)
            {
                if (AimRate.TryGetValue(player, out float previous) && now - previous < .06f) return;
                AimRate[player] = now;
                float progress = weapon.Aim(now, targetId, canLock);
                Reply(server, player, weapon, now, progress, targetId, false);
                return;
            }
            if (!weapon.TryFire(now, eligible && (!guided || canLock), guided, targetId))
            { Reply(server, player, weapon, now, 0, targetId, false); return; }
            // Start at the occupant, sweeping every step. Ignoring only the launch vehicle avoids
            // both self-hits and teleporting a rocket through a nearby wall at launch.
            var missileNew = new Missile { Id = ++_serial, Target = guided ? targetId : -1, Owner = player, Car = player.CurrentCar,
                Position = player.PlayerPosition + Vector3.up * .6f, Direction = direction, Guided = guided, Born = now };
            Missiles.Add(missileNew);
            State(server, missileNew);
            Reply(server, player, weapon, now, 1, targetId, true);
            Debug.Log($"[HeliMissiles] Fired #{missileNew.Id} player={player.PlayerIndex} guided={guided} target={missileNew.Target}");
        }
        internal static TABGPlayerServer FindBotTarget(ServerClient server,TABGPlayerServer bot)
        {
            if (_room!=server.GameRoomReference) Reset(server.GameRoomReference);
            if(!Eligible(bot))return null;
            TABGPlayerServer best=null;float distance=220*220;
            foreach(var candidate in _room.Players)
            {
                if(!Enemy(candidate,bot) || !candidate.HasDropped)continue;
                var delta=candidate.PlayerPosition-bot.PlayerPosition;
                if(delta.sqrMagnitude>=distance || Vector3.Dot(delta.normalized,bot.CurrentCar.CarRotation*Vector3.forward)<-.2f)continue;
                int id=candidate.CurrentCar!=null?MissileRules.VehicleTarget(candidate.CurrentCar.CarIndex):candidate.PlayerIndex;
                if(CanLock(bot,id,delta.normalized)){distance=delta.sqrMagnitude;best=candidate;}
            }
            return best;
        }
        internal static void BotFire(ServerClient server,TABGPlayerServer bot,TABGPlayerServer target)
        {
            if (_room!=server.GameRoomReference) Reset(server.GameRoomReference);
            if(!Eligible(bot) || target==null || !Enemy(target,bot))return;
            float now=Time.unscaledTime;var direction=(target.PlayerPosition-bot.PlayerPosition).normalized;
            int id=target.CurrentCar!=null?MissileRules.VehicleTarget(target.CurrentCar.CarIndex):target.PlayerIndex;
            var weapon=Weapon(bot);bool clear=CanLock(bot,id,direction);
            weapon.Aim(now,id,clear);
            if(!weapon.TryFire(now,clear,true,id))return;
            var missile=new Missile{Id=++_serial,Target=id,Owner=bot,Car=bot.CurrentCar,Position=bot.PlayerPosition+Vector3.up*.6f,Direction=direction,Guided=true,Born=now};
            Missiles.Add(missile);State(server,missile);
            Debug.Log($"[BotVehicles] {bot.PlayerName} fired guided missile #{missile.Id}, target={id}");
        }
        private static void Reply(ServerClient server, TABGPlayerServer p, MissileWeapon weapon, float now, float progress, int target, bool fired)
        {
            var data = VehicleProtocol.Message(VehicleProtocol.MissileReply, w => { w.Write(weapon.Remaining(now)); w.Write(progress); w.Write(target); w.Write(fired); });
            server.SendMessageToClients((EventCode)VehicleProtocol.Event, data, new[] { p.PlayerIndex }, true, false);
        }
        private static void Send(ServerClient server, byte kind, Action<BinaryWriter> write)
            => server.SendMessageToClients((EventCode)VehicleProtocol.Event, VehicleProtocol.Message(kind, write), _room.Players.Select(p => p.PlayerIndex).ToArray(), true, false);
        private static void State(ServerClient server, Missile m)
        {
            Send(server, VehicleProtocol.MissileState, w => { w.Write(m.Id); Write(w, m.Position); Write(w, m.Direction); w.Write(m.Guided); w.Write(m.Owner.PlayerIndex); w.Write(m.Target); w.Write(Time.unscaledTime - m.Born); });
        }
        private static void Remove(ServerClient server, Missile m, byte reason)
        {
            if(!Missiles.Remove(m))return;
            if(reason==2){BlastFeedback.Register(server,m.Owner,m.Position,true,m.Id);BotExplosionDamage.Apply(server,m.Owner,m.Position,true,m.Id);}
            Send(server, VehicleProtocol.MissileRemove, w => { w.Write(m.Id); Write(w, m.Position); w.Write(reason); w.Write(m.Owner.PlayerIndex); });
        }
        private void FixedUpdate()
        {
            var server = Citrus.World;
            if (server?.GameRoomReference == null) return;
            if (_room != server.GameRoomReference) Reset(server.GameRoomReference);
            if(_room.Players==null || _room.Cars==null){Missiles.Clear();return;}
            float now = Time.unscaledTime;
            bool send = now >= _nextSend;
            if (send) _nextSend = now + .05f;
            foreach (var missile in Missiles.ToArray())
            {
                if (now - missile.Born >= MissileRules.FlightLifetime(missile.Guided) || !_room.Players.Contains(missile.Owner)) { Remove(server, missile, 0); continue; }
                float speed = missile.Guided ? MissileRules.GuidedSpeed : MissileRules.StraightSpeed;
                if (missile.Guided && TargetPoint(missile.Target,missile.Owner,out Vector3 targetPoint,out var targetCar))
                {
                    if(targetCar!=null)
                    {
                        float best=float.PositiveInfinity;
                        foreach(var aim in VehicleGeometry.Get(targetCar.CarTypeIdentifier).AimPoints(missile.Position,missile.Direction,targetCar.CarPosition,targetCar.CarRotation))
                        {
                            float distanceTo=(aim-missile.Position).sqrMagnitude;
                            if(distanceTo<best){best=distanceTo;targetPoint=aim;}
                        }
                    }
                    Vector3 desired = targetPoint - missile.Position;
                    // Cover breaks tracking permanently; the rocket then continues on its last heading.
                    if (WallDistance(missile.Position, desired.normalized, desired.magnitude, missile.Car,.12f,targetCar) < desired.magnitude - .5f) missile.Target = -1;
                    else missile.Direction = V(MissilePoint.Turn(P(missile.Direction), P(desired), MissileRules.TurnRadians * Time.fixedDeltaTime));
                }
                float distance = speed * Time.fixedDeltaTime;
                float wall = WallDistance(missile.Position, missile.Direction, distance, missile.Car);
                var end = missile.Position + missile.Direction * wall;
                TABGPlayerServer victim = null; float first = 1;
                foreach (var p in _room.Players)
                {
                    if (!Enemy(p, missile.Owner) || p.CurrentCar == missile.Car) continue;
                    // Two overlapping spheres cover a standing/crouching player's torso and legs.
                    for (int h = 0; h < 2; h++)
                        if (MissilePoint.SegmentHit(P(missile.Position), P(end), P(p.PlayerPosition + Vector3.down * h * .65f), .85f, out float fraction) && fraction <= first)
                        { first = fraction; victim = p; }
                }
                missile.Position = Vector3.Lerp(missile.Position, end, first);
                missile.History.Enqueue(missile.Position);
                while (missile.History.Count > 18) missile.History.Dequeue();
                if (victim != null)
                {
                    // The native grenade explosion applies area damage exactly once on clients.
                    Debug.Log($"[HeliMissiles] Missile #{missile.Id} impacts player={victim.PlayerIndex}; hand-grenade blast, damage=40");
                    Remove(server, missile, 2);
                }
                else if (wall < distance - .001f) Remove(server, missile, 2);
                else if (send) State(server, missile);
            }
        }
    }
}
