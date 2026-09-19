using System.Collections.Generic;
using System.Linq;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.Vehicles;
namespace TabgInstaller.FakePlayers
{
    internal static class BotLaunchPads
    {
        internal sealed class Pad{internal Vector3 Position,Direction;internal float Ready,Until;internal Component Native;internal bool Temporary,MapAsset;}
        private static readonly List<Pad> Pads=new List<Pad>();private static GameRoom _room;private static float _scan;
        internal static void Reset(){Pads.Clear();_room=null;_scan=0;}
        internal static List<Pad> Known(GameRoom room)
        {
            if(_room!=room){Reset();_room=room;}
            if(Time.unscaledTime>=_scan)
            {
                _scan=Time.unscaledTime+20;
                foreach(var native in Resources.FindObjectsOfTypeAll<LaunchPad>())
                {
                    if(!native || !native.gameObject.scene.IsValid() || !native.gameObject.activeInHierarchy || native.GetComponentInParent<Pickup>() || native.isPush || Pads.Any(p=>p.Native==native))continue;
                    float multiplier=(float)HarmonyLib.AccessTools.Field(typeof(LaunchPad),"multiplier").GetValue(native);
                    Pads.Add(new Pad{Native=native,Position=native.transform.position,Direction=native.transform.forward*multiplier,Until=float.PositiveInfinity});
                }
                foreach(var native in Resources.FindObjectsOfTypeAll<JumpPlatform>())
                {
                    if(!native || !native.gameObject.scene.IsValid() || !native.gameObject.activeInHierarchy || native.GetComponentInParent<Pickup>() || Pads.Any(p=>p.Native==native))continue;
                    Pads.Add(new Pad{Native=native,Position=native.transform.position,Direction=Vector3.up*.55f,Until=float.PositiveInfinity});
                }
                // Dedicated map strips LaunchPad behaviours and names. Coordinates and directions
                // extracted from the two ACTIVE LaunchPads in installed Area_COMBINED (level8).
                // Disabled boss-challenge pads are deliberately excluded; require matching solid geometry.
                AddMapPad(new Vector3(415.992f,131.272f,457.466f));
                AddMapPad(new Vector3(-336.082f,247.327f,-541.791f));
                FakePlayersPlugin.Log($"[BotLaunch] Known scene pads={Pads.Count(p=>!p.Temporary)}; positions={string.Join("; ",Pads.Where(p=>!p.Temporary).Select(p=>(p.MapAsset?"map asset":p.Native.name)+" "+p.Position))}");

            }
            Pads.RemoveAll(p=>Time.unscaledTime>p.Until || (!p.Temporary && !p.MapAsset && !p.Native));return Pads;
        }
        private static void AddMapPad(Vector3 position)
        {
            if(Pads.Any(p=>(p.Position-position).sqrMagnitude<5*5))return;
            RaycastHit best=default(RaycastHit);float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(position+Vector3.up*3,Vector3.down,5,~0,QueryTriggerInteraction.Ignore))
            {
                if(!hit.collider || hit.collider.GetComponentInParent<Player>() || hit.collider.GetComponentInParent<Car>())continue;
                if(hit.normal.y>.8f && hit.distance<distance){best=hit;distance=hit.distance;}
            }
            if(float.IsInfinity(distance) || Mathf.Abs(best.point.y-position.y)>1.5f)return;
            Pads.Add(new Pad{MapAsset=true,Position=best.point+Vector3.up*.05f,Direction=Vector3.up*2,Until=float.PositiveInfinity});
        }
        internal static void Temporary(GameRoom room,Vector3 point,float ready)
        {
            Known(room);Pads.Add(new Pad{Position=point,Direction=Vector3.up*2,Ready=ready,Until=ready+20,Temporary=true});
        }
    }
    internal partial class AiDummyController
    {
        private BotLaunchPads.Pad _chosenPad;
        private Vector3 _launchVelocity,_launchGoal;
        private float _launchAt,_nextLaunchDecision,_launchCooldown,_launchSend,_padDeadline;
        private bool _launchFlying;
        private bool PlanLaunch(ref Vector3 destination)
        {
            if(_player.IsInsideCar || _player.IsDowned || _reviveStarted || _isHealing || _isReloading)return false;
            if(_chosenPad!=null)
            {
                if(Time.unscaledTime>_chosenPad.Until || Time.unscaledTime>_padDeadline){_chosenPad=null;return false;}
                destination=_chosenPad.Position;
                if(Flat(destination-_player.PlayerPosition).magnitude<2.8f && Mathf.Abs(destination.y-_player.PlayerPosition.y)<4)
                {
                    if(Time.unscaledTime<_chosenPad.Ready)return false;
                    _launchAt=Time.unscaledTime;_launchFlying=true;_launchCooldown=_launchAt+25;
                    var initial=_chosenPad.Direction*100;
                    _launchVelocity=Vector3.ClampMagnitude(initial,85);
                    if(_launchVelocity.y<20)_launchVelocity.y=35;
                    _chosenPad=null;StopFullAuto();ClearPhysicalInput();ReleaseLootClaim();
                    _player.SkyDive();ServerMessages.SendToRealClients(_server,(EventCode)39,new byte[]{_player.PlayerIndex,1},true);
                    FakePlayersPlugin.Log($"[BotLaunch] {_player.PlayerName}: launch toward {_launchGoal}, intent={_squadPurpose}/{_currentAction}");return true;
                }
                return false;
            }
            if(Time.unscaledTime<_nextLaunchDecision || Time.unscaledTime<_launchCooldown)return false;
            _nextLaunchDecision=Time.unscaledTime+2;
            bool purpose=_strategicTravel || _currentAction==AiAction.RunToRing || _currentAction==AiAction.FollowTeam || _currentAction==AiAction.FollowOrder || _state==AiState.Evading || _state==AiState.Advancing || _squadPurpose=="Gemeinsam in die Zone" || _squadPurpose=="Sammeln" || _squadPurpose=="Kamerad retten";
            if(!purpose || !ImmediateDanger() && (_squadLeader??this)._expeditionStage==ExpeditionStage.Equip)return false;
            var travelGoal=destination;
            if(_state==AiState.Evading && TryGetThreatPosition(out var threat))
                travelGoal=_player.PlayerPosition+Flat(_player.PlayerPosition-threat).normalized*160;
            var ring=GetRingContext();
            if(ring.HasRing)
            {
                var offset=Flat(travelGoal-ring.Center);
                if(offset.magnitude>ring.Radius*.82f)travelGoal=ring.Center+Vector3.ClampMagnitude(offset,ring.Radius*.82f);
            }
            if(Flat(travelGoal-_player.PlayerPosition).magnitude<65 || !TryResolveSafeGround(travelGoal,out var safe))return false;
            float direct=Flat(safe-_player.PlayerPosition).magnitude,best=direct*.7f;
            foreach(var pad in BotLaunchPads.Known(_room))
            {
                float walk=Flat(pad.Position-_player.PlayerPosition).magnitude;
                float remaining=Flat(safe-pad.Position).magnitude;
                var direction=Flat(pad.Direction);
                float alignment=direction.sqrMagnitude>.1f?Vector3.Dot(direction.normalized,Flat(safe-pad.Position).normalized):1;
                float cost=BotTravelRules.LaunchCost(walk,remaining,alignment);
                if(cost<best){best=cost;_chosenPad=pad;}
            }
            _launchGoal=safe;
            if(_chosenPad==null)
            {
                var grenade=_grenades.Values.FirstOrDefault(g=>(g.WeaponName??"").ToLowerInvariant().Contains("launch") && _player.HasLoot(g.UniqueIdentifier)>0);
                var point=_player.PlayerPosition+Flat(safe-_player.PlayerPosition).normalized*6;
                if(grenade!=null && TryResolveSafeGround(point,out point) && BotGrenades.TryThrow(_server,_player,grenade,point,true,out float flight))
                {
                    _launchCooldown=Time.unscaledTime+30;
                    _chosenPad=BotLaunchPads.Known(_room).Where(p=>p.Temporary && (p.Position-point).sqrMagnitude<12*12).OrderByDescending(p=>p.Ready).FirstOrDefault();
                }
            }
            if(_chosenPad!=null){_padDeadline=Time.unscaledTime+90;destination=_chosenPad.Position;}return false;
        }
        private bool TickLaunch(float dt)
        {
            if(!_launchFlying)return false;
            if(_player.IsDead || _player.IsDowned){_launchFlying=false;_player.Land();return false;}
            ClearPhysicalInput();TickBotRingDamage(dt);
            var position=_player.PlayerPosition;var delta=Flat(_launchGoal-position);
            var horizontal=Flat(_launchVelocity);
            horizontal=Vector3.MoveTowards(horizontal,delta.normalized*Mathf.Min(30,delta.magnitude*.7f),8*dt);
            _launchVelocity=new Vector3(horizontal.x,Mathf.Max(-40,_launchVelocity.y-12*dt),horizontal.z);
            var step=_launchVelocity*dt;RaycastHit closest=default(RaycastHit);float distance=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(position+Vector3.up*.7f,.35f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!IsOwnCollider(hit.collider) && !IsAnyPlayerCollider(hit.collider) && hit.distance<distance){closest=hit;distance=hit.distance;}
            if(!float.IsInfinity(distance))
            {
                position+=step.normalized*Mathf.Max(0,distance-.1f);
                if(_launchVelocity.y<=0 && closest.normal.y>.55f)
                {
                    _launchFlying=false;_player.Land();_decisionTimer=0;_stuckCheckTimer=1;
                    if(TryFindGroundY(position,out float ground))position.y=ground+_terrainHeightOffset;
                    using(var stream=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(stream))
                    {w.Write(_player.PlayerIndex);w.Write(position.x);w.Write(position.y);w.Write(position.z);ServerMessages.SendToRealClients(_server,(EventCode)43,stream.ToArray(),true);}
                    FakePlayersPlugin.Log($"[BotLaunch] {_player.PlayerName}: landed at {position}");
                }
                else _launchVelocity=Vector3.ProjectOnPlane(_launchVelocity,closest.normal)*.6f;
            }
            else position+=step;
            if(Time.unscaledTime-_launchAt>18)_launchVelocity=new Vector3(0,Mathf.Min(-8,_launchVelocity.y),0);
            ForceServerPosition(position);_player.UpdateMovementDirection(_launchVelocity.normalized);_player.UpdateMovementType(0);
            if(Time.unscaledTime>=_launchSend){_launchSend=Time.unscaledTime+.1f;FakePlayersPlugin.BroadcastPlayerUpdate(_server,_player,position);}
            return true;
        }
    }
}
