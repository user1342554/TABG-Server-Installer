using System.Linq;
using Landfall.Network;
using UnityEngine;
using TabgInstaller.Vehicles;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private TABGPlayerServer[] _squad=new TABGPlayerServer[0];
        private AiDummyController _squadLeader;
        private float _squadAt;
        private string _squadPurpose="Solo";
        private void RefreshSquad()
        {
            if(Time.unscaledTime<_squadAt)return;_squadAt=Time.unscaledTime+.75f;
            _squad=_room.Players.Where(p=>p.Bot && !p.IsDead && p.Health>0 && BotTactics.Friends(_player,p)).OrderBy(p=>p.PlayerIndex).ToArray();
            var leader=_squad.FirstOrDefault(p=>!p.IsDowned);
            _squadLeader=leader?.PlayerObject?.GetComponent<AiDummyController>();
        }
        private Vector3 CoordinateSquad(Vector3 own)
        {
            if(Recovering)return _recoveryGoal;
            RefreshSquad();var leader=_squadLeader??this;leader.RefreshSquad();leader.PlanExpedition();
            _squadPurpose=leader._expeditionReason;_strategicTravel=false;_sprinting=false;
            // Zone and personal escape remain possible even while a squad wants to loot.
            if(TryStrategicZone(out var safe)){_squadPurpose="Gemeinsam in die Zone";_strategicTravel=true;return safe;}
            if(_state==AiState.Unstuck || _hasTeamOrder || _isHealing || _isReloading || _reviveStarted || _currentAction==AiAction.ReviveTeamMate)return own;
            bool needsKit=!HasUsableWeapon();
            bool lootAction=_currentAction==AiAction.LootWeapon || _currentAction==AiAction.LootAmmo || _currentAction==AiAction.LootGrenade || _currentAction==AiAction.Heal;
            if(_wantedLoot!=null && _wantedLoot.VisualObject && !IsLootTemporarilyBlocked(_wantedLoot.Index) &&
               (needsKit && !ImmediateDanger() || lootAction))
            {_squadPurpose=needsKit?"Erst bewaffnen":"Vorrat aufnehmen";_strategicTravel=true;return _wantedLoot.Position;}
            if(_canSeeTarget && IsValidEnemyTarget(_target)){_squadPurpose="Kaempfen";return own;}
            if(!needsKit && _currentAction==AiAction.SearchLastSeen && _searchGiveUpTimer>0 && HasActiveThreatMemory() && TryGetThreatPosition(out var heard) && Flat(heard-_player.PlayerPosition).magnitude<100)
            {_squadPurpose="Kontakt untersuchen";_strategicTravel=true;return _state==AiState.Evading?own:ResolveTeamDestination(heard);}
            var contact=_squad.Select(p=>p.PlayerObject?.GetComponent<AiDummyController>()).FirstOrDefault(c=>c!=null && c._canSeeTarget && c.IsValidEnemyTarget(c._target));
            if(contact!=null && !needsKit){_squadPurpose="Kamerad im Kampf unterstuetzen";_strategicTravel=true;return ResolveTeamDestination(contact._target.PlayerPosition+SquadOffset());}
            var down=_squad.FirstOrDefault(p=>IsValidReviveTarget(p));
            if(down!=null){_squadPurpose="Kamerad retten";_strategicTravel=true;return ResolveTeamDestination(down.PlayerPosition+SquadOffset());}
            if(leader!=this && leader._player.IsInsideCar && !_player.IsInsideCar)
            {_strategicTravel=true;return leader._player.CurrentCar.CarPosition;}
            float separation=Flat(_player.PlayerPosition-leader._player.PlayerPosition).magnitude;
            bool far=leader._squad.Any(p=>!p.IsDowned && Flat(p.PlayerPosition-leader._player.PlayerPosition).magnitude>55);
            if(far && Time.unscaledTime>=leader._regroupBreakUntil)
            {
                if(leader._regroupSince==0)leader._regroupSince=Time.unscaledTime;
                if(Time.unscaledTime-leader._regroupSince<10)
                {_squadPurpose="Sammeln";_strategicTravel=true;
                    var rendezvous=leader._squad.Where(p=>!p.IsDowned).OrderBy(p=>leader._squad.Sum(m=>Flat(m.PlayerPosition-p.PlayerPosition).magnitude)).First().PlayerPosition;
                    return ResolveTeamDestination(rendezvous+SquadOffset());}
                leader._regroupSince=0;leader._regroupBreakUntil=Time.unscaledTime+25;
            }
            else if(!far)leader._regroupSince=0;
            if(leader!=this && separation>45){_strategicTravel=true;return ResolveTeamDestination(leader._player.PlayerPosition+SquadOffset());}
            if(_wantedLoot!=null && _wantedLoot.VisualObject && !IsLootTemporarilyBlocked(_wantedLoot.Index) && MayPursueLoot(_wantedLoot) &&
                (leader==this || Flat(_wantedLoot.Position-leader._expeditionGoal).magnitude<65))
            {_strategicTravel=Flat(_wantedLoot.Position-_player.PlayerPosition).magnitude>12;return _wantedLoot.Position;}
            if(_wantedLoot!=null){ReleaseLootClaim();_wantedLoot=null;}
            _strategicTravel=true;_player.ChangeAimDownSightState(false);
            return ResolveTeamDestination(leader._expeditionGoal+(leader==this?Vector3.zero:SquadOffset()));
        }
        private Vector3 SquadOffset()
        {
            int slot=System.Array.IndexOf(_squad,_player);
            return Quaternion.Euler(0,slot*120,0)*Vector3.forward*6;
        }
        internal bool TransportUseful(TABGCarServer car)
        {
            RefreshSquad();
            if(_reviveStarted || _reviveTarget!=null || _isHealing || _isReloading || _squadPurpose=="Kamerad retten")return false;
            bool emergency=GetRingContext().Danger>.4f;
            if(!_hasWeapon && !emergency)return false;
            if(_canSeeTarget && _target!=null && Vector3.Distance(_target.PlayerPosition,_player.PlayerPosition)<70)return false;
            if(_squad.Length>1)
            {
                if(!BotTravelRules.FitsSquad(car.NumberOfSeats,_squad.Count(p=>!p.IsDowned)))return false;
                if(_squad.Any(p=>!p.IsDowned && !p.IsInsideCar && p.PlayerObject?.GetComponent<AiDummyController>() is AiDummyController c && !c._hasWeapon) && !emergency)return false;
                if(_squadLeader!=null && _squadLeader!=this && _squadLeader._player.CurrentCar!=car)return false;
                if(_squadPurpose=="Sammeln")return false;
            }
            return true;
        }
    }
    public static partial class BotSquadAccess
    {
        public static bool TransportUseful(TABGPlayerServer bot,TABGCarServer car)
            =>bot?.PlayerObject?.GetComponent<AiDummyController>()?.TransportUseful(car)??false;
    }
}
