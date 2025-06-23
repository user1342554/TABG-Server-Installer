using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TabgInstaller.StarterPack
{
    internal class SpellDropController
    {
        //SpellDropServer
        private static void Start(Spelldrop_Server __instance)
        {
            if (Config.spelldropEnabled)
            {
                __instance.min = Config.minSpellDropDelay;
                __instance.max = Config.maxSpellDropDelay;
            }
            else
            {
                __instance.min = Config.minSpellDropDelay * 10000000;
                __instance.max = Config.maxSpellDropDelay * 10000000;
            }
            var trav = Traverse.Create(__instance).Method("SetTimeUntilNextDrop");
            trav.GetValue();
        }

        //GameRoom
        /*public bool DroppedOpened(int index, out Vector3 pos,GameRoom __instance, ref Pickup[] __result)
        {
            LootDatabase m_LootDatabase = __instance.LootDatabase;
            LandLog.Log("One");
            if (index >= __instance.SpawnedDrops.Count)
            {
                pos = Vector3.zero;
                __result = new Pickup[0];
                return false;
            }
            LandLog.Log("Two");
            TABGSpellDropServer tabgspellDropServer = __instance.SpawnedDrops[index];
            tabgspellDropServer.Open();
            LandLog.Log("Three");
            int num;
            switch (tabgspellDropServer.Rarity)
            {
                case Curse.Rarity.Rare:
                    num = 3;
                    Pickup[] array = new Pickup[num];
                    for (int i = 0; i < num; i++)
                    {
                        array[i] = m_LootDatabase.GetDataEntry(Config.spellDropRareContents[UnityEngine.Random.Range(0, Config.spellDropRareContents.Length)]).pickup;
                    }
                    __result = array;
                    break;
                case Curse.Rarity.Epic:
                    num = 2;
                    Pickup[] array2 = new Pickup[num];
                    for (int i = 0; i < num; i++)
                    {
                        array2[i] = m_LootDatabase.GetDataEntry(Config.spellDropEpicContents[UnityEngine.Random.Range(0, Config.spellDropEpicContents.Length)]).pickup;
                    }
                    __result = array2;
                    break;
                case Curse.Rarity.Legendary:
                    num = 1;
                    Pickup[] array3 = new Pickup[num];
                    for (int i = 0; i < num; i++)
                    {
                        array3[i] = m_LootDatabase.GetDataEntry(Config.spellDropEpicContents[UnityEngine.Random.Range(0, Config.spellDropEpicContents.Length)]).pickup;
                    }
                    __result = array3;
                    break;
            }
            LandLog.Log("Four");
            pos = __instance.SpawnedDrops[index].Position;
            LandLog.Log("Five");
            return false;
        }*/
    }
}
