using System;
using System.Collections.Generic;
using TabgInstaller.Vehicles;
using UnityEngine;

namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        // One standing proxy for planning, tactical goals, recovery and executed movement.
        private const float WalkRadius=.35f, WalkSlope=.65f, WalkSkin=.025f;
        private static CapsuleCollider _walkProxy;
        private readonly Collider[] _walkOverlaps=new Collider[48];
        private readonly RaycastHit[] _walkHits=new RaycastHit[48];
        private struct WalkBlock
        {
            internal string Reason;
            internal Collider Collider;
            internal Vector3 Normal;
            internal float Depth, Distance;
            public override string ToString()=>Reason+" collider="+(Collider?Collider.name+"/"+Collider.GetType().Name:"none")+" normal="+Normal+" depth="+Depth.ToString("F6")+" hitDistance="+Distance.ToString("F6");
        }
        private WalkBlock _lastWalkBlock;
        private float _walkLogAt;
        private readonly Dictionary<Vector3Int,float> _failedWalkGoals=new Dictionary<Vector3Int,float>();
        private readonly BotRecoveryState _recovery=new BotRecoveryState();
        private Vector3 _progressAnchor,_recoveryGoal,_recoveryOrigin,_previousWalkPosition;
        private bool _progressInitialized;
        private float _recoveryRetry;
        private int _recoveryAttempt;
        private bool Recovering=>_recovery.Active;
        private bool _plannedJump, _jumpSettling, _walkingDrop;
        private Vector3 _jumpStart,_jumpEnd;
        private float _jumpElapsed;

        private static CapsuleCollider WalkProxy()
        {
            if(_walkProxy)return _walkProxy;
            var go=new GameObject("BotWalkQuery"){hideFlags=HideFlags.HideAndDontSave};
            _walkProxy=go.AddComponent<CapsuleCollider>();_walkProxy.radius=WalkRadius;_walkProxy.height=1.5f;
            _walkProxy.direction=1;
            // The server runtime does not produce penetration results for this disabled query collider.
            // Keep its physics shape alive, far outside the map and excluded from all trigger-ignoring queries.
            go.transform.position=new Vector3(0,-10000,0);_walkProxy.isTrigger=true;
            Physics.SyncTransforms();return _walkProxy;
        }
        private void WalkCapsule(Vector3 position,out Vector3 bottom,out Vector3 top)
        {
            float foot=position.y-_terrainHeightOffset;
            bottom=new Vector3(position.x,foot+.60f,position.z);
            top=new Vector3(position.x,foot+1.40f,position.z);
        }
        private bool IgnoreWalkCollider(Collider collider)=>!collider || collider==_walkProxy || IsOwnCollider(collider) || IsAnyPlayerCollider(collider);
        private bool BodyOverlap(Vector3 position,out WalkBlock block)=>BodyOverlap(position,out block,out _);
        private bool BodyOverlap(Vector3 position,out WalkBlock block,out float supportLift)
        {
            supportLift=0;block=default(WalkBlock);WalkCapsule(position,out var bottom,out var top);
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,WalkRadius,_walkOverlaps,~0,QueryTriggerInteraction.Ignore);
            if(count>=_walkOverlaps.Length){block.Reason="overlap-buffer-full";return true;}
            bool found=false;var center=(bottom+top)*.5f;
            for(int i=0;i<count;i++)
            {
                var c=_walkOverlaps[i];_walkOverlaps[i]=null;if(IgnoreWalkCollider(c))continue;
                if(!Physics.ComputePenetration(WalkProxy(),center,Quaternion.identity,c,c.transform.position,c.transform.rotation,out var normal,out float depth))continue;
                if(depth<.0001f)continue;
                if(normal.y>=WalkSlope){supportLift=Mathf.Max(supportLift,depth/normal.y+.005f);continue;}
                if(!found || depth>block.Depth){found=true;block=new WalkBlock{Reason="body-overlap",Collider=c,Normal=normal,Depth=depth};}
            }
            return found;
        }
        private bool SweepBody(Vector3 start,Vector3 end,out WalkBlock block,bool escape=false)
        {
            block=default(WalkBlock);var delta=end-start;
            bool overlap=BodyOverlap(start,out var initial,out float startLift);
            bool escaping=overlap && escape && delta.magnitude<=.16f && Vector3.Dot(delta,initial.Normal)>.00001f;
            if(overlap && !escaping){block=initial;return false;}
            if(BodyOverlap(end,out var final,out float endLift) && !(escaping && final.Collider==initial.Collider && final.Depth<initial.Depth-.0001f))
            {block=final;return false;}
            if(delta.sqrMagnitude<.000001f)return !overlap;
            // Start-contact casts return a synthetic normal opposite travel. Lift the query body
            // out of valid supporting ground, then sweep again so walls on the same mesh still block.
            bool settling=escape && delta.y>0 && delta.magnitude<=.1f && endLift<startLift;
            if((startLift>.35f || endLift>.35f) && !settling){block.Reason="support-depth-too-large";return false;}
            var castStart=start+Vector3.up*startLift;var castEnd=end+Vector3.up*endLift;
            if((startLift>0 && BodyOverlap(castStart,out block)) || (endLift>0 && BodyOverlap(castEnd,out block)))return false;
            delta=castEnd-castStart;if(delta.sqrMagnitude<.000001f)return !overlap;
            WalkCapsule(castStart,out var bottom,out var top);
            int count=Physics.CapsuleCastNonAlloc(bottom,top,WalkRadius-.002f,delta.normalized,_walkHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count>=_walkHits.Length){block.Reason="sweep-buffer-full";return false;}
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var h=_walkHits[i];if(IgnoreWalkCollider(h.collider) || h.normal.y>=WalkSlope || (escaping && h.collider==initial.Collider))continue;
                if(h.distance<nearest){nearest=h.distance;block=new WalkBlock{Reason="body-sweep",Collider=h.collider,Normal=h.normal,Distance=h.distance};}
            }
            return float.IsInfinity(nearest);
        }
        private bool WalkGround(Vector3 candidate,Vector3 previous,out Vector3 ground,out WalkBlock block)
        {
            ground=candidate;block=default(WalkBlock);
            if(!TryFindGroundInfo(candidate,out var probe)){block.Reason="no-ground";return false;}
            if(probe.BadTerrain || probe.Normal.y<WalkSlope){block=new WalkBlock{Reason="ground-slope-or-surface",Normal=probe.Normal};return false;}
            ground.y=probe.Y+_terrainHeightOffset;
            if(!BodyOverlap(ground,out _,out float support) && support<=.35f)ground.y+=support;
            float rise=ground.y-previous.y;
            if(rise>.45f+Flat(candidate-previous).magnitude*1.18f || rise < -3f)
            {block.Reason="ground-height-gap";return false;}
            return true;
        }
        private bool SettleStandingSupport(float dt)
        {
            var current=_player.PlayerPosition;
            if(BodyOverlap(current,out _,out float lift) || lift<=.005f)return false;
            var next=current+Vector3.up*Mathf.Min(lift,Mathf.Min(.08f,2*dt));
            if(!SweepBody(current,next,out var obstruction,true))return false;
            ForceServerPosition(next);_player.UpdateMovementDirection(Vector3.zero);_player.UpdateMovementType(0);return true;
        }
        private bool WalkSegment(Vector3 start,Vector3 end,out WalkBlock block,bool escape=false)
        {
            block=default(WalkBlock);var previous=start;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Flat(end-start).magnitude/.65f));
            if(steps>64){block.Reason="segment-too-long";return false;}
            for(int i=1;i<=steps;i++)
            {
                var candidate=Vector3.Lerp(start,end,(float)i/steps);
                if(!WalkGround(candidate,previous,out var grounded,out block))return false;
                if(grounded.y<previous.y-.55f){if(!ClearWalkingDrop(previous,grounded,out block))return false;}
                else if(!SweepBody(previous,grounded,out block,escape))return false;
                previous=grounded;
            }
            return true;
        }
        private float _walkGoalUntil,_walkGoalLogAt;
        private Vector3 _walkGoalRequested,_walkGoalResolved;
        private bool ClearWalkGoal(Vector3 requested,out Vector3 grounded)
        {
            grounded=requested;
            if(!TryFindGroundInfo(requested,out var floor) || floor.BadTerrain)return false;
            grounded.y=floor.Y+_terrainHeightOffset;
            return !BodyOverlap(grounded,out _,out float lift) && lift<=.3f;
        }
        private Vector3 ResolveWalkDestination(Vector3 requested)
        {
            var current=_player.PlayerPosition;
            if(Flat(requested-current).sqrMagnitude>90*90)return requested;
            if(Time.unscaledTime<_walkGoalUntil && (requested-_walkGoalRequested).sqrMagnitude<.25f)return _walkGoalResolved;
            _walkGoalRequested=requested;_walkGoalUntil=Time.unscaledTime+1.5f;
            if(!FailedWalkGoal(requested) && ClearWalkGoal(requested,out _walkGoalResolved))return _walkGoalResolved;
            RejectWalkGoal(requested);
            if(_wantedLoot!=null && Flat(_wantedLoot.Position-requested).sqrMagnitude<4*4)
            {MarkLootTemporarilyBlocked(_wantedLoot);ReleaseLootClaim();_wantedLoot=null;_lootTimer=0;_decisionTimer=0;return _walkGoalResolved=current;}
            var toward=Flat(current-requested).normalized;if(toward.sqrMagnitude<.1f)toward=Vector3.forward;
            bool found=false;
            for(int r=1;r<=4 && !found;r++)for(int a=0;a<8;a++)
            {
                float radius=r==1?1:r==2?2:r==3?4:6;
                var candidate=requested+Quaternion.Euler(0,a*45,0)*toward*radius;
                if(!FailedWalkGoal(candidate) && ClearWalkGoal(candidate,out _walkGoalResolved)){found=true;break;}
            }
            if(!found)_walkGoalResolved=current;
            if(Time.unscaledTime>=_walkGoalLogAt)
            {_walkGoalLogAt=Time.unscaledTime+5;FakePlayersPlugin.Log($"[BotGoal] {_player.PlayerName}: blocked={requested}; substitute={_walkGoalResolved}; found={found}");}
            return _walkGoalResolved;
        }
        private void WalkDiagnostic(Vector3 wanted,Vector3 applied,WalkBlock block)
        {
            _lastWalkBlock=block;
            if(Time.unscaledTime<_walkLogAt)return;_walkLogAt=Time.unscaledTime+3;
            FakePlayersPlugin.Log($"[BotMove] {_player.PlayerName}: {block}; pos={_player.PlayerPosition}; requested={wanted}; applied={applied}; recovery={Recovering}; pathPending={_navigationWaiting}");
        }
        private bool FailedWalkGoal(Vector3 point)=>_failedWalkGoals.TryGetValue(Vector3Int.RoundToInt(point/3),out float until) && Time.unscaledTime<until;
        private void RejectWalkGoal(Vector3 point)
        {
            if(_failedWalkGoals.Count>64)_failedWalkGoals.Clear();
            _failedWalkGoals[Vector3Int.RoundToInt(point/3)]=Time.unscaledTime+30;
        }
        private void ResetWalkingRoute()
        {
            _detour=null;_navigationWaiting=false;_detourRetry=0;_hasNavPath=false;_localAvoidTimer=0;
            _smoothedDirection=Vector3.zero;KnownWalkingEdges.Clear();
        }
        private void UpdateRecovery(Vector3 current,Vector3 destination,float dt)
        {
            if(!_progressInitialized){_progressInitialized=true;_progressAnchor=_previousWalkPosition=current;}
            bool moved=Flat(current-_progressAnchor).magnitude>=.35f;
            if(moved)_progressAnchor=current;
            bool pending=_navigationWaiting && _detour!=null && !_detour.Done && Time.unscaledTime-_detourStarted<25;
            bool wants=Flat(destination-current).magnitude>.7f && !_isHealing && !_isReloading && !_reviveStarted;
            var result=_recovery.Tick(dt,wants,moved,pending,_plannedJump || _jumpSettling,
                Flat(current-_recoveryGoal).magnitude<.2f || (Recovering && Flat(current-_recoveryOrigin).magnitude>.8f));
            if(result==BotRecoveryEvent.Finished || result==BotRecoveryEvent.Failed)
            {
                FakePlayersPlugin.Log($"[BotRecovery] {_player.PlayerName}: {result}; moved={Flat(current-_recoveryOrigin).magnitude:F2}; pos={current}");
                ResetWalkingRoute();_decisionTimer=0;
                if(result==BotRecoveryEvent.Failed){_recoveryAttempt++;_recoveryRetry=Time.unscaledTime+1;}
            }
            if(result!=BotRecoveryEvent.Start)return;
            if(Time.unscaledTime<_recoveryRetry){_recovery.Cancel();return;}
            _recoveryOrigin=current;ResetWalkingRoute();
            bool overlap=BodyOverlap(current,out var obstruction);
            var away=overlap?Flat(obstruction.Normal):Flat(current-destination);
            if(away.sqrMagnitude<.01f)away=Vector3.forward;away.Normalize();
            bool found=false;
            if(overlap && away.sqrMagnitude>.1f)
            {
                var step=current+away*.08f;
                if(WalkGround(step,current,out step,out _) && SweepBody(current,step,out _,true))
                {_recoveryGoal=current+away*1.2f;found=true;}
            }
            for(int k=0;!found && k<16;k++)
            {
                float angle=((k+_recoveryAttempt)%8)*45;
                var point=current+Quaternion.Euler(0,angle,0)*away*(k<8?.8f:1.6f);
                if(WalkSegment(current,point,out _))
                {WalkGround(point,current,out _recoveryGoal,out _);found=true;}
            }
            if(!found)
            {
                _recovery.Cancel();_recoveryRetry=Time.unscaledTime+1;_recoveryAttempt++;
                if(_wantedLoot!=null){MarkLootTemporarilyBlocked(_wantedLoot);ReleaseLootClaim();_wantedLoot=null;}
                WalkDiagnostic(destination,current,overlap?obstruction:new WalkBlock{Reason="no-local-recovery-step"});return;
            }
            SetState(AiState.Unstuck,3);_unstuckTarget=_recoveryGoal;
            FakePlayersPlugin.Log($"[BotRecovery] {_player.PlayerName}: Start; from={current}; goal={_recoveryGoal}; overlap={overlap}");
        }
        private bool ClearWalkingDrop(Vector3 start,Vector3 landing,out WalkBlock block)
        {
            block=default(WalkBlock);float drop=start.y-landing.y;
            if(drop<.55f || drop>3 || BodyOverlap(landing,out _)){block.Reason="unsafe-drop-landing";return false;}
            var edge=new Vector3(landing.x,start.y,landing.z);
            return SweepBody(start,edge,out block) && SweepBody(edge,landing,out block);
        }
        private bool TryStartWalkingDrop(Vector3 current,Vector3 toward)
        {
            if(_plannedJump || Recovering || Flat(toward-current).sqrMagnitude<.01f)return false;
            var landing=current+Flat(toward-current).normalized*.9f;
            if(!WalkGround(landing,current,out landing,out _) || !ClearWalkingDrop(current,landing,out _) || !PeopleAllowStep(current,landing))return false;
            _jumpStart=current;_jumpEnd=landing;_jumpElapsed=0;_plannedJump=true;_walkingDrop=true;return true;
        }
        private Vector3 WalkingAirPoint(float elapsed)
        {
            if(!_walkingDrop){float t=Mathf.Clamp01(elapsed/.65f);return Vector3.Lerp(_jumpStart,_jumpEnd,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.95f);}
            var point=Vector3.Lerp(_jumpStart,new Vector3(_jumpEnd.x,_jumpStart.y,_jumpEnd.z),Mathf.Clamp01(elapsed/.18f));
            float falling=Mathf.Max(0,elapsed-.18f);point.y=Mathf.Max(_jumpEnd.y,_jumpStart.y-4.9f*falling*falling);return point;
        }
        private bool TryStartJump(Vector3 current,Vector3 toward)
        {
            if(_plannedJump || Recovering || _jumpCooldown>0 || Flat(toward-current).sqrMagnitude<.01f || BodyOverlap(current,out _))return false;
            _jumpCooldown=2;
            var landing=current+Flat(toward-current).normalized*1.8f;
            if(!WalkGround(landing,current,out landing,out _) || Mathf.Abs(landing.y-current.y)>.4f || BodyOverlap(landing,out _) || !PeopleAllowStep(current,landing))return false;
            var previous=current;
            for(int i=1;i<=12;i++)
            {
                float t=i/12f;var point=Vector3.Lerp(current,landing,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.95f);
                if(!SweepBody(previous,point,out _))return false;previous=point;
            }
            _jumpStart=current;_jumpEnd=landing;_jumpElapsed=0;_plannedJump=true;_walkingDrop=false;_jumpFlagTimer=.18f;return true;
        }
        private bool ExecuteJump(float dt)
        {
            if(_jumpSettling)
            {
                var current=_player.PlayerPosition;
                if(!TryFindGroundInfo(current,out var floor)){_jumpSettling=false;return false;}
                float target=floor.Y+_terrainHeightOffset;
                if(current.y<=target+.02f){_jumpSettling=false;return false;}
                var next=current;next.y=Mathf.Max(target,current.y-5*dt);
                if(!SweepBody(current,next,out var obstruction))
                {_jumpSettling=false;WalkDiagnostic(next,current,obstruction);return false;}
                ForceServerPosition(next);_player.UpdateMovementDirection(Vector3.zero);_player.UpdateMovementType(0);
                return true;
            }
            if(!_plannedJump)return false;
            _jumpElapsed+=dt;float duration=_walkingDrop?.18f+Mathf.Sqrt(2*(_jumpStart.y-_jumpEnd.y)/9.8f):.65f;float t=Mathf.Clamp01(_jumpElapsed/duration);
            var point=WalkingAirPoint(_jumpElapsed);
            if(!SweepBody(_player.PlayerPosition,point,out var block) || !PeopleAllowStep(_player.PlayerPosition,point))
            {_plannedJump=false;_jumpSettling=true;WalkDiagnostic(_jumpEnd,_player.PlayerPosition,block);return true;}
            ForceServerPosition(point);_player.UpdateMovementDirection(Flat(_jumpEnd-_jumpStart).normalized);
            _player.UpdateMovementType(_jumpFlagTimer>0?(byte)129:(byte)1);
            if(t>=1){_plannedJump=false;_progressAnchor=point;}
            return true;
        }
    }
}
