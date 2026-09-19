using System.Collections.Generic;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal static class BotLootIndex
    {
        private static GameRoom _room;private static float _next;
        private static readonly Dictionary<Vector2Int,List<NetworkGun>> Cells=new Dictionary<Vector2Int,List<NetworkGun>>();
        private const float CellSize=64;
        internal static List<NetworkGun> Nearby(GameRoom room,Vector3 position,float radius)
        {
            if(_room!=room || Time.unscaledTime>=_next)
            {
                _room=room;_next=Time.unscaledTime+2;Cells.Clear();
                if(room?.Weapons!=null)foreach(var gun in room.Weapons)
                {
                    if(gun==null || !gun.VisualObject)continue;var cell=Cell(gun.Position);
                    if(!Cells.TryGetValue(cell,out var list))Cells[cell]=list=new List<NetworkGun>();list.Add(gun);
                }
            }
            radius=Mathf.Min(radius,300);var center=Cell(position);int reach=Mathf.CeilToInt(radius/CellSize);
            var found=new List<NetworkGun>();
            for(int x=-reach;x<=reach;x++)for(int z=-reach;z<=reach;z++)
                if(Cells.TryGetValue(center+new Vector2Int(x,z),out var list))foreach(var gun in list)
                    if(gun!=null && gun.VisualObject && (gun.Position-position).sqrMagnitude<=radius*radius)found.Add(gun);
            found.Sort((a,b)=>(a.Position-position).sqrMagnitude.CompareTo((b.Position-position).sqrMagnitude));
            if(found.Count>96)found.RemoveRange(96,found.Count-96);
            return found;
        }
        private static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/CellSize),Mathf.FloorToInt(p.z/CellSize));
    }
}
