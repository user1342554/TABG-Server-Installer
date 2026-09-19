using System;
using System.IO;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    [HarmonyPatch(typeof(ItemThrownCommand),"Run")]
    internal static class BotNativeThreats
    {
        internal sealed class Throw{internal TABGPlayerServer Player;internal int Item,Stock;internal Vector3 Origin,Rotation;}
        static void Prefix(byte[] __0,ServerClient __1,byte __2,out Throw __state)
        {
            __state=null;if(__0==null || __0.Length<33)return;
            var p=__1?.GameRoomReference?.FindPlayer(__2);if(p==null || p.Bot || p.IsDead)return;
            using(var r=new BinaryReader(new MemoryStream(__0)))
            {
                r.ReadByte();int item=r.ReadInt32(),quantity=r.ReadInt32();
                var origin=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var rotation=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                if(quantity<=0 || !Finite(origin) || !Finite(rotation) || (origin-p.PlayerPosition).sqrMagnitude>100*100)return;
                int stock=p.HasLoot(item);if(stock<=0)return;
                __state=new Throw{Player=p,Item=item,Stock=stock,Origin=origin,Rotation=rotation};
            }
        }
        static void Postfix(ServerClient __1,Throw __state)
        {
            if(__state==null || __state.Player.HasLoot(__state.Item)>=__state.Stock)return;
            try{BotGrenades.ObserveNativeThrow(__1,__state.Player,__state.Item,__state.Origin,__state.Rotation);}
            catch(Exception ex){FakePlayersPlugin.Log("[BotHearing] Native grenade observation failed: "+ex.Message);}
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x+p.y+p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}
