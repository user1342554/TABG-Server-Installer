using System;
using System.Collections.Generic;
using System.Linq;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private readonly Dictionary<int,NetworkGun> _grenades=new Dictionary<int,NetworkGun>();
        private readonly HashSet<int> _healItems=new HashSet<int>();
        private float _nextGrenadeDecision,_rescueShieldUntil;
        private byte _shieldFor=255;
        private int _grenadeChoice;
        private static bool IsShield(NetworkGun item)=>item!=null && (item.WeaponName??"").IndexOf("shield",StringComparison.OrdinalIgnoreCase)>=0;
        private int GrenadeStock()=>_grenades.Keys.Sum(id=>Math.Max(0,_player.HasLoot(id)));
        private int OffensiveGrenadeStock()=>_grenades.Values.Where(g=>BotGrenades.IsDamaging(_room,g)).Sum(g=>Math.Max(0,_player.HasLoot(g.UniqueIdentifier)));
        private void RememberGrenade(NetworkGun item){_grenades[item.UniqueIdentifier]=item;}
        private void TickSmartGrenades()
        {
            if(Time.unscaledTime<_nextGrenadeDecision || _player.IsInsideCar || _isHealing)return;
            _nextGrenadeDecision=Time.unscaledTime+.8f;
            TABGPlayerServer rescue=_reviveStarted?_activeReviveTarget:_reviveTarget;
            if(IsValidReviveTarget(rescue) && (_shieldFor!=rescue.PlayerIndex || Time.unscaledTime>_rescueShieldUntil))
            {
                var shield=_grenades.Values.FirstOrDefault(g=>IsShield(g) && _player.HasLoot(g.UniqueIdentifier)>0);
                if(shield!=null && Vector3.Distance(rescue.PlayerPosition,_player.PlayerPosition)<28 &&
                    BotGrenades.TryThrow(_server,_player,shield,rescue.PlayerPosition,true,out float flight))
                {_shieldFor=rescue.PlayerIndex;_rescueShieldUntil=Time.unscaledTime+flight+7;_nextGrenadeDecision=Time.unscaledTime+8;return;}
            }
            if(_reviveStarted || _isReloading || !HasUsableWeapon())return;
            Vector3 throwTarget;
            bool visible=_target!=null && _canSeeTarget && IsValidEnemyTarget(_target);
            if(visible)throwTarget=_target.PlayerPosition;
            else if(_lastSeenTimer>AiDummyCatalog.ThreatMemorySeconds-3 && HasActiveThreatMemory())throwTarget=_lastKnownThreatPosition;
            else return;
            var attacks=_grenades.Values.Where(g=>!IsShield(g) && BotGrenades.IsDamaging(_room,g) && _player.HasLoot(g.UniqueIdentifier)>0).ToArray();
            if(attacks.Length==0)return;
            if(_grenadeChoice>1000000)_grenadeChoice=0;
            // A failed trajectory or friendly blast-radius check must not block every other grenade type.
            var frag=attacks[_grenadeChoice++%attacks.Length];
            float distance=Vector3.Distance(_player.PlayerPosition,throwTarget);
            bool entrenched=!visible || !HasShotLine(_target) || _targetVelocity.sqrMagnitude<4;
            int enemies=_room.Players.Count(p=>IsValidEnemyTarget(p) && HasLineOfSight(p) && (p.PlayerPosition-throwTarget).sqrMagnitude<8*8);
            if(!TabgInstaller.Vehicles.BotRoundRules.GrenadeOpportunity(distance,entrenched,enemies,_currentAction==AiAction.Push || _state==AiState.Advancing,UnityEngine.Random.value) || Time.unscaledTime<_carelessUntil)return;
            if(BotGrenades.TryThrow(_server,_player,frag,throwTarget,false,out _))_nextGrenadeDecision=Time.unscaledTime+UnityEngine.Random.Range(5f,8f);
        }
        private bool ShieldProtectsRescue(TABGPlayerServer target)
            =>target!=null && _shieldFor==target.PlayerIndex && Time.unscaledTime<_rescueShieldUntil && BotGrenades.Protected(target.PlayerPosition);
        private void RefreshMedicalStock()
        {
            if(_healingItemId>=0)_healItems.Add(_healingItemId);
            if(_healingItemId<0 || _player.HasLoot(_healingItemId)<=0)_healingItemId=_healItems.Where(id=>_player.HasLoot(id)>0).DefaultIfEmpty(-1).First();
            _healingItemCount=_healingItemId>=0?_player.HasLoot(_healingItemId):0;
        }
    }
}
