using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Landfall.Network;
using TabgInstaller.FlyingControls;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    internal static class BotExplosionDamage
    {
        private static MethodInfo _apply;
        internal static void Apply(ServerClient server,TABGPlayerServer attacker,Vector3 position,bool missile,int blastId=0)
        {
            if(server?.GameRoomReference?.Players==null || attacker==null)return;
            BotIntegration.Noise(attacker,position,160);
            if(_apply==null)_apply=HarmonyLib.AccessTools.Method("TabgInstaller.FakePlayers.FakePlayersPlugin:ApplyBlastDamage");
            if(_apply==null){Debug.LogWarning("[BotDamage] FakePlayers damage bridge unavailable");return;}
            float radius=missile?TabgInstaller.Vehicles.BotBlastRules.HandRadius:TabgInstaller.Vehicles.BotBlastRules.DynamiteRadius;
            float damage=missile?TabgInstaller.Vehicles.MissileRules.Damage:TabgInstaller.Vehicles.BotBlastRules.DynamiteDamage;
            // Bot-driven vehicles have no owning physics client to report car damage.
            // Snapshot before occupants can die and native code clears their seats.
            var botCars=server.GameRoomReference.Players.Where(p=>p.Bot && p.IsDriving && p.CurrentCar!=null).Select(p=>p.CurrentCar).Distinct().ToArray();
            foreach(var bot in server.GameRoomReference.Players.ToArray())
            {
                if(!bot.Bot || bot.IsDead || bot.Health<=0)continue;
                var delta=bot.PlayerPosition-position;float distance=delta.magnitude;
                if(distance>radius)continue;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(position+delta.normalized*.1f,delta.normalized,Mathf.Max(0,distance-.5f),~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider.GetComponentInParent<ServerNetworkVehicle>())continue;
                    bool playerBody=false;
                    foreach(var p in server.GameRoomReference.Players)if(p.PlayerObject && hit.collider.transform.IsChildOf(p.PlayerObject.transform)){playerBody=true;break;}
                    if(!playerBody){blocked=true;break;}
                }
                float amount=TabgInstaller.Vehicles.BotBlastRules.Damage(damage,radius,distance,false,false,false,blocked);
                if(amount>0)
                {
                    float before=bot.Health;bool wasDown=bot.IsDowned;
                    _apply.Invoke(null,new object[]{server,attacker,bot,amount,missile?"Heli missile":"FPV Dynamite"});
                    BlastFeedback.Confirm(server,attacker.PlayerIndex,missile,blastId,false,bot.PlayerIndex,bot.PlayerPosition,!wasDown && bot.IsDowned?Mathf.Min(before,amount):TabgInstaller.Vehicles.BlastReceipt.HealthLost(before,bot.Health));
                }
            }
            foreach(var car in botCars)
            {
                if(!server.GameRoomReference.Cars.Contains(car) || car.DamagableParts.Count==0)continue;
                Vector3 point=car.CarPosition;float nearest=float.PositiveInfinity;
                foreach(var aim in TabgInstaller.Vehicles.VehicleGeometry.Get(car.CarTypeIdentifier).AimPoints(position,(car.CarPosition-position).normalized,car.CarPosition,car.CarRotation))
                    if((aim-position).sqrMagnitude<nearest){nearest=(aim-position).sqrMagnitude;point=aim;}
                float distance=Vector3.Distance(point,position);if(distance>radius)continue;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(position,(point-position).normalized,Mathf.Max(0,distance-.3f),~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider.GetComponentInParent<ServerNetworkVehicle>())continue;
                    if(server.GameRoomReference.Players.Any(p=>p.PlayerObject && hit.collider.transform.IsChildOf(p.PlayerObject.transform)))continue;
                    blocked=true;break;
                }
                float amount=TabgInstaller.Vehicles.BotBlastRules.Damage(damage,radius,distance,false,false,false,blocked);
                if(amount<=0)continue;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(attacker.PlayerIndex);writer.Write(car.DamagableParts.First().Index);writer.Write(car.CarIndex);writer.Write(amount);
                    CarDamageCommand.Run(TabgInstaller.Vehicles.BlastReceipt.Tag(stream.ToArray(),missile,blastId),server);
                    BlastFeedback.MarkBotVehicle(missile,blastId,car.CarIndex);
                }
                Debug.Log($"[BotDamage] Blast against bot vehicle {car.CarName} #{car.CarIndex}, damage={amount:0.0}");
            }
        }
    }
}
