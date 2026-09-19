using System.Collections.Generic;
using System.Linq;
using TabgInstaller.Vehicles;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private readonly Dictionary<int,NetworkGun> _carriedWeapons=new Dictionary<int,NetworkGun>();
        private readonly Dictionary<int,int> _storedMagazines=new Dictionary<int,int>();
        private float _lastWeaponSwitch,_carelessUntil,_nextJudgement,_hearingTimer;
        private bool _riskyHeal,_outnumbered;
        private Vector3 _grenadeDanger;private float _dangerUntil,_dangerReactAt,_dangerRadius;
        internal void ObserveGrenade(Vector3 origin,Vector3 landing,float fuse,float radius)
        {
            if(_player.IsDead || _player.IsDowned || (origin-_player.PlayerPosition).sqrMagnitude>50*50 || !HasLineToPoint(origin+Vector3.up,true))return;
            _grenadeDanger=landing;_dangerUntil=Time.unscaledTime+fuse+.5f;_dangerRadius=radius+3;
            _dangerReactAt=Time.unscaledTime+Mathf.Lerp(1.1f,.2f,GetSkillT());
        }
        private bool TryGrenadeEscape(out Vector3 point)
        {
            point=_player.PlayerPosition;
            if(_player.IsInsideCar || Time.unscaledTime>_dangerUntil || Time.unscaledTime<_dangerReactAt || (point-_grenadeDanger).sqrMagnitude>_dangerRadius*_dangerRadius)return false;
            var away=point-_grenadeDanger;away.y=0;if(away.sqrMagnitude<.1f)away=Vector3.right;
            point+=away.normalized*(_dangerRadius+3);return TacticalGround(ref point);
        }
        internal void HearWorldNoise(TABGPlayerServer source,Vector3 point)
        {
            var error=UnityEngine.Random.insideUnitCircle*Mathf.Lerp(12,4,GetSkillT());
            RememberThreat(source,point+new Vector3(error.x,0,error.y),5,false);
        }
        private void TickCommonSense(float dt)
        {
            if(Time.unscaledTime>=_nextJudgement)
            {
                _nextJudgement=Time.unscaledTime+6;
                int threats=0,friends=1;
                foreach(var other in _room.Players)
                {
                    if(other==_player || other.IsDead || other.IsDowned || (other.PlayerPosition-_player.PlayerPosition).sqrMagnitude>65*65)continue;
                    if(BotTactics.Friends(_player,other))friends++;
                    else if(HasLineOfSight(other))threats++;
                }
                _outnumbered=threats>=friends+2;
                _carelessUntil=UnityEngine.Random.value<BotSkillRules.MistakeChance(_skillLevel)?Time.unscaledTime+4:0;
            }
        }
        private void TickWeaponChoice()
        {
            if(!_hasWeapon || _isHealing || Time.unscaledTime-_lastWeaponSwitch<4)return;
            _reserveAmmo=_player.HasLoot(GetAmmoItemIdForWeapon());
            if(_magazineAmmo>0 && (_target==null || !_canSeeTarget))return;
            float distance=_target!=null && _canSeeTarget?Vector3.Distance(_target.PlayerPosition,_player.PlayerPosition):30;
            NetworkGun best=null;float score=float.NegativeInfinity;
            foreach(var pair in _carriedWeapons)
            {
                if(pair.Key==_equippedWeaponId || _player.HasLoot(pair.Key)<=0)continue;
                int mag=_storedMagazines.TryGetValue(pair.Key,out int stored)?stored:BotWeaponCatalog.Profile(_room,pair.Key,pair.Value.WeaponName).MagazineSize;
                if(mag<=0)continue;
                var profile=BotWeaponCatalog.Profile(_room,pair.Key,pair.Value.WeaponName);
                float candidate=-Mathf.Abs(profile.PreferredRange-distance);
                if(candidate>score){score=candidate;best=pair.Value;}
            }
            if(best!=null && (_magazineAmmo<=0 || (distance<GetMinimumFightRange() && score> -Mathf.Abs(_weaponProfile.PreferredRange-distance)+12)))
            {StopFullAuto();EquipWeapon(best);_shootTimer=.6f;}
        }
        private bool FriendInShot(Vector3 origin,Vector3 direction,float distance)
        {
            foreach(var p in _room.Players)
            {
                if(p==_player || p.IsDead || !BotTactics.Friends(p,_player))continue;
                var offset=p.PlayerPosition+Vector3.up*.7f-origin;float along=Vector3.Dot(offset,direction);
                if(along>0 && along<distance && (offset-direction*along).sqrMagnitude<1)return true;
            }
            return false;
        }
    }
}
