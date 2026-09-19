using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal static class BotGrenades
    {
        private sealed class Info{internal float Fuse=2.5f,Radius,Damage;internal bool Shield,Launch;internal Pickup Prefab;}
        private struct Cover{internal Vector3 Point;internal float From,Until;}
        private static readonly Dictionary<int,Info> Catalog=new Dictionary<int,Info>();
        private static readonly List<Cover> Shields=new List<Cover>();
        private static TrejectoryCalculator _planner;private static float _nextPlan;
        private const BindingFlags NativeInstance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly MethodInfo Next=typeof(TrejectoryCalculator).GetMethod("GetNextPoint",NativeInstance);
        private static readonly FieldInfo Velocity=typeof(TrejectoryCalculator).GetField("velocity",NativeInstance),Done=typeof(TrejectoryCalculator).GetField("done",NativeInstance);
        internal static void Reset(){Catalog.Clear();Shields.Clear();_nextPlan=0;if(_planner)UnityEngine.Object.Destroy(_planner.gameObject);_planner=null;}
        internal static bool IsOffensive(NetworkGun item)
        {
            string n=(item?.WeaponName??"").ToLowerInvariant();
            return !n.Contains("launch") && !n.Contains("shield") && !n.Contains("smoke") && !n.Contains("fpv") && (n.Contains("grenade") || n.Contains("dynamite"));
        }
        internal static bool IsDamaging(GameRoom room,NetworkGun item)
        {
            if(!IsOffensive(item))return false;
            if(Catalog.TryGetValue(item.UniqueIdentifier,out var known))return known.Damage>0;
            return Describe(room,item)?.Damage>0;
        }
        private static Info Describe(GameRoom room,NetworkGun item)=>Describe(room,item.UniqueIdentifier,item.WeaponName);
        private static Info Describe(GameRoom room,int itemId,string name)
        {
            if(Catalog.TryGetValue(itemId,out var cached))return cached;
            var prefab=room.GetItem(itemId);if(!prefab)return null;
            var info=new Info{Prefab=prefab,Launch=(name??"").ToLowerInvariant().Contains("launch"),Shield=(name??"").IndexOf("shield",StringComparison.OrdinalIgnoreCase)>=0};
            var timer=prefab.GetComponentInChildren<CountEvent>(true);
            if(timer)info.Fuse=Mathf.Clamp(timer.secondsToCount, .5f,8);
            var queue=new Queue<GameObject>();var visited=new HashSet<int>();queue.Enqueue(prefab.gameObject);
            while(queue.Count>0 && visited.Count<20)
            {
                var node=queue.Dequeue();if(!node || !visited.Add(node.GetInstanceID()))continue;
                foreach(var blast in node.GetComponentsInChildren<Explosion>(true))
                    if(blast.damage>info.Damage){info.Damage=blast.damage;info.Radius=blast.radius;}
                foreach(var spawn in node.GetComponentsInChildren<SpawnObject>(true))if(spawn.objectToSpawn)queue.Enqueue(spawn.objectToSpawn);
            }
            Catalog[itemId]=info;return info;
        }
        internal static bool Protected(Vector3 point)
        {
            Shields.RemoveAll(s=>Time.unscaledTime>s.Until);
            return Shields.Any(s=>Time.unscaledTime>=s.From && (point-s.Point).sqrMagnitude<6*6);
        }
        internal static bool Blocks(Vector3 origin,Vector3 end)
        {var delta=end-origin;return FirstShieldHit(origin,delta.normalized,delta.magnitude)<=delta.magnitude;}
        internal static float FirstShieldHit(Vector3 origin,Vector3 direction,float distance)
        {
            Shields.RemoveAll(s=>Time.unscaledTime>s.Until);
            float first=float.PositiveInfinity;
            foreach(var s in Shields)
            {
                if(Time.unscaledTime<s.From)continue;
                var offset=origin-s.Point;
                first=Mathf.Min(first,TabgInstaller.Vehicles.BotShieldRules.FirstHit(offset.x,offset.y,offset.z,direction.x,direction.y,direction.z,6,distance));
            }
            return first;
        }
        internal static bool TryThrow(ServerClient server,TABGPlayerServer bot,NetworkGun item,Vector3 target,bool protection,out float flight)
        {
            flight=0;if(bot.IsDead || bot.IsDowned || bot.IsInsideCar || bot.HasLoot(item.UniqueIdentifier)<=0 || Time.unscaledTime<_nextPlan)return false;
            _nextPlan=Time.unscaledTime+.5f;
            var info=Describe(server.GameRoomReference,item);if(info==null || (!info.Shield && !info.Launch && info.Damage<=0))return false;
            Vector3 origin=bot.PlayerPosition+Vector3.up*1.35f;
            if(!Plan(bot,info,origin,target,out Vector3 rotation,out Vector3 landing))return false;
            float radius=info.Shield?6:Mathf.Max(6,info.Radius);
            if(!info.Shield && !info.Launch && server.GameRoomReference.Players.Any(p=>!p.IsDead && BotTactics.Friends(bot,p) && (p.PlayerPosition-landing).sqrMagnitude<(radius+2)*(radius+2)))return false;
            if((target-landing).sqrMagnitude>(protection?6*6:radius*radius))return false;
            int before=bot.HasLoot(item.UniqueIdentifier);bot.RemoveLoot(item.UniqueIdentifier,1);
            if(bot.HasLoot(item.UniqueIdentifier)!=before-1)return false;
            int id=ServerMessages.SendGrenadeThrow(server,bot,item.UniqueIdentifier,1,origin,rotation,true);
            flight=info.Fuse;var room=server.GameRoomReference;
            if(info.Launch)BotLaunchPads.Temporary(room,landing,Time.unscaledTime+info.Fuse);
            if(!info.Shield && !info.Launch)foreach(var observer in room.Players)if(observer.Bot && !observer.IsDead)observer.PlayerObject?.GetComponent<AiDummyController>()?.ObserveGrenade(origin,landing,info.Fuse,info.Radius);
            room.AddNewProjectileSyncIndex(id);
            if(info.Shield)Shields.Add(new Cover{Point=landing,From=Time.unscaledTime+info.Fuse,Until=Time.unscaledTime+info.Fuse+7});
            server.WaitThenDoAction(info.Fuse,()=>
            {
                if(server.GameRoomReference!=room)return;
                // The native SyncProjectileEvent activates the native grenade's own ActionEvent.
                using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
                {w.Write(id);w.Write(false);w.Write(false);w.Write(true);w.Write(false);w.Write((byte)0);ServerMessages.SendToRealClients(server,(EventCode)58,stream.ToArray(),true);}
                if(!info.Shield && !info.Launch)
                {
                    BotTactics.Noise(bot,landing,160);
                    foreach(var victim in room.Players.ToArray())
                    {
                        if(!victim.Bot || victim.IsDead || BotTactics.Friends(bot,victim))continue;
                        float distance=Vector3.Distance(victim.PlayerPosition,landing);if(distance>=info.Radius)continue;
                        bool wall=Physics.RaycastAll(landing+Vector3.up*.3f,(victim.PlayerPosition+Vector3.up-landing).normalized,distance,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.attachedRigidbody && !h.collider.GetComponentInParent<ServerNetworkVehicle>() && h.normal.y<.8f);
                        if(!wall)FakePlayersPlugin.ApplyBlastDamage(server,bot,victim,info.Damage*(1-distance/Mathf.Max(1,info.Radius)),"bot grenade");
                    }
                }
                if(info.Shield || info.Launch)server.WaitThenDoAction(info.Launch?20:8,()=>{if(server.GameRoomReference==room)room.RemoveProjectileSyncIndex(id);});else room.RemoveProjectileSyncIndex(id);
            });
            FakePlayersPlugin.Log($"[BotGrenade] {bot.PlayerName}: {item.WeaponName} #{id}, shield={info.Shield}, target={target}, predicted={landing}, stock={bot.HasLoot(item.UniqueIdentifier)}");
            return true;
        }
        private static float ThrowForce(TABGPlayerServer bot,string field,float fallback)
        {
            var handler=bot.PlayerObject?bot.PlayerObject.GetComponentInChildren<InteractionHandler>():null;
            return handler?(float)AccessTools.Field(typeof(InteractionHandler),field).GetValue(handler):fallback;
        }
        private static void PreparePlanner(TABGPlayerServer bot,Info info)
        {
            if(!_planner){var node=new GameObject("Native bot throw planner");_planner=node.AddComponent<TrejectoryCalculator>();_planner.enabled=false;}
            var source=info.Prefab.GetComponent<TrejectoryCalculator>();
            foreach(string name in new[]{"radius","gravity","airDrag","drag","stopAtFirstCollision"})
            {var field=AccessTools.Field(typeof(TrejectoryCalculator),name);if(source && field!=null)field.SetValue(_planner,field.GetValue(source));}
            AccessTools.Field(typeof(TrejectoryCalculator),"frameLength").SetValue(_planner,.02f);
            var handler=bot.PlayerObject?bot.PlayerObject.GetComponentInChildren<InteractionHandler>():null;
            float forward=handler?(float)AccessTools.Field(typeof(InteractionHandler),"m_throwingForwardForce").GetValue(handler):17;
            float up=handler?(float)AccessTools.Field(typeof(InteractionHandler),"m_throwingUpForce").GetValue(handler):3;
            _planner.mask=handler?(LayerMask)AccessTools.Field(typeof(InteractionHandler),"m_throwingMask").GetValue(handler):(LayerMask)~0;
        }
        private static Vector3 Trace(TABGPlayerServer bot,Info info,Vector3 origin,Vector3 direction)
        {
            float forward=ThrowForce(bot,"m_throwingForwardForce",17);
                _planner.transform.position=origin;_planner.transform.rotation=Quaternion.LookRotation(direction);
                Done.SetValue(_planner,false);Velocity.SetValue(_planner,direction*forward+Vector3.up*ThrowForce(bot,"m_throwingUpForce",3));
                AccessTools.Field(typeof(TrejectoryCalculator),"firstCollision").SetValue(_planner,true);
                var collisions=AccessTools.Field(typeof(TrejectoryCalculator),"collisions").GetValue(_planner) as System.Collections.IList;collisions?.Clear();
                var point=origin;
                for(int i=0;i<Mathf.Min(400,Mathf.CeilToInt(info.Fuse/.02f)) && !(bool)Done.GetValue(_planner);i++)point=(Vector3)Next.Invoke(_planner,new object[]{point});
            return point;
        }
        internal static void ObserveNativeThrow(ServerClient server,TABGPlayerServer source,int itemId,Vector3 origin,Vector3 rotation)
        {
            if(Time.unscaledTime<_nextPlan)return;
            var prefab=server.GameRoomReference.GetItem(itemId);
            if(!prefab || prefab.weaponType!=Pickup.WeaponType.Grenade || prefab.itemName.ToLowerInvariant().Contains("fpv"))return;
            var info=Describe(server.GameRoomReference,itemId,prefab.itemName);
            if(info==null || info.Damage<=0)return;
            _nextPlan=Time.unscaledTime+.5f;PreparePlanner(source,info);
            var landing=Trace(source,info,origin,Quaternion.Euler(rotation)*Vector3.forward);
            foreach(var p in server.GameRoomReference.Players)
                if(p.Bot && !p.IsDead)p.PlayerObject?.GetComponent<AiDummyController>()?.ObserveGrenade(origin,landing,info.Fuse,info.Radius);
            var room=server.GameRoomReference;
            server.WaitThenDoAction(info.Fuse,()=>{if(server.GameRoomReference==room)BotTactics.Noise(source,landing,160);});
        }
        private static bool Plan(TABGPlayerServer bot,Info info,Vector3 origin,Vector3 target,out Vector3 rotation,out Vector3 landing)
        {
            rotation=Vector3.zero;landing=origin;
            PreparePlanner(bot,info);
            var source=info.Prefab.GetComponent<TrejectoryCalculator>();
            float forward=ThrowForce(bot,"m_throwingForwardForce",17);
            var toward=target-origin;toward.y=0;if(toward.sqrMagnitude<1)return false;
            float best=float.PositiveInfinity;
            // Two bounded native trajectory probes per attempt. Try low, then high arc over cover.
            float horizontal=toward.magnitude,gravity=source?source.gravity:15;
            float elevation=Mathf.Clamp(Mathf.Asin(Mathf.Clamp(horizontal*gravity/(forward*forward),0,1))*Mathf.Rad2Deg*.5f-8,-35,40);
            foreach(float angle in new[]{elevation,65-elevation})
            {
                var direction=toward.normalized*Mathf.Cos(angle*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(angle*Mathf.Deg2Rad);
                var point=Trace(bot,info,origin,direction);
                float error=(point-target).sqrMagnitude;
                if(error<best){best=error;landing=point;rotation=Quaternion.LookRotation(direction).eulerAngles;}
            }
            return !float.IsInfinity(best);
        }
    }
}
