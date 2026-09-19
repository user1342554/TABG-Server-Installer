using System;
using System.Diagnostics;
using UnityEngine;
namespace TabgInstaller.FakePlayers
{
    public sealed class BotPerformance : MonoBehaviour
    {
        private static long _ticks,_max;private static int _calls;private float _next;private int _gc;
        internal static void Record(long ticks){_ticks+=ticks;if(ticks>_max)_max=ticks;_calls++;}
        private void Awake(){_next=Time.unscaledTime+30;_gc=GC.CollectionCount(0);}
        private void Update()
        {
            if(Time.unscaledTime<_next)return;_next=Time.unscaledTime+30;
            float total=(float)(_ticks*1000.0/Stopwatch.Frequency),peak=(float)(_max*1000.0/Stopwatch.Frequency);
            FakePlayersPlugin.Log($"[BotPerf] 30s: updates={_calls}, totalCPUms={total:0.0}, maxUpdateMs={peak:0.00}, managedMB={GC.GetTotalMemory(false)/1048576.0:0.0}, gen0={GC.CollectionCount(0)-_gc}");
            _ticks=_max=0;_calls=0;_gc=GC.CollectionCount(0);
        }
    }
}
