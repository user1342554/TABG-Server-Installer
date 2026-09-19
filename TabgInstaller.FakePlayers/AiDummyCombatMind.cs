using System;
using System.Linq;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;
using UnityEngine.AI;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        internal int Personality => _mind.Personality;
        private BotCombatMind _mind=new BotCombatMind(1);
        private float _observedHealth,_nextVision,_colliderCoverAt,_flankUntil,_flankRetry;
        private TABGPlayerServer _visionTarget;
        private readonly Collider[] _coverCandidates=new Collider[48];
        private readonly NavMeshPath _tacticalPath=new NavMeshPath();
        private Vector3 _colliderCover,_coverThreat,_flankPoint;
        private bool _haveColliderCover;
        internal bool PrepareCombatTest(Vector3 point)
        {
            if(_player.IsDead || _player.IsInsideCar || !TryFindGroundY(point,out float ground))return false;
            point.y=ground+_terrainHeightOffset;if(IsBadTerrain(point) || Physics.CheckCapsule(point+Vector3.up*.2f,point+Vector3.up*1.3f,.35f,~0,QueryTriggerInteraction.Ignore))return false;
            StopFullAuto();ReleaseLootClaim();_wantedLoot=null;_wantedCar=null;_target=null;_threatTarget=null;
            _hasThreatMemory=_hasLastSeenTarget=_canSeeTarget=_hasNavPath=_hasPoiTarget=false;
            _threatMemoryTimer=_lastSeenTimer=_soundMemoryTimer=0;
            _observedHealth=_player.Health;
            _dropStarted=false;_dropFinished=true;_dropPositionLockTimer=0;_lockedDropLanding=point;
            _warmupTimer=8;_retargetTimer=0;_decisionTimer=0;_flankUntil=0;_flankRetry=0;_colliderCoverAt=0;
            _player.Dropped();ForceServerPosition(point);_server.ForceChunkEntry(_player);
            _player.UpdateMovementDirection(Vector3.zero);_player.UpdateMovementType(0);
            FakePlayersPlugin.BroadcastRespawn(_server,_player,point);

            FakePlayersPlugin.BroadcastPlayerUpdate(_server,_player,point);
            FakePlayersPlugin.Log($"[BotTest] {_player.PlayerName}: {_mind.Name}, skill={_skillLevel}, unarmed={!_hasWeapon}, position={point}, warmup=8s");
            return true;
        }
        private void TickCombatMind(float dt)
        {
            _mind.Tick(dt);
            if(_player.Health<_observedHealth)
            {
                _mind.AddPressure(.25f+(_observedHealth-_player.Health)/80f);
                _decisionTimer=0;
            }
            _observedHealth=_player.Health;
        }
        private bool TacticalGround(ref Vector3 point)
        {
            if(!TryFindGroundY(point,out float ground))return false;
            point.y=ground+_terrainHeightOffset;
            if(IsBadTerrain(point) || FailedWalkGoal(point) || Mathf.Abs(point.y-_player.PlayerPosition.y)>6)return false;
            if(TrySampleNavMesh(_player.PlayerPosition,out var from) && TrySampleNavMesh(point,out var to))
            {
                if(!NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,_tacticalPath) || _tacticalPath.status!=NavMeshPathStatus.PathComplete)return false;
                var corners=_tacticalPath.corners;float length=0;
                for(int i=1;i<corners.Length;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
                if(length>Vector3.Distance(_player.PlayerPosition,point)*2.2f+4)return false;
                for(int i=1;i<corners.Length;i++)if(!WalkSegment(corners[i-1]+Vector3.up*_terrainHeightOffset,corners[i]+Vector3.up*_terrainHeightOffset,out _))return false;
                point=to.position+Vector3.up*_terrainHeightOffset;return true;
            }
            // Maps without a usable NavMesh only get locally reachable tactical goals.
            return WalkSegment(_player.PlayerPosition,point,out _);
        }
        private bool TryFindColliderCover(Vector3 threat,out Vector3 cover)
        {
            cover=_colliderCover;
            if(Time.unscaledTime<_colliderCoverAt)
                return _haveColliderCover && (threat-_coverThreat).sqrMagnitude<16 && HasCoverFromTarget(cover);
            _colliderCoverAt=Time.unscaledTime+1.2f+(_player.PlayerIndex%4)*.07f;
            _coverThreat=threat;_haveColliderCover=false;
            int count=Physics.OverlapSphereNonAlloc(_player.PlayerPosition,18,_coverCandidates,~0,QueryTriggerInteraction.Ignore);
            float best=float.PositiveInfinity;int checkedPoints=0;
            for(int i=0;i<count && checkedPoints<10;i++)
            {
                var collider=_coverCandidates[i];_coverCandidates[i]=null;
                if(!collider || IsOwnCollider(collider) || IsThreatCollider(collider) || collider.GetComponentInParent<ServerNetworkVehicle>())continue;
                var bounds=collider.bounds;
                if(bounds.size.y<1 || bounds.size.x>45 || bounds.size.z>45)continue;
                bool body=false;foreach(var player in _room.Players)if(player.PlayerObject && collider.transform.IsChildOf(player.PlayerObject.transform)){body=true;break;}
                if(body)continue;
                var away=Flat(bounds.center-threat).normalized;
                float support=Mathf.Abs(away.x)*bounds.extents.x+Mathf.Abs(away.z)*bounds.extents.z;
                var candidate=bounds.center+away*(support+1.2f);candidate.y=_player.PlayerPosition.y;
                if(Flat(candidate-_player.PlayerPosition).sqrMagnitude>24*24)continue;
                checkedPoints++;
                if(!TacticalGround(ref candidate) || !HasCoverFromTarget(candidate))continue;
                float score=Vector3.Distance(candidate,_player.PlayerPosition);
                foreach(var mate in _room.Players)
                    if(mate!=_player && BotTactics.Friends(_player,mate) && (candidate-mate.PlayerPosition).sqrMagnitude<3*3)score+=15;
                if(score<best){best=score;cover=candidate;_haveColliderCover=true;}
            }
            Array.Clear(_coverCandidates,0,_coverCandidates.Length);
            _colliderCover=cover;return _haveColliderCover;
        }
        private bool TryFlankDestination(out Vector3 point)
        {
            point=_flankPoint;
            if(_target==null || _player.IsInsideCar || _isReloading || _isHealing)return false;
            if(_weaponProfile.CombatClass==WeaponCombatClass.Sniper || _weaponProfile.CombatClass==WeaponCombatClass.AutoSniper || _weaponProfile.CombatClass==WeaponCombatClass.Lmg)return false;
            var mate=BotTactics.EngagedMate(_player,_target.PlayerIndex);
            float distance=Vector3.Distance(_player.PlayerPosition,_target.PlayerPosition);
            if(!BotCombatMind.CanFlank(mate!=null,HasUsableWeapon(),_canSeeTarget,_player.Health,_mind.Pressure,distance,_lastRingDanger))
            {_flankUntil=0;return false;}
            if(Time.unscaledTime<_flankUntil && Flat(_flankPoint-_player.PlayerPosition).sqrMagnitude>4)return true;
            if(Time.unscaledTime<_flankRetry)return false;
            _flankRetry=Time.unscaledTime+9;_flankUntil=0;
            Vector3 toward=Flat(_target.PlayerPosition-_player.PlayerPosition).normalized;
            // The lower-index engaged ally holds the front. Others split left/right.
            float sign=BotTactics.FlankSide(_player);
            point=_player.PlayerPosition+Vector3.Cross(Vector3.up,toward)*sign*Mathf.Clamp(distance*.4f,7,16)+toward*3;
            if(!TacticalGround(ref point))return false;
            _flankPoint=point;_flankUntil=Time.unscaledTime+5;
            FakePlayersPlugin.Log($"[BotTactic] {_player.PlayerName}: flank {sign} while {mate.PlayerName} sees {_target.PlayerName}");
            return true;
        }
    }
}
