using System;
using System.Linq;
using TabgInstaller.FlyingControls;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    public sealed class VehicleDiagnostics : MonoBehaviour
    {
        private float _next=10f;
        private int _attempt;
        private void Update()
        {
            if(Time.unscaledTime<_next || _attempt>=3)return;
            _next=Time.unscaledTime+20f;_attempt++;
            try
            {
                GrenadeProfiles.Load();
                if (_attempt==1)
                foreach(var entry in LootDatabase.Instance.GetPickups())
                {
                    if (entry.pickup == null || !(entry.pickup.itemName.IndexOf("grenade",StringComparison.OrdinalIgnoreCase)>=0 || entry.pickup.itemName.IndexOf("dynam",StringComparison.OrdinalIgnoreCase)>=0)) continue;
                    Debug.Log($"[VehicleBalance] Loot {entry.pickup.itemName}: {entry.prefab.name}; spawns={string.Join(",",entry.prefab.GetComponentsInChildren<SpawnObject>(true).Select(s=>s.objectToSpawn?s.objectToSpawn.name:"null").ToArray())}; explosions={entry.prefab.GetComponentsInChildren<Explosion>(true).Length}");
                }
                if(_attempt==1)
                foreach(var mesh in Resources.FindObjectsOfTypeAll<MeshCollider>().Where(m=>m.gameObject.scene.IsValid() && (m.name.IndexOf("road",StringComparison.OrdinalIgnoreCase)>=0 || m.transform.parent?.name.IndexOf("road",StringComparison.OrdinalIgnoreCase)>=0)).Take(10))
                {
                    Debug.Log($"[VehicleBalance] Road mesh {mesh.name} parent={mesh.transform.parent?.name} vertices={mesh.sharedMesh?.vertexCount}");
                    if(mesh.sharedMesh && mesh.sharedMesh.isReadable)
                    Debug.Log($"[VehicleBalance] Vertices={string.Join(";",mesh.sharedMesh.vertices.Take(12).Select(v=>v.ToString()).ToArray())}; UV={string.Join(";",mesh.sharedMesh.uv.Take(12).Select(v=>v.ToString()).ToArray())}");
                }
                if(_attempt==1 && CarDatabase.Instance!=null)
                for(int i=0;i<CarDatabase.Instance.ItemCount;i++)
                {
                    var prefab=CarDatabase.Instance.GetDataEntry(i).prefab;
                    if(!prefab)continue;
                    var parts=prefab.GetComponentsInChildren<Damagable>(true);
                    Debug.Log($"[VehicleBalance] Prefab {prefab.name}: damage parts={parts.Length}; health={string.Join(",",parts.Select(d=>d.health.ToString()).ToArray())}");
                }
                if (_attempt == 1)
                {
                    var route=RoadNetwork.Route(Vector3.zero,new Vector3(500,140,500));
                    Debug.Log($"[VehicleBalance] Real map route probe: {route?.Count ?? 0} points");
                }
                var roads=Resources.FindObjectsOfTypeAll<EasyRoads3Dv3.ERModularRoad>().Where(r=>r.gameObject.scene.IsValid()).ToArray();
                Debug.Log($"[VehicleBalance] Scene roads={roads.Length}; spline points={roads.Sum(r=>r.splinePoints?.Count??0)}");
                foreach(var road in roads.Take(2))Debug.Log($"[VehicleBalance] Road {road.name}: transform={road.transform.position}; first spline={road.splinePoints?.FirstOrDefault()}");
            }
            catch(Exception ex){Debug.LogWarning("[VehicleBalance] Diagnostics: "+ex.Message);}
        }
    }
}
