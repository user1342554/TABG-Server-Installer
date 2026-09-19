using System;
using System.Linq;
using CitrusLib;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    // Explicit one-shot diagnostic. Never runs while a human is connected.
    public sealed class BotStartupCheck : MonoBehaviour
    {
        private float _next=35;
        private void Update()
        {
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+2;
            var server=Citrus.World;var room=server?.GameRoomReference;
            if(room?.Players==null)return;
            if(room.Players.Any(p=>!p.Bot)){Debug.Log("[BotCheck] Skipped: human connected.");Destroy(this);return;}
            var bot=room.Players.FirstOrDefault(p=>p.Bot && !p.IsDead && p.Health>0);
            if(bot==null){if(Time.unscaledTime>90)Destroy(this);return;}
            try{FpvServer.CheckBotDamage(server,bot);}
            catch(Exception ex){Debug.LogError("[BotCheck] FAILED: "+ex);}
            Destroy(this);
        }
    }
}
