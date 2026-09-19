using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
namespace TabgInstaller.FakePlayers
{
    [HarmonyPatch(typeof(EntityUpdatesCommand),"Run")]
    internal static class BotNetworkRecipients
    {
        // Vanilla omits its Bot check in the vehicle queue branch. Bots have no queue.
        private static readonly List<TABGPlayerServer> Scratch=new List<TABGPlayerServer>();
        private static List<TABGPlayerServer> RealWatchers(ServerChunks chunks,ChunkDataServer chunk)
            => Filter(chunks.GetWatchers(chunk));
        private static List<TABGPlayerServer> Filter(List<TABGPlayerServer> watchers)
        {
            Scratch.Clear();
            if(watchers!=null)foreach(var p in watchers)if(p!=null && !p.Bot && p.UpdateMessageQueue!=null)Scratch.Add(p);
            return Scratch; // caller immediately copies with AddRange; no shared source mutation.
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            var native=typeof(ServerChunks).GetMethod("GetWatchers",new[]{typeof(ChunkDataServer)});
            var filter=typeof(BotNetworkRecipients).GetMethod("RealWatchers",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            foreach(var instruction in input)
            {
                if((instruction.opcode==OpCodes.Call || instruction.opcode==OpCodes.Callvirt) && Equals(instruction.operand,native))
                {instruction.opcode=OpCodes.Call;instruction.operand=filter;}
                yield return instruction;
            }
        }
    }
    public partial class FakePlayersPlugin
    {
        public static bool SetBotSeat(ServerClient server,TABGPlayerServer bot,TABGCarServer car,TABGCarServerSeat seat,bool enter)
        {
            if(server?.GameRoomReference==null || bot==null || !bot.Bot || car==null || seat==null || !server.GameRoomReference.Players.Contains(bot))return false;
            if(enter && (bot.IsDead || bot.IsDowned || seat.Occupant!=null || bot.IsInsideCar))return false;
            if(!enter && seat.Occupant!=bot)return false;
            NativeOccupy.Invoke(null,new object[]{seat,car,bot,enter?SeatAction.GetIn:SeatAction.GetOut});
            car.RemoveTemporaryOwner();
            BroadcastSeatAccepted(server,bot,car,seat,enter);BroadcastPlayerUpdate(server,bot,bot.PlayerPosition);
            return true;
        }
        private static readonly System.Reflection.MethodInfo NativeOccupy=AccessTools.Method(typeof(RequestSeatCommand),"OccupySeat");
    }
}
