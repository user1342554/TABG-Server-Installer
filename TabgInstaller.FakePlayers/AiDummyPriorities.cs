using System.Linq;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private float _lastSquadReport,_roundLogAt;
        private bool ImmediateDanger()
        {
            bool attacked=_player.LastAttackTime>0 && Time.time-_player.LastAttackTime<3;
            bool armedEnemy=_target!=null && (! _target.Bot || _target.PlayerObject?.GetComponent<AiDummyController>()?.HasUsableWeapon()==true);
            float distance=_target!=null?Flat(_target.PlayerPosition-_player.PlayerPosition).magnitude:999;
            return TabgInstaller.Vehicles.BotRoundRules.ImmediateDanger(attacked,_canSeeTarget,armedEnemy,distance);
        }
        private void LogRoundState()
        {
            if(!BotTactics.IsTestBot(_player.PlayerIndex) || Time.unscaledTime<_roundLogAt)return;
            _roundLogAt=Time.unscaledTime+15;
            FakePlayersPlugin.Log($"[BotState] t={Time.unscaledTime:0} {GetDebugSummary()} pos={_player.PlayerPosition} sprint={_sprinting}");
        }
    }
}
