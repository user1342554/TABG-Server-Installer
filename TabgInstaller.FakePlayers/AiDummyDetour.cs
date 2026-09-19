using System.Collections.Generic;
using TabgInstaller.Vehicles;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private BotGridSearch _detour;
        private Vector3 _detourOrigin,_detourDestination;
        private float _detourRetry,_detourStarted;
        private int _detourStep;
        private bool _navigationWaiting,_detourReported;
        private float _routeProgressAt,_routeProgressDistance;
        private float GridSpacing=2;
        private float _fineNavigationUntil;
        private struct KnownEdge{internal bool Clear;internal float Until;}
        private static readonly Dictionary<(Vector3Int,Vector3Int),KnownEdge> KnownWalkingEdges=new Dictionary<(Vector3Int,Vector3Int),KnownEdge>();
        private readonly Dictionary<BotGridCell,Vector3> _detourGround=new Dictionary<BotGridCell,Vector3>();
        private readonly HashSet<BotGridCell> _detourInvalid=new HashSet<BotGridCell>();
        private static int _detourFrame,_detourCursor;
        private static readonly List<AiDummyController> DetourWorkers=new List<AiDummyController>();
        internal static void ResetDetours(){DetourWorkers.Clear();KnownWalkingEdges.Clear();_detourCursor=0;}
        private static void WorkOnDetours()
        {
            if(_detourFrame==Time.frameCount)return;_detourFrame=Time.frameCount;
            DetourWorkers.RemoveAll(c=>!c || c._detour==null || c._detour.Done);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<24 && DetourWorkers.Count>0 && watch.Elapsed.TotalMilliseconds<3;i++)
            {var worker=DetourWorkers[_detourCursor++%DetourWorkers.Count];if(!worker._detour.Done)worker._detour.Step(1);}
            if(_detourCursor>1000000)_detourCursor=0;
        }
        private bool DetourGround(BotGridCell cell,out Vector3 point)
        {
            if(_detourGround.TryGetValue(cell,out point))return true;
            point=_detourOrigin+new Vector3(cell.X*GridSpacing,0,cell.Z*GridSpacing);
            if(_detourInvalid.Contains(cell))return false;
            if(!TryFindGroundInfo(point,out var ground) || ground.BadTerrain)
            {_detourInvalid.Add(cell);return false;}
            point.y=ground.Y+_terrainHeightOffset;
            _detourGround[cell]=point;return true;
        }
        private bool DetourEdge(BotGridCell a,BotGridCell b)
        {
            if(!DetourGround(a,out var start) || !DetourGround(b,out var end))return false;
            var aKey=new Vector3Int(Mathf.RoundToInt(start.x*100),Mathf.RoundToInt(start.y*2),Mathf.RoundToInt(start.z*100));
            var bKey=new Vector3Int(Mathf.RoundToInt(end.x*100),Mathf.RoundToInt(end.y*2),Mathf.RoundToInt(end.z*100));
            var key=(aKey,bKey);
            if(KnownWalkingEdges.TryGetValue(key,out var known) && Time.unscaledTime<known.Until)return known.Clear;
            bool clear=ClearWalkingSegment(start,end);
            if(KnownWalkingEdges.Count>24000)KnownWalkingEdges.Clear();
            KnownWalkingEdges[key]=new KnownEdge{Clear=clear,Until=Time.unscaledTime+30};return clear;
        }
        private bool ClearWalkingSegment(Vector3 start,Vector3 end)=>WalkSegment(start,end,out _);
        private bool ResolveDetour(Vector3 current,Vector3 destination,out Vector3 waypoint)
        {
            waypoint=current;_navigationWaiting=false;
            if(_detour!=null && Flat(destination-_detourDestination).sqrMagnitude>2*2){_detour=null;_detourRetry=0;}
            if(_detour==null)
            {
                if(Time.unscaledTime<_detourRetry || ClearWalkingSegment(current,current+Vector3.ClampMagnitude(destination-current,12)))return false;
                _detourRetry=Time.unscaledTime+1;GridSpacing=Time.unscaledTime<_fineNavigationUntil?.5f:2f;_detourOrigin=new Vector3(Mathf.Round(current.x/GridSpacing)*GridSpacing,current.y,Mathf.Round(current.z/GridSpacing)*GridSpacing);_detourDestination=destination;_detourStarted=Time.unscaledTime;
                if(!ClearWalkingSegment(current,_detourOrigin))_detourOrigin=current;
                if(TryFindGroundY(_detourOrigin,out float originGround))_detourOrigin.y=originGround+_terrainHeightOffset;
                _detourGround.Clear();_detourInvalid.Clear();_detourGround[new BotGridCell(0,0)]=_detourOrigin;
                var direction=Vector3.ClampMagnitude(Flat(destination-_detourOrigin),GridSpacing*40);
                _detour=new BotGridSearch(Mathf.RoundToInt(direction.x/GridSpacing),Mathf.RoundToInt(direction.z/GridSpacing),48,DetourEdge,4096);_detourStep=1;_detourReported=false;
                if(!DetourWorkers.Contains(this))DetourWorkers.Add(this);
            }
            if(!_detour.Done)
            {
                WorkOnDetours();
                if(Time.unscaledTime-_detourStarted>25){_detour=null;_detourRetry=Time.unscaledTime+2;return false;}
                if(!_detour.Done){_navigationWaiting=true;return true;}
            }
            if(!_detourReported)
            {
                _detourReported=true;
                _routeProgressAt=Time.unscaledTime;_routeProgressDistance=float.PositiveInfinity;
                if(!_detour.Reached && Flat(_detourDestination-current).sqrMagnitude<90*90)
                {RejectWalkGoal(_detourDestination);_walkGoalUntil=0;}
                FakePlayersPlugin.Log($"[BotPath] {_player.PlayerName}: routeComputed={_detour.Reached}, nodes={_detour.Expanded}, waypoints={_detour.Path.Count}, spacing={GridSpacing}, from={current}, goal={destination}");
            }
            if(_detour.Path.Count<2)
            {
                if(GridSpacing>1 && _detour.Expanded<150)
                {
                    // Coarse cells can miss a narrow doorway: retry locally at half-metre resolution.
                    _fineNavigationUntil=Time.unscaledTime+30;_detour=null;_detourRetry=0;_navigationWaiting=true;return true;
                }
                if(_wantedLoot!=null && Flat(_wantedLoot.Position-_detourDestination).sqrMagnitude<6*6)
                {
                    FakePlayersPlugin.Log($"[BotPath] {_player.PlayerName}: no reachable route to {_wantedLoot.WeaponName}; choosing other loot.");
                    MarkLootTemporarilyBlocked(_wantedLoot);ReleaseLootClaim();_wantedLoot=null;_lootTimer=0;_lootProgressTimer=0;
                }
                _detour=null;_detourRetry=Time.unscaledTime+2;return false;
            }
            int previousStep=_detourStep;
            while(_detourStep<_detour.Path.Count && Flat(_detourGround[_detour.Path[_detourStep]]-current).magnitude<.7f)_detourStep++;
            if(_detourStep>=_detour.Path.Count){_detour=null;return false;}
            // Skip a corner only after checking the entire shortcut, so stairs/walls are not cut.
            while(_detourStep+1<_detour.Path.Count && (_detourGround[_detour.Path[_detourStep+1]]-current).sqrMagnitude<7*7 && ClearWalkingSegment(current,_detourGround[_detour.Path[_detourStep+1]]))_detourStep++;
            if(previousStep!=_detourStep){_routeProgressAt=Time.unscaledTime;_routeProgressDistance=float.PositiveInfinity;}
            waypoint=_detourGround[_detour.Path[_detourStep]];
            float remaining=Flat(waypoint-current).magnitude;
            if(remaining<_routeProgressDistance-.5f){_routeProgressDistance=remaining;_routeProgressAt=Time.unscaledTime;}
            else if(Time.unscaledTime-_routeProgressAt>6){_fineNavigationUntil=Time.unscaledTime+30;_detour=null;KnownWalkingEdges.Clear();_detourRetry=0;return false;}
            return true;
        }
    }
}
