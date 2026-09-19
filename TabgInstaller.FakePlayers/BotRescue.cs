using System.IO;
using System.Linq;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal static class BotRescue
    {
        // Only actual team-mode teammates can rescue; solo alliances do not alter death rules.
        internal static bool TryDown(ServerClient server,TABGPlayerServer victim,TABGPlayerServer attacker)
        {
            if(server?.GameRoomReference==null || victim==null || !victim.Bot || victim.IsDead || victim.IsDowned || attacker==null)return false;
            if(!TabgInstaller.Vehicles.BotRoundRules.CanRevive(server.GameRoomReference.CurrentGameSettings.MaxTeamSize))return false;
            if(!server.GameRoomReference.Players.Any(p=>p!=victim && p.GroupIndex==victim.GroupIndex && victim.GroupIndex!=255 && !p.IsDead && !p.IsDowned && p.Health>0 && BotTactics.Friends(p,victim)))return false;
            victim.UpdateLastAttacker(attacker.PlayerIndex);
            if(victim.CurrentSeat!=null)FakePlayersPlugin.SetBotSeat(server,victim,victim.CurrentCar,victim.CurrentSeat,false);
            victim.Down();
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write(victim.PlayerIndex);w.Write(attacker.PlayerIndex);w.Write(0f);w.Write((byte)4);
                w.Write(NetworkOptimizationHelper.OptimizeDirection((victim.PlayerPosition-attacker.PlayerPosition).normalized));
                ServerMessages.SendToRealClients(server,(EventCode)6,stream.ToArray(),true,true);
            }
            FakePlayersPlugin.Log($"[BotRescue] {victim.PlayerName} down; a team mate can rescue them.");
            return true;
        }
        [HarmonyPatch(typeof(BattleRoyaleGameMode),"KillPlayer")]
        internal static class PactDownPatch
        {
            static bool Prefix(TABGPlayerServer __0,TABGPlayerServer __1)
                =>!TryDown(FakePlayersPlugin.ServerRef,__0,__1);
        }
    }
}
