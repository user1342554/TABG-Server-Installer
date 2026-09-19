using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    public partial class FakePlayersPlugin
    {
        private static int StyledGear(List<GearDataEntry> items,int skill,int personality)
        {
            string[] simple={"shirt","pants","jeans","shoe","cap","basic","plain"};
            string[] theme=personality==0?new[]{"punk","mohawk","mask","horn","pirate","cowboy"}:
                personality==1?new[]{"helmet","military","army","jacket","goggle","tactical"}:
                new[]{"hood","coat","scarf","beanie","camo","hat"};
            var pool=items.Where(e=>e.m_gear).OrderByDescending(e=>
            {
                string name=(e.m_gear.DisplayName+" "+e.m_gear.name).ToLowerInvariant();
                float fit=(skill<=2?simple:theme).Count(word=>name.Contains(word))*5;
                if(skill>=4)fit+=Mathf.Min(3,e.m_gear.transform.childCount);
                return fit;
            }).Take(skill<=2?Math.Min(5,items.Count):Math.Min(8,items.Count)).ToArray();
            return pool.Length>0?pool[UnityEngine.Random.Range(0,pool.Length)].m_gear.Index:RandomGearIndex(items);
        }
        private static int BotColor(GearDatabase db,Gear.GearType part,int skill,int personality)
        {
            if(db.Colors==null || db.Colors.Length==0 || skill<=2)return -1;
            // Pick from the real palette. All parts share a coherent personality color.
            for(int offset=0;offset<db.Colors.Length;offset++)
            {
                int color=(personality*7+(skill>=4?3:0)+offset)%db.Colors.Length;
                if(part!=Gear.GearType.HEAD || db.m_bannedHeadColors==null || !db.m_bannedHeadColors.Contains(color))return color;
            }
            return -1;
        }
    }
}
