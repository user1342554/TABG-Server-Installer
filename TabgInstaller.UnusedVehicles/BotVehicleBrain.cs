using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landfall.Network;
using TabgInstaller.FlyingControls;
using TabgInstaller.Vehicles;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    public sealed class BotVehicleBrain : MonoBehaviour
    {
        private sealed class Drive
        {
            internal TABGCarServer Wanted,Car;internal Vector3 Velocity,Goal;
            internal float Search,RouteAt,Send,Entered,Blocked,GroundAt,Ground,GoalGround,WaitUntil;
            internal bool Landing;
            internal readonly Dictionary<int,float> AvoidCars=new Dictionary<int,float>();
            internal RoadFollower Road;internal TABGPlayerServer Target;internal float Perceive,Fire;
        }
        private readonly Dictionary<TABGPlayerServer,Drive> _drivers=new Dictionary<TABGPlayerServer,Drive>();
        private static BotVehicleBrain _instance;
        internal static void Assign(ServerClient server,TABGPlayerServer bot,TABGCarServer car)
        {
            if(!_instance)return;
            if(_instance._room!=server.GameRoomReference){_instance._drivers.Clear();_instance._room=server.GameRoomReference;}
            _instance._drivers[bot]=new Drive{Wanted=car,Search=Time.unscaledTime+30};
        }
        private GameRoom _room;private ServerClient _server;private float _cleanup;
        private void Update()
        {
            if(Time.unscaledTime<_cleanup || _server==null || _room==null)return;
            _cleanup=Time.unscaledTime+1;
            foreach(var bot in _drivers.Keys.ToArray())
                if(bot.IsDead || bot.Health<=0 || !_room.Players.Contains(bot))
                {var state=_drivers[bot];if(state.Car!=null && _room.Cars.Contains(state.Car) && !Enumerable.Range(0,state.Car.NumberOfSeats).Select(state.Car.GetSeat).Any(s=>s.DriverSeat && s.Occupant!=null && s.Occupant!=bot && s.Occupant.Bot))View(_server,state.Car,false,true);_drivers.Remove(bot);}
        }
        private void Awake(){_instance=this;if(!BotIntegration.Register(this)){Destroy(this);return;}Debug.Log("[BotVehicles] Server vehicle AI ready: all drivable vehicles, aircraft and helicopters with homing missiles.");}
        private void OnDestroy(){BotIntegration.Unregister();}
        private static void Seat(ServerClient server,TABGPlayerServer bot,TABGCarServer car,TABGCarServerSeat seat,bool enter)
        {
            BotIntegration.Seat(server,bot,car,seat,enter);
        }
        private static void View(ServerClient server,TABGCarServer car,bool active,bool reliable=false)
        {
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.AircraftFall,w=>
            {w.Write(car.CarIndex);w.Write(active);var p=car.CarPosition;w.Write(p.x);w.Write(p.y);w.Write(p.z);var a=car.CarRotation.eulerAngles;w.Write(a.x);w.Write(a.y);w.Write(a.z);}),server.GameRoomReference.Players.Where(p=>!p.Bot).Select(p=>p.PlayerIndex).ToArray(),reliable,false);
        }
        private bool WorldCollider(Collider c)
        {
            if(c.GetComponentInParent<ServerNetworkVehicle>())return false;
            foreach(var p in _room.Players)if(p.PlayerObject && c.transform.IsChildOf(p.PlayerObject.transform))return false;
            return true;
        }
        private float Ground(Vector3 point,bool ahead=false)
        {
            float height=float.NegativeInfinity;
            foreach(var h in Physics.RaycastAll(point+Vector3.up*(ahead?100:2),Vector3.down,500,~0,QueryTriggerInteraction.Ignore))
                if(WorldCollider(h.collider) && h.normal.y>.5f)height=Mathf.Max(height,h.point.y);
            return float.IsNegativeInfinity(height)?point.y-2:height;
        }
        private bool Blocked(Vector3 start,Vector3 delta,float radius,out RaycastHit contact)
        {
            contact=default(RaycastHit);float distance=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(start,radius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(WorldCollider(hit.collider) && hit.distance<distance){distance=hit.distance;contact=hit;}
            return !float.IsPositiveInfinity(distance);
        }
        private bool Tick(ServerClient server,TABGPlayerServer bot,TABGPlayerServer target,ref Vector3 destination,float dt)
        {
            _server=server;
            if(_room!=server.GameRoomReference){_drivers.Clear();_room=server.GameRoomReference;}
            if(_room?.Cars==null)return false;
            if(!_drivers.TryGetValue(bot,out var state))_drivers[bot]=state=new Drive();
            float now=Time.unscaledTime;Vector3 intention=destination;
            if(bot.IsInsideCar && !bot.IsDriving && bot.CurrentCar!=null)
            {
                var ride=bot.CurrentCar;state.Car=null;state.Wanted=null;
                var driver=Enumerable.Range(0,ride.NumberOfSeats).Select(ride.GetSeat).FirstOrDefault(s=>s.DriverSeat)?.Occupant;
                if(driver==null && Mathf.Abs(ride.CarPosition.y-Ground(ride.CarPosition))<4)
                {Seat(server,bot,ride,bot.CurrentSeat,false);state.Search=now+12;return false;}
                float side=(bot.CurrentSeat.NetworkIndex%2==0?-1:1);
                bot.UpdatePosition(ride.CarPosition+ride.CarRotation*new Vector3(side*1.2f,1,-.7f));
                if(bot.PlayerObject)bot.PlayerObject.transform.position=bot.PlayerPosition;
                if(now>=state.Send){state.Send=now+.1f;BotIntegration.BroadcastPlayerUpdate(server,bot,bot.PlayerPosition);}
                if(MissileRules.IsHeli(ride.CarName))
                {
                    if(now>=state.Perceive){state.Perceive=now+.5f;state.Target=HelicopterMissiles.FindBotTarget(server,bot);}
                    if(state.Target!=null && !BotIntegration.Friends(bot,state.Target) && now>=state.Fire)
                    {state.Fire=now+.1f;HelicopterMissiles.BotFire(server,bot,state.Target);}
                }
                return true;
            }
            if(bot.IsDriving && bot.CurrentCar!=null && state.Car==null)
            {state.Car=bot.CurrentCar;state.Entered=now;state.WaitUntil=now+3;state.GroundAt=0;}

            if(state.Car!=null && (!_room.Cars.Contains(state.Car) || bot.CurrentCar!=state.Car))
            {if(_room.Cars.Contains(state.Car))View(server,state.Car,false,true);state.Car=null;state.Wanted=null;state.Search=now+12;}
            if(state.Car==null)
            {
                if(now>=state.Search)
                {
                    state.Search=now+4;state.Wanted=null;
                    float best=65*65;
                    foreach(var candidate in _room.Cars)
                    {
                        if(state.AvoidCars.TryGetValue(candidate.CarIndex,out float retry) && now<retry)continue;
                        if(!BotIntegration.TransportUseful(bot,candidate))continue;
                        if(candidate.NumberOfSeats<=0 || (candidate.DamagableParts.Count>0 && candidate.DamagableParts.All(p=>p.Health<=0)))continue;
                        var driver=Enumerable.Range(0,candidate.NumberOfSeats).Select(candidate.GetSeat).FirstOrDefault(s=>s.DriverSeat);
                        if(driver==null)continue;
                        bool alliedRide=driver.Occupant!=null && BotIntegration.Friends(bot,driver.Occupant) && Enumerable.Range(0,candidate.NumberOfSeats).Select(candidate.GetSeat).Any(s=>!s.DriverSeat && s.Occupant==null);
                        if(driver.Occupant!=null && !alliedRide)continue;
                        float d=(candidate.CarPosition-bot.PlayerPosition).sqrMagnitude;
                        if(d<best && Mathf.Abs(candidate.CarPosition.y-bot.PlayerPosition.y)<5 && (alliedRide || (destination-bot.PlayerPosition).sqrMagnitude>90*90))
                        {best=d;state.Wanted=candidate;}
                    }
                }
                if(state.Wanted==null || !_room.Cars.Contains(state.Wanted))return false;
                destination=state.Wanted.CarPosition;
                if(!BotIntegration.TransportUseful(bot,state.Wanted)){state.Wanted=null;return false;}
                if(Vector3.ProjectOnPlane(destination-bot.PlayerPosition,Vector3.up).magnitude>6)return false;
                var seats=Enumerable.Range(0,state.Wanted.NumberOfSeats).Select(state.Wanted.GetSeat).ToArray();
                var pilot=seats.FirstOrDefault(s=>s.DriverSeat);
                bool reserved=_drivers.Any(pair=>pair.Key!=bot && pair.Key.PlayerIndex<bot.PlayerIndex && !pair.Key.IsDead && BotIntegration.Friends(bot,pair.Key) && pair.Value.Wanted==state.Wanted);
                var seat=pilot!=null && pilot.Occupant==null && !reserved?pilot:
                    pilot?.Occupant!=null && BotIntegration.Friends(bot,pilot.Occupant)?seats.FirstOrDefault(s=>!s.DriverSeat && s.Occupant==null):null;
                if(seat==null){state.Search=now+1;return false;}
                Seat(server,bot,state.Wanted,seat,true);
                if(bot.CurrentCar!=state.Wanted)return false;
                if(!seat.DriverSeat){state.Wanted=null;return true;}
                destination=intention;state.WaitUntil=now+12;state.Landing=false;state.Car=state.Wanted;state.Wanted=null;state.Road=null;state.Velocity=Vector3.zero;state.Blocked=0;state.GroundAt=0;state.Entered=now;state.Goal=destination;state.Car.RemoveTemporaryOwner();View(server,state.Car,true,true);
                Debug.Log($"[BotVehicles] {bot.PlayerName} entered {state.Car.CarName} #{state.Car.CarIndex}");
            }
            var car=state.Car;
            bool aircraftHere=VehicleBalance.IsAircraft(car.CarName);
            if(!aircraftHere && (!BotIntegration.TransportUseful(bot,car) || (intention-bot.PlayerPosition).sqrMagnitude<14*14) && now-state.Entered>2)
            {Exit(server,bot,state);return false;}
            bool leftBehind=_room.Players.Any(p=>p!=bot && p.Bot && !p.IsDead && !p.IsDowned && !p.IsInsideCar && BotIntegration.Friends(bot,p) && (p.PlayerPosition-car.CarPosition).sqrMagnitude<100*100);
            if(now>=state.WaitUntil && now-state.Entered<15 && leftBehind){Exit(server,bot,state);return false;}
            bool waiting=now<state.WaitUntil && _room.Players.Any(p=>p!=bot && p.Bot && !p.IsDead && !p.IsDowned && !p.IsInsideCar && BotIntegration.Friends(bot,p) && (p.PlayerPosition-car.CarPosition).sqrMagnitude<65*65);
            if(waiting)
            {
                state.Velocity=Vector3.zero;bot.UpdatePosition(car.CarPosition+Vector3.up);
                if(now>=state.Send){state.Send=now+.15f;View(server,car,true);BotIntegration.BroadcastPlayerUpdate(server,bot,bot.PlayerPosition);}return true;
            }
            bool heli=MissileRules.IsHeli(car.CarName);
            bool aircraft=VehicleBalance.IsAircraft(car.CarName),hover=VehicleBalance.IsTaxi(car.CarName);
            float clearance=hover?2:VehicleGeometry.Get(car.CarTypeIdentifier).GroundClearance;
            if(heli && now>=state.Perceive)
            {state.Perceive=now+.6f;state.Target=HelicopterMissiles.FindBotTarget(server,bot);}
            if(heli && state.Target!=null && !state.Target.IsDead && !BotIntegration.Friends(bot,state.Target))
                target=state.Target;
            if(target!=null)BotIntegration.Report(bot,target.PlayerPosition);
            Vector3 goal=destination;
            if(target!=null)
            {
                Vector3 away=Vector3.ProjectOnPlane(car.CarPosition-target.PlayerPosition,Vector3.up).normalized;
                if(away.sqrMagnitude<.1f)away=Vector3.forward;
                goal=target.PlayerPosition+away*(aircraft?75:18);
            }
            Vector3 velocity;
            if(now>=state.GroundAt){state.GroundAt=now+.2f;state.Ground=Ground(car.CarPosition);state.GoalGround=Ground(goal,true);}
            float ground=state.Ground;
            if(aircraft)
            {
                bool arrived=Vector3.ProjectOnPlane(goal-car.CarPosition,Vector3.up).sqrMagnitude<25*25;
                if(car.CarHealth<10 || (target==null && now-state.Entered>8 &&
                    (arrived || !BotIntegration.TransportUseful(bot,car) || now-state.Entered>90)))state.Landing=true;
                if(state.Landing)
                {
                    goal=car.CarPosition;goal.y=ground+VehicleGeometry.Get(car.CarTypeIdentifier).GroundClearance;
                    if(car.CarPosition.y-goal.y<.7f && Mathf.Abs(state.Velocity.y)<1){Exit(server,bot,state);return false;}
                }
                else goal.y=Mathf.Max(state.GoalGround+24,ground+16);
                var error=goal-car.CarPosition;velocity=Vector3.ClampMagnitude(error*1.4f,18);
                velocity.y=Mathf.Clamp(error.y*1.6f,state.Landing?-2:-5,6);
            }
            else
            {
                if(state.Road==null || (now>state.RouteAt && (goal-state.Goal).sqrMagnitude>25*25))
                {
                    state.RouteAt=now+8;state.Goal=goal;
                    var path=RoadNetwork.Route(car.CarPosition,goal);
                    if(path==null || path.Count<2){Exit(server,bot,state);return false;}
                    var points=new List<RoadPosition>{RoadNetwork.Point(car.CarPosition)};foreach(var point in path)points.Add(RoadNetwork.Point(point));state.Road=new RoadFollower(points);
                }
                velocity=RoadNetwork.Vector(state.Road.Step(RoadNetwork.Point(car.CarPosition),RoadNetwork.Point(state.Velocity),15,8,dt));
                velocity.y=Mathf.Clamp((ground+clearance-car.CarPosition.y)*3,-4,5);
                if(state.Road.Arrived && now-state.Entered>4){Exit(server,bot,state);return false;}
            }
            // Arrival/avoidance use bounded acceleration, lookahead and deterministic side choice.
            var look=velocity.normalized*Mathf.Max(5,velocity.magnitude*.8f);
            if(Blocked(car.CarPosition+Vector3.up*.5f,look,aircraft?2:.65f,out var obstacle))
            {
                state.Blocked+=dt;
                float top=obstacle.collider.bounds.max.y+(aircraft?4:2);
                if((aircraft || hover) && top-ground<(aircraft?60:12))velocity=new Vector3(0,Mathf.Clamp((top-car.CarPosition.y)*2,0,6),0);
                else if(aircraft || hover)velocity=Vector3.Cross(Vector3.up,look).normalized*((bot.PlayerIndex%2==0)?5:-5);
                else velocity=Vector3.zero; // Wheeled vehicles brake before obstacles instead of flying.
                if(state.Blocked>8 && !aircraft){Exit(server,bot,state);return false;}
            }
            else state.Blocked=0;
            state.Velocity=Vector3.MoveTowards(state.Velocity,velocity,12*dt);
            var step=state.Velocity*dt;
            if(Blocked(car.CarPosition+Vector3.up*.5f,step,aircraft?1.5f:.5f,out _)){state.Velocity=Vector3.zero;step=Vector3.zero;}
            car.UpdatePosition(car.CarPosition+step);
            Vector3 facing=aircraft && target!=null?target.PlayerPosition-car.CarPosition:state.Velocity;facing.y=0;
            if(facing.sqrMagnitude>.1f)car.UpdateRotation(Quaternion.RotateTowards(car.CarRotation,Quaternion.LookRotation(facing),80*dt));
            car.UpdateInput(state.Velocity.normalized);car.UpdateDrivingState(CarDrivingState.None);car.WasChanged();
            bot.UpdatePosition(car.CarPosition+Vector3.up);bot.UpdateRotation(new Vector2(0,car.CarRotation.eulerAngles.y));bot.UpdateMovementDirection(Vector3.zero);
            if(bot.PlayerObject)bot.PlayerObject.transform.position=bot.PlayerPosition;
            if(heli && target!=null && now>=state.Fire && now-state.Entered>2 && !BotIntegration.Friends(bot,target))
            {state.Fire=now+.1f;HelicopterMissiles.BotFire(server,bot,target);}
            if(now>=state.Send){state.Send=now+.1f;View(server,car,true);BotIntegration.BroadcastPlayerUpdate(server,bot,bot.PlayerPosition);}
            return true;
        }
        private static void Exit(ServerClient server,TABGPlayerServer bot,Drive state)
        {
            if(state.Car!=null)state.AvoidCars[state.Car.CarIndex]=Time.unscaledTime+60;
            if(state.Car==null)return;
            if(bot.CurrentSeat!=null)Seat(server,bot,state.Car,bot.CurrentSeat,false);
            View(server,state.Car,false,true);state.Car=null;state.Wanted=null;state.Road=null;state.Search=Time.unscaledTime+45;
        }
    }
}
