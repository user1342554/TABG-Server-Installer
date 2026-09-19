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
    public sealed class ServerHoverTaxi : MonoBehaviour
    {
        private sealed class State
        {
            internal TABGCarServer Car;internal TABGPlayerServer Driver;internal Vector3 Velocity;
            internal RoadFollower Route;internal readonly HoverBoost Boost=new HoverBoost();
            internal float Ground,GroundAt,SendAt,ProbeAt,HopUntil,HopY,Blocked,RequestAt;
            internal byte Status;
        }
        private static readonly Dictionary<int,State> States=new Dictionary<int,State>();
        private static GameRoom _room;private float _next;
        private static bool Taxi(TABGCarServer car)=>car!=null && VehicleBalance.IsTaxi(car.CarName);
        private static void Reset(GameRoom room){_room=room;States.Clear();}
        private static TABGPlayerServer Driver(TABGCarServer car)
            =>Enumerable.Range(0,car.NumberOfSeats).Select(car.GetSeat).FirstOrDefault(s=>s!=null && s.DriverSeat)?.Occupant;
        private static State Get(TABGCarServer car)
        {
            if(!States.TryGetValue(car.CarIndex,out var state))States[car.CarIndex]=state=new State{Car=car,Ground=car.CarPosition.y-2};
            car.RemoveTemporaryOwner(); // Revoke native temporary physics ownership before any server motion.
            var driver=Driver(car);
            if(state.Driver!=driver){state.Driver=driver;state.Route=null;state.Boost.Stop();state.Status=0;state.HopUntil=0;}
            return state;
        }
        // Native CarUpdateCommand accepts a passenger's car position too. Only actual drivers
        // may publish normal cars; hover motion is exclusively produced below on the server.
        [HarmonyPatch(typeof(CarUpdateCommand),"Run")]
        internal static class MotionAuthority
        {
            static bool Prefix(byte[] __0,ServerClient __1)
            {
                if(__0==null || __0.Length==0)return false;
                var player=__1.GameRoomReference?.Players.Find(p=>p.PlayerIndex==__0[0]);
                return player!=null && player.IsDriving && player.CurrentSeat!=null && player.CurrentSeat.DriverSeat && !Taxi(player.CurrentCar);
            }
        }
        [HarmonyPatch(typeof(CarTemporaryUpdateCommand),"Run")]
        internal static class TemporaryMotionAuthority
        {
            static bool Prefix(byte[] __0,ServerClient __1)
            {
                // Native format: player byte, position (12), quaternion (1 or 7), car id (4).
                if(__0==null || __0.Length<18)return false;
                int offset=__0[13]<4?20:14;
                if(__0.Length!=offset+4)return false;
                var car=__1.GameRoomReference?.FindCar(System.BitConverter.ToInt32(__0,offset));
                return car!=null && (!Taxi(car) || (!States.ContainsKey(car.CarIndex) && Driver(car)==null));
            }
        }
        internal static void Receive(byte kind,BinaryReader r,TABGPlayerServer player,ServerClient server)
        {
            if(_room!=server.GameRoomReference)Reset(server.GameRoomReference);
            var car=player.CurrentCar;
            if(!Taxi(car) || player.IsDead || player.IsDowned || !player.IsDriving || Driver(car)!=player)return;
            var state=Get(car);float now=Time.unscaledTime;
            if(kind==VehicleProtocol.TaxiBoost)
            {
                if(r.BaseStream.Position!=r.BaseStream.Length)return;
                if(state.Boost.TryStart(now,true,state.Route!=null && Vector3.ProjectOnPlane(state.Velocity,Vector3.up).sqrMagnitude>1))state.SendAt=0;
                return;
            }
            bool active=r.ReadBoolean();Vector3 goal=Vector3.zero;
            if(active)goal=new Vector3(VehicleProtocol.ReadFinite(r),VehicleProtocol.ReadFinite(r),VehicleProtocol.ReadFinite(r));
            if(r.BaseStream.Position!=r.BaseStream.Length)return;
            if(!active){state.Route=null;state.Boost.Stop();state.Status=0;state.HopUntil=0;return;}
            if(now<state.RequestAt || Vector3.Distance(car.CarPosition,goal)>10000)return;
            state.RequestAt=now+.4f;
            var route=RoadNetwork.Route(car.CarPosition,goal);
            state.Route=null;state.Blocked=0;state.HopUntil=0;state.GroundAt=0;
            if(route==null || route.Count<2){state.Status=5;return;}
            var points=new List<RoadPosition>{RoadNetwork.Point(car.CarPosition)};
            foreach(var p in route)points.Add(RoadNetwork.Point(p));
            state.Route=new RoadFollower(points);state.Status=1;
            Debug.Log($"[HoverServer] Driver={player.PlayerIndex}, car={car.CarIndex}, route={route.Count}, destination={goal}");
        }
        private static bool World(Collider collider)
        {
            if(collider.GetComponentInParent<ServerNetworkVehicle>() || collider.attachedRigidbody)return false;
            foreach(var p in _room.Players)if(p.PlayerObject && collider.transform.IsChildOf(p.PlayerObject.transform))return false;
            return true;
        }
        private static bool Ground(State s,Vector3 position,out float height)
        {
            // Probe from the road/previous ground, never from a repeatedly lifted vehicle or passenger.
            float reference=s.Route!=null?s.Route.PointAhead(0).Y:s.Ground;
            float best=float.PositiveInfinity;height=s.Ground;
            foreach(var h in Physics.RaycastAll(new Vector3(position.x,reference+6,position.z),Vector3.down,1000,~0,QueryTriggerInteraction.Ignore))
                if(World(h.collider) && h.normal.y>.55f && h.distance<best){height=h.point.y;best=h.distance;}
            return !float.IsPositiveInfinity(best);
        }
        private static bool Blocked(Vector3 from,Vector3 to,out RaycastHit nearest)
        {
            nearest=default(RaycastHit);var delta=to-from;float best=float.PositiveInfinity;
            if(delta.sqrMagnitude<.001f)return false;
            foreach(var h in Physics.SphereCastAll(from,1.1f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(World(h.collider) && h.normal.y<.7f && h.distance<best){nearest=h;best=h.distance;}
            return !float.IsPositiveInfinity(best);
        }
        private void Update()
        {
            float now=Time.unscaledTime;if(now<_next)return;float dt=.05f;_next=now+dt;
            var server=Citrus.World;var room=server?.GameRoomReference;if(room?.Cars==null || room.Players==null)return;
            if(_room!=room)Reset(room);
            foreach(var car in room.Cars)
                if(Taxi(car) && Driver(car)!=null && !Driver(car).Bot)Get(car);
            foreach(var pair in States.ToArray())
            {
                var s=pair.Value;var car=s.Car;
                if(!room.Cars.Contains(car) || Driver(car)?.Bot==true){States.Remove(pair.Key);continue;}
                Get(car);
                if(s.Driver==null || s.Driver.IsDead){s.Route=null;s.Boost.Stop();s.Status=0;s.HopUntil=0;}
                if(now>=s.GroundAt)
                {
                    s.GroundAt=now+.2f;
                    if(Ground(s,car.CarPosition,out float ground))s.Ground=ground;
                    else{s.Route=null;s.Status=3;}
                }
                Vector3 flat=Vector3.ProjectOnPlane(s.Velocity,Vector3.up),desired=Vector3.zero;
                if(s.Route!=null)
                {
                    float speed=VehicleBalance.RoadSpeed*(s.Boost.Active(now)?HoverBoost.SpeedMultiplier:1);
                    desired=RoadNetwork.Vector(s.Route.Step(RoadNetwork.Point(car.CarPosition),RoadNetwork.Point(flat),speed,s.Boost.Active(now)?45:12,dt));
                    if(s.Route.Arrived){s.Route=null;s.Boost.Stop();s.Status=4;}
                }
                float normal=s.Ground+Mathf.Clamp(VehicleGeometry.Get(car.CarTypeIdentifier).GroundClearance+.5f,1.2f,4);
                if(s.Route!=null && desired.sqrMagnitude>1 && now>=s.ProbeAt)
                {
                    s.ProbeAt=now+.2f;
                    float look=Mathf.Max(8,flat.magnitude*1.1f);
                    var ahead=RoadNetwork.Vector(s.Route.PointAhead(look));ahead.y=car.CarPosition.y;
                    if(Blocked(car.CarPosition,ahead,out var obstacle))
                    {
                        float top=obstacle.collider.bounds.max.y+2;
                        if(top<=HoverClearance.Ceiling(normal) && !Blocked(new Vector3(car.CarPosition.x,top,car.CarPosition.z),new Vector3(ahead.x,top,ahead.z),out _))
                        {s.HopY=top;s.HopUntil=now+1.2f;s.Blocked=0;s.Status=2;}
                        else{s.Blocked+=.2f;s.Status=3;}
                    }
                    else{s.Blocked=0;if(now>=s.HopUntil)s.Status=1;}
                }
                float target=now<s.HopUntil?HoverClearance.Target(normal,s.HopY):normal;
                if(s.Status==3 || target-car.CarPosition.y>1.4f)desired=Vector3.zero;
                flat=Vector3.MoveTowards(flat,desired,(s.Boost.Active(now)?45:20)*dt);
                float vertical=Mathf.MoveTowards(s.Velocity.y,Mathf.Clamp((target-car.CarPosition.y)*3,-6,8),20*dt);
                s.Velocity=new Vector3(flat.x,vertical,flat.z);var step=s.Velocity*dt;
                if(Blocked(car.CarPosition,car.CarPosition+step,out _)){s.Velocity=Vector3.zero;step=Vector3.zero;}
                if(s.Blocked>10){s.Route=null;s.Status=3;s.Boost.Stop();}
                car.UpdatePosition(car.CarPosition+step);
                if(flat.sqrMagnitude>1)car.UpdateRotation(Quaternion.RotateTowards(car.CarRotation,Quaternion.LookRotation(flat),135*dt));
                car.UpdateInput(Vector3.zero);car.UpdateDrivingState(CarDrivingState.None);car.WasChanged();
                foreach(var seat in Enumerable.Range(0,car.NumberOfSeats).Select(car.GetSeat))
                    if(seat?.Occupant!=null)seat.Occupant.UpdatePosition(car.CarPosition+Vector3.up);
                if(now>=s.SendAt){s.SendAt=now+.1f;Send(server,s,now);}
            }
        }
        private static void Send(ServerClient server,State s,float now)
        {
            var recipients=_room.Players.Where(p=>!p.Bot).Select(p=>p.PlayerIndex).ToArray();var car=s.Car;
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.AircraftFall,w=>
            {w.Write(car.CarIndex);w.Write(true);FpvWrite(w,car.CarPosition);FpvWrite(w,car.CarRotation.eulerAngles);}),recipients,false,false);
            if(s.Driver!=null)server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.TaxiStatus,w=>
            {w.Write(car.CarIndex);w.Write(s.Status);w.Write(s.Boost.Remaining(now));w.Write(s.Boost.CooldownRemaining(now));}),new[]{s.Driver.PlayerIndex},false,false);
        }
        private static void FpvWrite(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
    }
}
