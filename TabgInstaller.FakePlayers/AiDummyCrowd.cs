using TabgInstaller.Vehicles;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    internal partial class AiDummyController
    {
        private Vector3 AvoidPeople(Vector3 current,Vector3 desired)
        {
            if(desired.sqrMagnitude<.01f)return desired;
            Landfall.Network.TABGPlayerServer nearest=null;float best=BotSeparation.LookAhead*BotSeparation.LookAhead;
            foreach(var p in _room.Players)
            {
                if(p==_player || p.IsDead || !p.HasDropped || p.IsInsideCar || Mathf.Abs(p.PlayerPosition.y-current.y)>2.5f)continue;
                var offset=Flat(p.PlayerPosition-current);float distance=offset.sqrMagnitude;
                if(distance<best && (distance<BotSeparation.BodyDistance*BotSeparation.BodyDistance || Vector3.Dot(offset,desired)>0)){nearest=p;best=distance;}
            }
            if(nearest==null)return desired;
            var relative=nearest.PlayerPosition-current;
            if(best<.0001f)return _player.PlayerIndex<nearest.PlayerIndex?Vector3.right:Vector3.left;
            if(BotSeparation.Steer(desired.x,desired.z,relative.x,relative.z,out float x,out float z))return new Vector3(x,0,z);
            return desired;
        }
        private bool PeopleAllowStep(Vector3 current,Vector3 next)
        {
            var step=next-current;
            foreach(var p in _room.Players)
            {
                if(p==_player || p.IsDead || !p.HasDropped || p.IsInsideCar || Mathf.Abs(p.PlayerPosition.y-current.y)>2.5f)continue;
                var relative=p.PlayerPosition-current;
                if(!BotSeparation.SafeStep(step.x,step.z,relative.x,relative.z))return false;
            }
            return true;
        }
    }
}
