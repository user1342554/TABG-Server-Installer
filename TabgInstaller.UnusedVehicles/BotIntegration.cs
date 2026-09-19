using System;
using System.Reflection;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
namespace TabgInstaller.UnusedVehicles
{
    // Keep FakePlayers optional. Resolve delegates once, never reflect inside AI ticks.
    internal static class BotIntegration
    {
        private static Func<TABGPlayerServer,TABGPlayerServer,bool> _friends,_canAttack;
        private static Action<TABGPlayerServer,Vector3> _report;
        private static Action<TABGPlayerServer,Vector3,float> _noise;
        private static Action<ServerClient,TABGPlayerServer,Vector3> _broadcast;
        private static FieldInfo _vehicle;
        private static System.Func<ServerClient,TABGPlayerServer,TABGCarServer,TABGCarServerSeat,bool,bool> _seat;
        private static System.Func<TABGPlayerServer,TABGCarServer,bool> _transport;
        internal static bool Register(object driver)
        {
            var tactics=AccessTools.TypeByName("TabgInstaller.FakePlayers.BotTactics");
            var plugin=AccessTools.TypeByName("TabgInstaller.FakePlayers.FakePlayersPlugin");
            if(tactics==null || plugin==null)return false;
            _friends=(Func<TABGPlayerServer,TABGPlayerServer,bool>)Delegate.CreateDelegate(typeof(Func<TABGPlayerServer,TABGPlayerServer,bool>),AccessTools.Method(tactics,"Friends"));
            _canAttack=(Func<TABGPlayerServer,TABGPlayerServer,bool>)Delegate.CreateDelegate(typeof(Func<TABGPlayerServer,TABGPlayerServer,bool>),AccessTools.Method(tactics,"CanAttack"));
            _report=(Action<TABGPlayerServer,Vector3>)Delegate.CreateDelegate(typeof(Action<TABGPlayerServer,Vector3>),AccessTools.Method(tactics,"Report"));
            _broadcast=(Action<ServerClient,TABGPlayerServer,Vector3>)Delegate.CreateDelegate(typeof(Action<ServerClient,TABGPlayerServer,Vector3>),AccessTools.Method(plugin,"BroadcastPlayerUpdate"));
            _noise=(Action<TABGPlayerServer,Vector3,float>)Delegate.CreateDelegate(typeof(Action<TABGPlayerServer,Vector3,float>),AccessTools.Method(tactics,"Noise"));
            _seat=(System.Func<ServerClient,TABGPlayerServer,TABGCarServer,TABGCarServerSeat,bool,bool>)Delegate.CreateDelegate(typeof(System.Func<ServerClient,TABGPlayerServer,TABGCarServer,TABGCarServerSeat,bool,bool>),AccessTools.Method(plugin,"SetBotSeat"));
            _transport=(System.Func<TABGPlayerServer,TABGCarServer,bool>)Delegate.CreateDelegate(typeof(System.Func<TABGPlayerServer,TABGCarServer,bool>),AccessTools.Method("TabgInstaller.FakePlayers.BotSquadAccess:TransportUseful"));
            _vehicle=AccessTools.Field(tactics,"VehicleControl");
            _vehicle.SetValue(null,Delegate.CreateDelegate(_vehicle.FieldType,driver,AccessTools.Method(driver.GetType(),"Tick")));
            return true;
        }
        internal static bool Seat(ServerClient server,TABGPlayerServer bot,TABGCarServer car,TABGCarServerSeat seat,bool enter)=>_seat!=null && _seat(server,bot,car,seat,enter);
        internal static bool TransportUseful(TABGPlayerServer bot,TABGCarServer car)=>_transport!=null && _transport(bot,car);
        internal static void Unregister()=>_vehicle?.SetValue(null,null);
        internal static bool Friends(TABGPlayerServer a,TABGPlayerServer b)=>_friends!=null?_friends(a,b):a!=null && b!=null && (a==b || (a.GroupIndex!=255 && a.GroupIndex==b.GroupIndex));
        internal static bool CanAttack(TABGPlayerServer attacker,TABGPlayerServer target)=>_canAttack!=null?_canAttack(attacker,target):!Friends(attacker,target);
        internal static void Noise(TABGPlayerServer bot,Vector3 point,float range)=>_noise?.Invoke(bot,point,range);
        internal static void Report(TABGPlayerServer bot,Vector3 position)=>_report?.Invoke(bot,position);
        internal static void BroadcastPlayerUpdate(ServerClient server,TABGPlayerServer bot,Vector3 position)=>_broadcast?.Invoke(server,bot,position);
    }
}
