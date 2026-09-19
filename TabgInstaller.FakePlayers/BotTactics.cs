using System;
using System.Collections.Generic;
using System.Linq;
using Landfall.Network;
using TabgInstaller.Vehicles;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    public static class BotTactics
    {
        public delegate bool VehicleUpdate(ServerClient server,TABGPlayerServer bot,TABGPlayerServer visibleTarget,ref Vector3 destination,float dt);
        public static VehicleUpdate VehicleControl;
        private static readonly BotAllianceRules Pacts=new BotAllianceRules();
        private struct Sighting { internal Vector3 Position;internal float Time;internal byte Enemy; }
        private static readonly Dictionary<byte,Sighting> Reports=new Dictionary<byte,Sighting>();
        private static readonly HashSet<int> Encounters=new HashSet<int>();
        private static readonly HashSet<byte> TestMembers=new HashSet<byte>();
        private static readonly Dictionary<byte,bool> Social=new Dictionary<byte,bool>();
        public static byte TeamId(TABGPlayerServer bot)=>bot==null?(byte)0:Pacts.TeamId(bot.PlayerIndex);
        private static int _pairCursor;
        private static GameRoom _room;private static float _next;private static bool _finale;
        public static bool Friends(TABGPlayerServer a,TABGPlayerServer b)=>a!=null && b!=null &&
            (a==b || (a.GroupIndex!=255 && a.GroupIndex==b.GroupIndex) || (a.Bot && b.Bot && Pacts.Friends(a.PlayerIndex,b.PlayerIndex)));
        public static bool CanAttack(TABGPlayerServer attacker,TABGPlayerServer target) => attacker!=null && target!=null && !Friends(attacker,target) && (!attacker.Bot || !BotTestObservers.Contains(target));
        public static int AllianceSize(TABGPlayerServer bot)=>bot==null?1:Pacts.Size(bot.PlayerIndex);
        private static bool Alive(TABGPlayerServer p)=>p!=null && !p.IsDead && p.Health>0 && p.HasDropped;
        internal static void EnsureRoom(GameRoom room){if(_room!=room){Reset();_room=room;}}
        internal static bool IsTestBot(byte id)=>TestMembers.Contains(id);
        internal static void TestPact(byte[] members)
        {
            foreach(byte member in members){Forget(member);TestMembers.Add(member);}
            for(int i=1;i<members.Length && i<3;i++)Pacts.Join(members[0],members[i]);
        }
        internal static void Forget(byte index){Pacts.Remove(index);Reports.Remove(index);TestMembers.Remove(index);Social.Remove(index);Encounters.RemoveWhere(key=>(key>>8)==index || (key&255)==index);}
        internal static void Reset(){_room=null;Pacts.Clear();Reports.Clear();Encounters.Clear();TestMembers.Clear();Social.Clear();_pairCursor=0;_finale=false;_next=0;}
        internal static void Tick(ServerClient server)
        {
            var room=server.GameRoomReference;
            if(_room!=room){_room=room;Pacts.Clear();Reports.Clear();Encounters.Clear();TestMembers.Clear();Social.Clear();_pairCursor=0;_finale=false;_next=0;}
            if(Time.unscaledTime<_next || room?.Players==null)return;
            _next=Time.unscaledTime+.5f;
            var living=room.Players.Where(Alive).ToArray();
            foreach(var p in room.Players)if(p.IsDead || p.Health<=0){Pacts.Remove(p.PlayerIndex);Reports.Remove(p.PlayerIndex);}
            var remaining=room.Players.Where(p=>p!=null && !p.IsDead && p.Health>0).ToArray();
            if(!_finale && remaining.Length>1 && remaining.Length<=3 && remaining.All(p=>p.Bot && Pacts.Friends(remaining[0].PlayerIndex,p.PlayerIndex)))
            {
                _finale=true;Pacts.Clear();Reports.Clear();
                FakePlayersPlugin.Log("[BotPact] Final survivors: alliance dissolved; free-for-all resumes.");
                // Occasionally the pact concedes to one friend. Always leave one survivor.
                if(UnityEngine.Random.value<.18f)
                    for(int i=1;i<remaining.Length;i++)FakePlayersPlugin.ApplyEnvironmentDamage(server,remaining[i],10000,"conceded the final to a former ally");
                return;
            }
            if(_finale || room.CurrentGameSettings.MaxTeamSize!=1)return;
            var bots=living.Where(p=>p.Bot && !p.IsDowned).OrderBy(p=>p.PlayerIndex).ToArray();
            if(bots.Length<2)return;
            // At most four line-of-sight probes per half-second; rotate fairly through pairs.
            int total=bots.Length*bots.Length,probes=0;
            for(int scan=0;scan<total && probes<4;scan++)
            {
                int pair=_pairCursor++%total;var a=bots[pair/bots.Length];var b=bots[pair%bots.Length];
                if(a.PlayerIndex>=b.PlayerIndex || Friends(a,b) || TestMembers.Contains(a.PlayerIndex) || TestMembers.Contains(b.PlayerIndex))continue;
                if(!Social.ContainsKey(a.PlayerIndex))Social[a.PlayerIndex]=UnityEngine.Random.value<.35f;
                if(!Social.ContainsKey(b.PlayerIndex))Social[b.PlayerIndex]=UnityEngine.Random.value<.35f;
                if(!Social[a.PlayerIndex] || !Social[b.PlayerIndex])continue;
                int key=(a.PlayerIndex<<8)|b.PlayerIndex;
                if(Encounters.Contains(key) || (a.PlayerPosition-b.PlayerPosition).sqrMagnitude>55*55)continue;
                probes++;
                var delta=b.PlayerPosition-a.PlayerPosition;bool blocked=false;
                foreach(var hit in Physics.RaycastAll(a.PlayerPosition+Vector3.up*.7f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                {
                    if((a.PlayerObject && hit.collider.transform.IsChildOf(a.PlayerObject.transform)) ||
                       (b.PlayerObject && hit.collider.transform.IsChildOf(b.PlayerObject.transform)))continue;
                    blocked=true;break;
                }
                if(blocked)continue;
                var ca=a.PlayerObject?a.PlayerObject.GetComponent<AiDummyController>():null;
                var cb=b.PlayerObject?b.PlayerObject.GetComponent<AiDummyController>():null;
                if(ca==null || cb==null)continue;
                Encounters.Add(key); // One decision at their first visible encounter, not rerolled every tick.
                bool shared=Reports.TryGetValue(a.PlayerIndex,out var sa) && Reports.TryGetValue(b.PlayerIndex,out var sb) &&
                    sa.Enemy!=255 && sa.Enemy==sb.Enemy && sa.Enemy!=a.PlayerIndex && sa.Enemy!=b.PlayerIndex &&
                    Time.unscaledTime-sa.Time<4 && Time.unscaledTime-sb.Time<4;
                bool hostile=(a.LastAttacker==b.PlayerIndex && a.LastAttackTime>0 && Time.time-a.LastAttackTime<12) ||
                    (b.LastAttacker==a.PlayerIndex && b.LastAttackTime>0 && Time.time-b.LastAttackTime<12);
                float chance=BotCombatMind.AllianceChance(ca.Personality,cb.Personality,shared,hostile);
                if(UnityEngine.Random.value<chance && Pacts.Join(a.PlayerIndex,b.PlayerIndex))
                    FakePlayersPlugin.Log($"[BotPact] First encounter: {a.PlayerName} + {b.PlayerName}; reason={(shared?"common enemy":"personality")}, personalities={ca.Personality}/{cb.Personality}, size={Pacts.Size(a.PlayerIndex)}");
            }
        }
        public static void Noise(TABGPlayerServer source,Vector3 point,float range)
        {
            if(_room?.Players==null)return;
            foreach(var p in _room.Players)
                if(p.Bot && Alive(p) && !Friends(p,source) && (p.PlayerPosition-point).sqrMagnitude<range*range)
                    p.PlayerObject?.GetComponent<AiDummyController>()?.HearWorldNoise(source,point);
        }
        public static void Report(TABGPlayerServer bot,Vector3 position)
        {
            if(Reports.TryGetValue(bot.PlayerIndex,out var previous) && Time.unscaledTime-previous.Time<.5f)return;
            Reports[bot.PlayerIndex]=new Sighting{Position=position,Time=Time.unscaledTime,Enemy=255};
        }
        internal static void ReportEnemy(TABGPlayerServer bot,Vector3 position,byte enemy)
        {
            Report(bot,position);
            if(Reports.TryGetValue(bot.PlayerIndex,out var sight)){sight.Enemy=enemy;Reports[bot.PlayerIndex]=sight;}
        }
        internal static int FlankSide(TABGPlayerServer bot)
        {
            int rank=0;foreach(var mate in _room.Players)if(Alive(mate) && mate.PlayerIndex<bot.PlayerIndex && Friends(bot,mate))rank++;
            return rank%2==0?1:-1;
        }
        internal static TABGPlayerServer EngagedMate(TABGPlayerServer bot,byte enemy)
        {
            if(_room?.Players==null)return null;
            foreach(var mate in _room.Players)
                if(mate.Bot && mate.PlayerIndex<bot.PlayerIndex && Alive(mate) && Friends(bot,mate) && (mate.PlayerPosition-bot.PlayerPosition).sqrMagnitude<80*80 &&
                   Reports.TryGetValue(mate.PlayerIndex,out var sight) && sight.Enemy==enemy && Time.unscaledTime-sight.Time<1)return mate;
            return null;
        }
        internal static bool Hear(TABGPlayerServer bot,out Vector3 position,out float reportedAt)
        {
            position=Vector3.zero;reportedAt=0;if(_room?.Players==null)return false;
            float newest=Time.unscaledTime-6;
            foreach(var mate in _room.Players)
            {
                if(mate==bot || !mate.Bot || !Friends(bot,mate) || (mate.PlayerPosition-bot.PlayerPosition).sqrMagnitude>180*180)continue;
                if(Reports.TryGetValue(mate.PlayerIndex,out var sight) && sight.Time>newest){newest=sight.Time;position=sight.Position;}
            }
            reportedAt=newest;return newest>Time.unscaledTime-6;
        }
        internal static TABGPlayerServer Leader(TABGPlayerServer bot)
        {
            TABGPlayerServer leader=null;
            foreach(var mate in _room.Players)
                if(mate.Bot && Alive(mate) && mate.PlayerIndex<bot.PlayerIndex && Friends(bot,mate) && (leader==null || mate.PlayerIndex<leader.PlayerIndex))leader=mate;
            return leader;
        }
    }
}
