using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    // Read native prefabs once per room; choosing a role never creates a weapon.
    internal static class BotWeaponCatalog
    {
        private sealed class Entry{internal bool Usable;internal int Ammo=-1;internal WeaponProfile Profile;}
        private static readonly System.Reflection.FieldInfo Healing=AccessTools.Field(typeof(Gun),"healingMultiplierr"),Damage=AccessTools.Field(typeof(Gun),"damageMultiplier"),Type=AccessTools.Field(typeof(Gun),"gunType");
        private static GameRoom _room;
        private static readonly Dictionary<int,Entry> Cache=new Dictionary<int,Entry>();
        private static Entry Get(GameRoom room,int id,string name="")
        {
            if(_room!=room){Cache.Clear();_room=room;}
            if(Cache.TryGetValue(id,out var entry))return entry;
            entry=new Entry{Profile=AiDummyCatalog.GetWeaponProfile(-1,null)};
            if(room==null || id<0)return entry;
            var pickup=room.GetItem(id);var gun=pickup?pickup.GetComponentInChildren<Gun>(true):null;
            var hit=gun && gun.projectile?gun.projectile.GetComponentInChildren<ProjectileHit>(true):null;
            if(!gun || !hit || gun.projectile.GetComponentInChildren<ProjectileHitGrapple>(true) || hit.damage<=0 || (float)Healing.GetValue(gun)>0 || (Gun.gunTypes)Type.GetValue(gun)==Gun.gunTypes.Spell){Cache[id]=entry;return entry;}
            entry.Usable=true;
            entry.Ammo=gun.AmmoItemIndex;
            if(entry.Ammo<0)
            {
                var ammo=AccessTools.Field(typeof(Gun),"m_ammoType")?.GetValue(gun) as GameObject;
                var ammoPickup=ammo?ammo.GetComponent<Pickup>():null;
                if(ammoPickup)entry.Ammo=(int)AccessTools.Field(typeof(Pickup),"m_itemIndex").GetValue(ammoPickup);
            }
            var c=(Gun.gunTypes)Type.GetValue(gun);
            string role=c==Gun.gunTypes.Shotgun?"shotgun":c==Gun.gunTypes.Sniper || c==Gun.gunTypes.CrossBow || c==Gun.gunTypes.Rifle?"sniper":
                c==Gun.gunTypes.SMG?"smg":c==Gun.gunTypes.Pistol?"pistol":c==Gun.gunTypes.LMG || c==Gun.gunTypes.MiniGun?"mg":"ak";
            var p=AiDummyCatalog.GetWeaponProfile(10000,role);
            var plan=gun.hasFullAuto?FirePlan.FullAuto:gun.hasBurst && !gun.hasSingleFire?FirePlan.Burst:FirePlan.Semi;
            var combat=p.CombatClass==WeaponCombatClass.Sniper && plan==FirePlan.FullAuto?WeaponCombatClass.AutoSniper:p.CombatClass;
            entry.Profile=new WeaponProfile(combat,plan,p.MinRange,p.PreferredRange,p.MaxRange,
                Mathf.Max(.1f,hit.damage*(float)Damage.GetValue(gun)),Mathf.Max(.045f,gun.rateOfFire),Mathf.Max(1,gun.burst),Mathf.Max(1,gun.bulletsInMag),Mathf.Max(.2f,gun.reloadTime),p.CloseHitChance,p.FarHitChance);
            Cache[id]=entry;return entry;
        }
        internal static bool Usable(GameRoom room,int id)=>Get(room,id).Usable;
        internal static int Ammo(GameRoom room,int id)=>Get(room,id).Ammo;
        internal static WeaponProfile Profile(GameRoom room,int id,string name)=>Get(room,id,name).Profile;
    }
}
