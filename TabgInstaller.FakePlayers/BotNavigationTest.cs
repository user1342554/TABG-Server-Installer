using System;
using System.Collections.Generic;
using Landfall.Network;
using CitrusLib;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    public partial class FakePlayersPlugin
    {
        private void QueueNavigationTest(ServerClient server,GameRoom room,TABGPlayerServer observer,byte[] ids,float deadline)
        {
            server.WaitThenDoAction(1.5f,()=>
            {
                if(server.GameRoomReference!=room || observer.IsDead || Time.unscaledTime>deadline)
                {foreach(byte id in ids)PendingTestBots.Remove(id);_pendingTests=Math.Max(0,_pendingTests-1);return;}
                if(!observer.HasDropped || Array.Exists(ids,id=>room.FindPlayer(id)?.PlayerObject?.GetComponent<AiDummyController>()==null))
                {QueueNavigationTest(server,room,observer,ids,deadline);return;}
                var starts=new[]{new Vector3(-118.29f,122.13f,171.13f),new Vector3(-179.06f,125.13f,239.50f),new Vector3(-176.50f,130.51f,267.22f)};
                var goals=new[]{new Vector3(-111.65f,122.13f,163.66f),new Vector3(-185.70f,125.13f,246.97f),new Vector3(-159.09f,130.57f,264.24f)};
                var names=new[]{"SkillIssue-slope","Dosenbier-escape","Emotional-tree"};
                for(int i=0;i<ids.Length && i<3;i++)
                {
                    var controller=room.FindPlayer(ids[i])?.PlayerObject?.GetComponent<AiDummyController>();
                    if(controller!=null){if(i==0)controller.TestWalkingGeometry();controller.PrepareNavigationCase(starts[i],goals[i],names[i]);}
                    PendingTestBots.Remove(ids[i]);
                }
                _pendingTests=Math.Max(0,_pendingTests-1);
                Citrus.SelfParrot(observer,"Drei Bewegungsfaelle gestartet. Ergebnisse nach spaetestens 60 Sekunden im Serverlog.");
            });
        }
    }
    internal partial class AiDummyController
    {
        private float _navigationCaseUntil,_navigationCaseLogAt;
        private string _navigationCaseName;
        private Vector3 _navigationCaseStart,_navigationCaseGoal;
        internal void PrepareNavigationCase(Vector3 start,Vector3 goal,string name)
        {
            _recovery.Cancel();ResetWalkingRoute();_progressInitialized=false;_plannedJump=_jumpSettling=false;_walkGoalUntil=0;ReleaseLootClaim();_wantedLoot=null;
            _navigationCaseName=name;_navigationCaseStart=start;_navigationCaseGoal=goal;_navigationCaseUntil=Time.unscaledTime+60;_navigationCaseLogAt=0;
            _dropStarted=false;_dropFinished=true;_dropPositionLockTimer=0;_warmupTimer=0;_player.Dropped();
            ForceServerPosition(start);_server.ForceChunkEntry(_player);FakePlayersPlugin.BroadcastRespawn(_server,_player,start);
            var originalGoal=goal;goal=ResolveWalkDestination(goal);_navigationCaseGoal=goal;
            FakePlayersPlugin.Log($"[BotNavigationTest] {name}: goalValidated original={originalGoal}; valid={goal}; shifted={Flat(originalGoal-goal).magnitude:F2}");
            FakePlayersPlugin.Log($"[BotNavigationTest] {name}: START; bot={_player.PlayerName}; from={start}; goal={goal}");
        }
        private bool TickNavigationCase(float dt)
        {
            if(_navigationCaseUntil<=0)return false;
            if(!Recovering)_state=AiState.Advancing;
            _strategicTravel=true;MoveToward(_navigationCaseGoal,dt);
            FakePlayersPlugin.BroadcastPlayerUpdate(_server,_player,_player.PlayerPosition);
            var actualGoal=_walkGoalResolved;
            bool fallback=Flat(actualGoal-_navigationCaseGoal).magnitude>.5f;
            float distance=Flat(_player.PlayerPosition-actualGoal).magnitude;
            bool arrived=distance<.8f && Flat(_player.PlayerPosition-_navigationCaseStart).magnitude>3 && !Recovering;
            if(fallback && Flat(actualGoal-_navigationCaseGoal).magnitude>6.1f)arrived=false;
            if(Time.unscaledTime>=_navigationCaseLogAt)
            {_navigationCaseLogAt=Time.unscaledTime+3;FakePlayersPlugin.Log($"[BotNavigationTest] {_navigationCaseName}: pos={_player.PlayerPosition}; remaining={distance:F2}; recovery={Recovering}; nodes={_detour?.Expanded}; pending={_navigationWaiting}");}
            if(arrived || Time.unscaledTime>=_navigationCaseUntil)
            {
                FakePlayersPlugin.Log($"[BotNavigationTest] {_navigationCaseName}: {(arrived?"PASS":"FAIL")}; displaced={Flat(_player.PlayerPosition-_navigationCaseStart).magnitude:F2}; remaining={distance:F2}; fallback={fallback}; requestedGoal={_navigationCaseGoal}; actualGoal={actualGoal}; pos={_player.PlayerPosition}");
                _navigationCaseUntil=0;_recovery.Cancel();ResetWalkingRoute();_decisionTimer=0;
            }
            return true;
        }
        internal void TestWalkingContact(Vector3 position)
        {
            WalkCapsule(position,out var bottom,out var top);var center=(bottom+top)*.5f;
            FakePlayersPlugin.Log($"[BotContact] pos={position}; offset={_terrainHeightOffset}; body={bottom} -> {top}");
            foreach(var c in Physics.OverlapCapsule(bottom,top,WalkRadius,~0,QueryTriggerInteraction.Ignore))
            {
                if(IgnoreWalkCollider(c))continue;
                bool penetrates=Physics.ComputePenetration(WalkProxy(),center,Quaternion.identity,c,c.transform.position,c.transform.rotation,out var normal,out float depth);
                FakePlayersPlugin.Log($"[BotContact] collider={c.name}/{c.GetType().Name}; penetration={penetrates}; normal={normal}; depth={depth:F6}; bounds={c.bounds}");
            }
            for(int i=0;i<8;i++)
            {
                var step=Quaternion.Euler(0,i*45,0)*Vector3.forward*.08f;
                bool ground=WalkGround(position+step,position,out var next,out var block);
                bool move=ground && SweepBody(position,next,out block,true);
                FakePlayersPlugin.Log($"[BotContact] direction={i*45}; allowed={move}; next={next}; block={block}");
            }
        }
        internal void TestWalkingGeometry()
        {
            _recovery.Cancel();_plannedJump=_jumpSettling=false;_jumpCooldown=0;
            var objects=new List<GameObject>();Mesh combined=null;int checks=0;
            GameObject Box(string name,Vector3 center,Vector3 size)
            {var go=new GameObject(name);go.transform.position=center;go.AddComponent<BoxCollider>().size=size;objects.Add(go);return go;}
            void Check(bool ok,string name){checks++;FakePlayersPlugin.Log($"[BotGeometry] {(ok?"PASS":"FAIL")} {name}");}
            var start=new Vector3(780,450.5f+_terrainHeightOffset,680);
            try
            {
                Box("NavTestFloor",new Vector3(780,450,680),new Vector3(30,1,30));Physics.SyncTransforms();
                Check(WalkSegment(start,start+Vector3.right*4,out _),"open ground");
                var tree=Box("NavTestTrunk",new Vector3(780,451.5f,680),new Vector3(.8f,2, .8f));Physics.SyncTransforms();
                var touching=start+Vector3.right*.65f;
                Check(BodyOverlap(touching,out _),"detect initial trunk overlap");
                Check(SweepBody(touching,touching+Vector3.right*.08f,out _,true),"short outward step reduces penetration");
                Check(!SweepBody(touching,touching-Vector3.right*.08f,out _,true),"reject step deeper into trunk");
                var barely=start+Vector3.right*.749f;
                Check(BodyOverlap(barely,out _),"detect one millimetre side overlap");
                Check(SweepBody(barely,barely+Vector3.right*.08f,out _,true),"escape one millimetre side overlap");
                Check(SweepBody(start+Vector3.right*.75f,start+Vector3.right*.83f,out _),"leave exact tangential contact");
                tree.SetActive(false);
                var supported=start-Vector3.up*.265f;
                Check(WalkSegment(supported,supported+Vector3.forward*2,out _),"shallow ground overlap permits grounded travel");
                combined=new Mesh();combined.vertices=new[]{new Vector3(-3,0,-3),new Vector3(3,0,-3),new Vector3(3,0,3),new Vector3(-3,0,3),new Vector3(1,0,-3),new Vector3(1,3,-3),new Vector3(1,3,3),new Vector3(1,0,3)};
                combined.triangles=new[]{0,2,1,0,3,2,4,6,5,4,7,6};combined.RecalculateBounds();
                var mixed=new GameObject("NavTestFloorAndWall");mixed.transform.position=new Vector3(780,450.5f,680);mixed.AddComponent<MeshCollider>().sharedMesh=combined;objects.Add(mixed);Physics.SyncTransforms();
                Check(!WalkSegment(supported,supported+Vector3.right*2,out _),"ground contact never masks wall on same mesh");
                mixed.SetActive(false);
                Box("NavTestDoorLeft",new Vector3(786,452,675.25f),new Vector3(.2f,3,8.5f));
                Box("NavTestDoorRight",new Vector3(786,452,684.75f),new Vector3(.2f,3,8.5f));Physics.SyncTransforms();
                Check(WalkSegment(start+Vector3.right*3,start+Vector3.right*9,out _),"one metre doorway uses same body as motor");
                var wall=Box("NavTestTallWall",new Vector3(781,452,680),new Vector3(.4f,3,3));Physics.SyncTransforms();
                _jumpCooldown=0;Check(!TryStartJump(start,start+Vector3.right*3),"no jump into tall wall");wall.SetActive(false);
                Box("NavTestLowStep",new Vector3(781,450.75f,680),new Vector3(.4f,.5f,3));Physics.SyncTransforms();
                _jumpCooldown=0;Check(TryStartJump(start,start+Vector3.right*3),"jump has clear arc and landing behind low obstacle");
                _plannedJump=false;_recovery.Cancel();
                Box("NavTestDropLedge",new Vector3(781.7f,451.1f,676),new Vector3(.8f,1.2f,1));Physics.SyncTransforms();
                var ledge=new Vector3(781.7f,451.7f+_terrainHeightOffset,676);var below=new Vector3(782.8f,450.5f+_terrainHeightOffset,676);
                Check(ClearWalkingDrop(ledge,below,out _),"short ledge descent has clear horizontal exit and vertical fall");
                Check(!ClearWalkingDrop(ledge,below-Vector3.up*4,out _),"reject excessive drop height");
            }
            catch(Exception ex){FakePlayersPlugin.Log("[BotGeometry] ERROR "+ex);}
            finally
            {
                _plannedJump=false;_jumpCooldown=0;
                foreach(var go in objects){go.SetActive(false);UnityEngine.Object.Destroy(go);}if(combined)UnityEngine.Object.Destroy(combined);Physics.SyncTransforms();
                FakePlayersPlugin.Log($"[BotGeometry] finished {checks} checks");
            }
        }
    }
}
