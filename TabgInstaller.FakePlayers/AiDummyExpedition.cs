using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Landfall.Network;
using TabgInstaller.Vehicles;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private enum ExpeditionStage { Equip, Travel, Search }
        private ExpeditionStage _expeditionStage;
        private bool _expeditionStarted,_strategicTravel,_sprinting,_wasEngaged;
        private int _place=-1,_patrol;
        private float _nextSupplyStop;
        private bool NeedsResupply()=>!HasUsableWeapon() || _reserveAmmo<BotRoundRules.ReserveTarget(_weaponProfile.MagazineSize) || OffensiveGrenadeStock()<2;
        private float _stageSince,_expeditionPlanAt,_expeditionProgressAt,_regroupSince,_regroupBreakUntil,_routeLogAt;
        private Vector3 _expeditionGoal,_expeditionProgressPosition;
        private string _expeditionReason="Ausruesten";
        private readonly Dictionary<int,float> _visitedPlaces=new Dictionary<int,float>();
        private static readonly System.Reflection.FieldInfo RingTravel=AccessTools.Field(typeof(TheRing),"timeToTravel");
        private float RingSeconds()
        {
            var ring=TheRing.Instance;if(!ring)return float.PositiveInfinity;
            float remaining=(float)RingTravel.GetValue(ring)-ring.GetTimeTravelled();
            return remaining>0?remaining:float.PositiveInfinity;
        }
        private bool TryStrategicZone(out Vector3 goal)
        {
            goal=_player.PlayerPosition;var ring=GetRingContext();if(!ring.HasRing)return false;
            float outside=Mathf.Max(0,ring.Distance-Mathf.Max(5,ring.Radius-25));
            if(!ring.ShouldRotate && !BotRoundRules.Rotate(outside,RingSeconds(),ring.IsMoving || ring.IsClosing))return false;
            if(outside<=0 && ring.Distance<ring.Radius*.7f)return false;
            var offset=Flat(_player.PlayerPosition-ring.Center);
            var candidate=ring.Center+Vector3.ClampMagnitude(offset,Mathf.Max(3,ring.Radius*.65f));
            candidate.y=_player.PlayerPosition.y;
            return TryResolveSafeGround(candidate,out goal);
        }
        private bool ReadyForSearch(float age)
            =>BotRoundRules.Ready(HasCombatWeapon(),_magazineAmmo+_reserveAmmo,_weaponProfile.MagazineSize,age);
        private bool MayPursueLoot(NetworkGun loot)
        {
            if(!HasUsableWeapon())return true;
            if(NeedsResupply() && Flat(loot.Position-_player.PlayerPosition).magnitude<45 && (GetPickup(loot)?.weaponType==Pickup.WeaponType.Ammo || GetPickup(loot)?.weaponType==Pickup.WeaponType.Grenade))return true;
            var leader=_squadLeader??this;
            if(!leader._expeditionStarted || leader._expeditionStage==ExpeditionStage.Equip)return true;
            // Moving groups may collect nearby supplies, but an optional weapon must not restart a tour.
            return (loot.Position-_player.PlayerPosition).sqrMagnitude<12*12;
        }
        private void PlanExpedition()
        {
            float now=Time.unscaledTime;if(now<_expeditionPlanAt)return;_expeditionPlanAt=now+1;
            var places=BotMapKnowledge.Known(_room);
            if(!_expeditionStarted)
            {
                _expeditionStarted=true;_stageSince=now;_expeditionProgressAt=now;_expeditionProgressPosition=_player.PlayerPosition;
                _place=Enumerable.Range(0,places.Count).OrderBy(i=>Flat(places[i].Position-_player.PlayerPosition).sqrMagnitude).First();
                _expeditionGoal=_player.PlayerPosition;_patrol=_player.PlayerIndex%6;
            }
            var members=_squad.Length>1?_squad:new[]{_player};
            bool engaged=members.Any(p=>p.PlayerObject?.GetComponent<AiDummyController>() is AiDummyController c && (c._canSeeTarget && c.HasUsableWeapon() || c._reviveStarted || c._isHealing));
            if(engaged && now-_stageSince<BotRoundRules.MaxLootSeconds){_wasEngaged=true;return;}
            if(_wasEngaged)
            {
                _wasEngaged=false;
                if(_expeditionStage!=ExpeditionStage.Equip && members.Any(p=>p.PlayerObject?.GetComponent<AiDummyController>() is AiDummyController c && (c.NeedsResupply() || p.Health<45)))
                {_expeditionStage=ExpeditionStage.Equip;_stageSince=now;}
            }
            if(!engaged && _expeditionStage!=ExpeditionStage.Equip && now>_nextSupplyStop && members.Any(p=>p.PlayerObject?.GetComponent<AiDummyController>()?.NeedsResupply()==true))
            {_expeditionStage=ExpeditionStage.Equip;_stageSince=now;_nextSupplyStop=now+100;}
            bool ready=members.Where(p=>!p.IsDowned).All(p=>p.PlayerObject?.GetComponent<AiDummyController>()?.ReadyForSearch(now-_stageSince)??false);
            if(_expeditionStage==ExpeditionStage.Equip)
            {
                _expeditionReason="Ausruesten: "+places[_place].Name;
                var loot=BotLootIndex.Nearby(_room,_player.PlayerPosition,110).FirstOrDefault(g=>g.VisualObject && members.Any(p=>
                {var c=p.PlayerObject?.GetComponent<AiDummyController>();return c!=null && !c.IsLootTemporarilyBlocked(g.Index) && c.ScoreLootValue(g,c.GetPickup(g))>0;}));
                if(loot!=null)_expeditionGoal=loot.Position;
                if(BotRoundRules.LeaveLoot(ready,now-_stageSince) || loot==null && now-_stageSince>12)
                {_expeditionStage=ExpeditionStage.Search;_stageSince=now;_nextSupplyStop=now+60;NextSearchPoint();}
            }
            else if(_expeditionStage==ExpeditionStage.Search)
            {
                _expeditionReason="Gegner suchen: "+places[_place].Name;
                if(Flat(_expeditionGoal-_player.PlayerPosition).magnitude<5)NextSearchPoint();
                if(now-_stageSince>BotRoundRules.SearchSeconds)ChooseNextPlace();
            }
            else
            {
                _expeditionReason="Weiter nach "+places[_place].Name;
                if(Flat(_player.PlayerPosition-places[_place].Position).magnitude<55)
                {
                    _expeditionStage=ready?ExpeditionStage.Search:ExpeditionStage.Equip;_stageSince=now;NextSearchPoint();
                }
                else if(now-_stageSince>150)ChooseNextPlace();
            }
            // A failed waypoint cannot hold the team indefinitely, even if the local steering stalls.
            if(!_navigationWaiting && now-_expeditionProgressAt>12)
            {
                if(Flat(_player.PlayerPosition-_expeditionProgressPosition).magnitude<2 && Flat(_expeditionGoal-_player.PlayerPosition).magnitude>5)
                {
                    if(_wantedLoot!=null){MarkLootTemporarilyBlocked(_wantedLoot);ReleaseLootClaim();_wantedLoot=null;}
                    if(_expeditionStage==ExpeditionStage.Travel){_visitedPlaces[_place]=now;ChooseNextPlace();}
                    else NextSearchPoint();
                }
                _expeditionProgressPosition=_player.PlayerPosition;_expeditionProgressAt=now;
            }
            if(now>=_routeLogAt)
            {_routeLogAt=now+15;FakePlayersPlugin.Log($"[BotRoute] {_player.PlayerName}: {_expeditionReason}; goal={_expeditionGoal}; pos={_player.PlayerPosition}; ready={ready}; members={members.Length}");}
        }
        private void NextSearchPoint()
        {
            var center=BotMapKnowledge.Known(_room)[_place].Position;
            for(int attempt=0;attempt<6;attempt++)
            {
                float angle=(_patrol++%6)*60+_player.PlayerIndex*13;
                var candidate=center+Quaternion.Euler(0,angle,0)*Vector3.forward*(25+(_patrol%3)*18);
                candidate.y=_player.PlayerPosition.y;
                var ring=GetRingContext();if(ring.HasRing && Flat(candidate-ring.Center).magnitude>ring.Radius*.93f)continue;
                if(TryResolveSafeGround(candidate,out _expeditionGoal))return;
            }
            ChooseNextPlace();
        }
        private void ChooseNextPlace()
        {
            float now=Time.unscaledTime;var places=BotMapKnowledge.Known(_room);_visitedPlaces[_place]=now;
            var ring=GetRingContext();float best=float.PositiveInfinity;int chosen=-1;Vector3 goal=_player.PlayerPosition;
            for(int i=0;i<places.Count;i++)
            {
                if(i==_place)continue;var place=places[i];float distance=Flat(place.Position-_player.PlayerPosition).magnitude;
                if(!BotRoundRules.TripFits(distance,RingSeconds()) || distance<80 || ring.HasRing && Flat(place.Position-ring.Center).magnitude>ring.Radius*.88f)continue;
                float age=_visitedPlaces.TryGetValue(i,out float visited)?now-visited:1000;
                float variation=((_player.PlayerIndex*31+i*17+(int)(now/180)*7)%101)/100f;
                float score=BotRoundRules.PoiScore(distance,place.Popularity,age,variation,BotMapKnowledge.FlightDistance(place.Position));
                if(score>=best)continue;
                var candidate=place.Position;candidate.y=_player.PlayerPosition.y;
                if(!TryResolveSafeGround(candidate,out var safe))continue;
                best=score;chosen=i;goal=safe;
            }
            if(chosen>=0){_place=chosen;_expeditionGoal=goal;_expeditionStage=ExpeditionStage.Travel;}
            else
            {
                // Late ring: patrol safe ground locally instead of choosing a distant unsafe POI.
                var center=ring.HasRing?ring.Center:_player.PlayerPosition;
                var offset=Quaternion.Euler(0,_patrol++*73,0)*Vector3.forward*(ring.HasRing?Mathf.Min(25,ring.Radius*.35f):35);
                var candidate=center+offset;candidate.y=_player.PlayerPosition.y;
                if(TryResolveSafeGround(candidate,out var safe))_expeditionGoal=safe;
                _expeditionStage=ExpeditionStage.Search;
            }
            _stageSince=now;_expeditionProgressAt=now;_expeditionProgressPosition=_player.PlayerPosition;_routeLogAt=0;
        }
    }
}
