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
    public sealed class AircraftSafety : MonoBehaviour
    {
        private sealed class State
        {
            internal Vector3 Previous,Velocity;
            internal float Sample,Send,GroundCheck,GroundSince,HealthSync;
            internal byte Pilot=255;
            internal bool Falling,Settled;
        }
        private readonly Dictionary<TABGCarServer,State> _states=new Dictionary<TABGCarServer,State>();
        private GameRoom _room;
        private static bool Alive(TABGPlayerServer p)=>p!=null && !p.IsDead && !p.IsDowned;
        private static void Seat(ServerClient server,TABGPlayerServer p,TABGCarServer car,TABGCarServerSeat seat,SeatAction action)
        {
            if(p.Bot && BotIntegration.Seat(server,p,car,seat,action==SeatAction.GetIn))return;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {w.Write(p.PlayerIndex);w.Write(car.CarIndex);w.Write(seat.NetworkIndex);w.Write((byte)action);RequestSeatCommand.Run(stream.ToArray(),server);}
        }
        internal static void Eject(BinaryReader r,TABGPlayerServer p,ServerClient server)
        {
            int index=r.ReadInt32();var car=p.CurrentCar;var seat=p.CurrentSeat;
            if(r.BaseStream.Position!=r.BaseStream.Length || !Alive(p) || car==null || car.CarIndex!=index || seat==null || !VehicleBalance.IsAircraft(car.CarName))return;
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.EjectShield,w=>{w.Write(index);w.Write(p.PlayerIndex);}),server.GameRoomReference.Players.Select(x=>x.PlayerIndex).ToArray(),true,false);
            Seat(server,p,car,seat,SeatAction.GetOut);
            if(p.CurrentCar!=null)return;
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.EjectLaunch,w=>w.Write(index)),new[]{p.PlayerIndex},true,false);
            Debug.Log($"[AircraftSafety] Eject player={p.PlayerIndex} car={index}");
        }
        private static void Send(ServerClient server,TABGCarServer car,State state)
        {
            server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.AircraftFall,w=>
            {
                w.Write(car.CarIndex);w.Write(state.Falling);
                w.Write(car.CarPosition.x);w.Write(car.CarPosition.y);w.Write(car.CarPosition.z);
                var a=car.CarRotation.eulerAngles;w.Write(a.x);w.Write(a.y);w.Write(a.z);
            }),server.GameRoomReference.Players.Select(p=>p.PlayerIndex).ToArray(),true,false);
        }
        private void FixedUpdate()
        {
            var server=Citrus.World;if(server?.GameRoomReference==null)return;
            if(_room!=server.GameRoomReference){_states.Clear();_room=server.GameRoomReference;}
            if(_room.Cars==null || _room.Players==null){_states.Clear();return;}
            float now=Time.unscaledTime,dt=Time.fixedDeltaTime;
            foreach(var obsolete in _states.Keys.Where(c=>!_room.Cars.Contains(c)).ToArray())_states.Remove(obsolete);
            foreach(var car in _room.Cars.Where(c=>VehicleBalance.IsAircraft(c.CarName)).ToArray())
            {
                if(!_states.TryGetValue(car,out var state))_states[car]=state=new State{Previous=car.CarPosition,Sample=now};
                if(now>=state.HealthSync){state.HealthSync=now+2;AircraftCombat.SyncMaximum(server,car);}
                if(MissileRules.IsHeli(car.CarName) && now>=state.GroundCheck)
                {
                    state.GroundCheck=now+.25f;float gap=float.PositiveInfinity;
                    foreach(var h in Physics.RaycastAll(car.CarPosition+Vector3.up,Vector3.down,12,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(h.collider.GetComponentInParent<ServerNetworkVehicle>() || _room.Players.Any(p=>p.PlayerObject && h.collider.transform.IsChildOf(p.PlayerObject.transform)))continue;
                        if(h.normal.y>.8f)gap=Mathf.Min(gap,h.distance-1);
                    }
                    bool near=gap<=VehicleGeometry.Get(car.CarTypeIdentifier).GroundClearance+.65f && state.Velocity.magnitude<5;
                    if(!near){state.GroundSince=0;AircraftCombat.GroundState(server,car,false);}
                    else {if(state.GroundSince==0)state.GroundSince=now;if(now-state.GroundSince>.8f)AircraftCombat.GroundState(server,car,true);}
                }
                var occupants=_room.Players.Where(p=>Alive(p) && p.CurrentCar==car && p.CurrentSeat!=null).OrderBy(p=>p.CurrentSeat.NetworkIndex).ToArray();
                var driverSeat=Enumerable.Range(0,car.NumberOfSeats).Select(car.GetSeat).FirstOrDefault(s=>s!=null && s.DriverSeat);
                var driver=occupants.FirstOrDefault(p=>p.CurrentSeat==driverSeat);
                if(driver==null && driverSeat!=null && occupants.Length>0)
                {
                    var replacement=occupants[0];
                    // Native exit/entry events synchronize both seats and the new driver's flag.
                    Seat(server,replacement,car,replacement.CurrentSeat,SeatAction.GetOut);
                    if(driverSeat.Occupant!=null)
                    {
                        if(_room.Players.Contains(driverSeat.Occupant))Seat(server,driverSeat.Occupant,car,driverSeat,SeatAction.GetOut);
                        else driverSeat.EjectOccupant();
                    }
                    Seat(server,replacement,car,driverSeat,SeatAction.GetIn);
                    driver=replacement.CurrentSeat==driverSeat?replacement:null;
                    if(driver!=null)
                    {
                        server.SendMessageToClients((EventCode)VehicleProtocol.Event,VehicleProtocol.Message(VehicleProtocol.PilotHandover,w=>
                        {w.Write(car.CarIndex);w.Write(state.Velocity.x);w.Write(state.Velocity.y);w.Write(state.Velocity.z);}),new[]{driver.PlayerIndex},true,false);
                        Debug.Log($"[AircraftSafety] Pilot handover car={car.CarIndex} player={driver.PlayerIndex}");
                    }
                }
                if(driver!=null)
                {
                    state.Pilot=driver.PlayerIndex;
                    if(state.Falling){state.Falling=false;state.Settled=false;Send(server,car,state);}
                    float elapsed=now-state.Sample;
                    if(car.CarPosition!=state.Previous && elapsed>.001f)
                    {state.Velocity=Vector3.ClampMagnitude((car.CarPosition-state.Previous)/elapsed,60);state.Previous=car.CarPosition;state.Sample=now;}
                    else if(elapsed>.3f)state.Velocity=Vector3.zero;
                    continue;
                }
                if(!MissileRules.IsHeli(car.CarName) || state.Pilot==255)continue;
                if(!state.Falling)
                {state.Falling=true;state.Settled=false;car.RemoveTemporaryOwner();state.Send=0;Debug.Log($"[AircraftSafety] Unoccupied helicopter falling #{car.CarIndex}");}
                if(!state.Settled)
                {
                    state.Velocity+=Physics.gravity*dt;state.Velocity*=Mathf.Exp(-.035f*dt);
                    Vector3 step=state.Velocity*dt;RaycastHit contact=default(RaycastHit);float nearest=float.PositiveInfinity;
                    foreach(var hit in Physics.SphereCastAll(car.CarPosition,1.2f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore))
                    {
                        // Departing occupants are not terrain; only world contact triggers a crash.
                        if(_room.Players.Any(p=>p.PlayerObject && hit.collider.transform.IsChildOf(p.PlayerObject.transform)))continue;
                        var proxy=hit.collider.GetComponentInParent<ServerNetworkVehicle>();
                        if(proxy && ReferenceEquals(AccessTools.Field(typeof(ServerNetworkVehicle),"m_Car").GetValue(proxy),car))continue;
                        if(hit.distance<nearest){contact=hit;nearest=hit.distance;}
                    }
                    // Sphere casts omit initial overlaps. A short ground ray also catches an
                    // aircraft whose resting hull already touches terrain when the pilot exits.
                    if(step.y<0)foreach(var hit in Physics.RaycastAll(car.CarPosition+Vector3.up*2,Vector3.down,3.2f-step.y,~0,QueryTriggerInteraction.Ignore))
                    {
                        // Departing occupants are not terrain; only world contact triggers a crash.
                        if(_room.Players.Any(p=>p.PlayerObject && hit.collider.transform.IsChildOf(p.PlayerObject.transform)))continue;
                        var proxy=hit.collider.GetComponentInParent<ServerNetworkVehicle>();
                        if(proxy && ReferenceEquals(AccessTools.Field(typeof(ServerNetworkVehicle),"m_Car").GetValue(proxy),car))continue;
                        if(hit.point.y>car.CarPosition.y+.1f)continue;
                        float distance=Mathf.Max(0,(car.CarPosition.y-hit.point.y-1.2f)/-step.y)*step.magnitude;
                        if(distance<=step.magnitude && distance<nearest){contact=hit;nearest=distance;}
                    }
                    if(!float.IsInfinity(nearest))
                    {
                        car.UpdatePosition(car.CarPosition+step.normalized*nearest);
                        float speed=Mathf.Abs(Vector3.Dot(state.Velocity,contact.normal));
                        if(AircraftCombat.UnoccupiedImpact(car,state.Pilot,speed,server))continue;
                        state.Velocity=Vector3.zero;state.Settled=true;
                    }
                    else
                    {
                        car.UpdatePosition(car.CarPosition+step);
                        car.UpdateRotation(car.CarRotation*Quaternion.Euler(8*dt,25*dt,5*dt));
                    }
                    car.WasChanged();
                }
                if(now>=state.Send){state.Send=now+.05f;Send(server,car,state);}
            }
        }
    }
}
