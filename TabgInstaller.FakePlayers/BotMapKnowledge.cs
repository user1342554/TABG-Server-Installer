using System.Collections.Generic;
using System.Linq;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal static class BotMapKnowledge
    {
        internal sealed class Place
        {
            internal readonly string Name;internal readonly Vector3 Position;internal readonly float Popularity;
            internal Place(string name,float x,float z,float popularity=1){Name=name;Position=new Vector3(x,130,z);Popularity=popularity;}
        }
        // Existing installer spawn coordinates; terrain height is resolved at route selection.
        private static readonly Place[] BasePlaces={
            new Place("Area 64",385,468,2),new Place("Industry",98,-95,2),new Place("Tall Work",-313,-530,1.8f),
            new Place("Circle Town",141,-375,1.8f),new Place("Finland",-573,301,1.8f),new Place("Big Work",366,-646,1.8f),new Place("City",-674,-8,1.8f),
            new Place("Western",659,600),new Place("Actual Castle",-689,510),new Place("Crappy Castle",-392,400),
            new Place("Containers",-169,159),new Place("Big Power",723,-689),new Place("Small Power",439,-274),
            new Place("Chaos",-13,-523),new Place("Oasis",631,487),new Place("Pyramid",636,240),new Place("Sandcastle",685,8),
            new Place("Long Wall",433,73),new Place("Point of Impact",-74,611),new Place("Snow Castle",-385,-771),
            new Place("Normandie",-411,517),new Place("WW2 Town",-465,-481),new Place("Fields",-254,-128),new Place("West Works",-422,82)
        };
        private static readonly List<Place> Places=new List<Place>();private static GameRoom _room;
        private static GameRoom _flightRoom;private static Vector3 _flightStart,_flightEnd;private static bool _hasFlight;
        internal static void ObserveFlight(ServerClient server)
        {
            if(_flightRoom!=server.GameRoomReference){_flightRoom=server.GameRoomReference;_hasFlight=false;}
            if(_flightRoom?.CurrentGameState!=GameState.Flying)return;
            var plane=server.GetSpawnedPlane();if(!plane)return;
            if(!_hasFlight){_flightStart=plane.transform.position;_hasFlight=true;}
            _flightEnd=plane.transform.position;
        }
        internal static float FlightDistance(Vector3 point)
        {
            if(!_hasFlight)return 0;
            var delta=Vector3.ProjectOnPlane(_flightEnd-_flightStart,Vector3.up);
            float t=delta.sqrMagnitude>1?Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(point-_flightStart,Vector3.up),delta)/delta.sqrMagnitude):0;
            return Vector3.ProjectOnPlane(point-(_flightStart+delta*t),Vector3.up).magnitude;
        }
        internal static IReadOnlyList<Place> Known(GameRoom room)
        {
            if(_room==room && Places.Count>0)return Places;
            _room=room;Places.Clear();Places.AddRange(BasePlaces);
            // Map-authored hotspot markers extend the catalogue, including Factory when present.
            foreach(var marker in Resources.FindObjectsOfTypeAll<RingHotspot>())
            {
                if(!marker || !marker.gameObject.scene.IsValid())continue;
                var p=marker.transform.position;
                if(Places.Any(place=>Vector3.ProjectOnPlane(place.Position-p,Vector3.up).sqrMagnitude<60*60))continue;
                string label=marker.name;bool factory=false;
                for(var t=marker.transform;t;t=t.parent)if(t.name.ToLowerInvariant().Contains("factory")){label="Factory";factory=true;break;}
                Places.Add(new Place(label,p.x,p.z,factory?1.8f:1));
            }
            FakePlayersPlugin.Log($"[BotRoute] Map knowledge: {Places.Count} destinations.");return Places;
        }
    }
}
