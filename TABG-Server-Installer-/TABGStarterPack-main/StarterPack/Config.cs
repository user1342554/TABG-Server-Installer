using Landfall.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StarterPack
{
    internal class Config
    {
        //Match Settings
        public static WinCondition winCondition;
        public static int killsToWin;

        public static bool forceKillOffStart;

        //Drop Settings
        public static bool dropItemsOnDeath;

        public static string givenItems;

        //Ring Manager
        public static List<RingContainer> ringPositions;
        public static RingContainer chosenRing;

        //Respawning Players
        //Loadouts
        public static List<Loadout> loadouts;

        //Player Settings
        public static bool HealOnKill = false;
        public static float HealOnKillAmount = 20;

        public static bool canGoDown = false;
        public static bool canLockOut = false;

        //Lobby Spawn Controller
        public static int[] spawnPoints;
        public static Vector3 CustomSpawnPoint;

        //Vote To Start
        public static int percentOfVotes;
        public static int minNumberOfPlayers;
        public static int timeToStart;

        //Spell Drop Controller
        public static bool spelldropEnabled = true;
        public static int minSpellDropDelay = 10;
        public static int maxSpellDropDelay = 12;
        public static int spellDropOffset = 0;
        /*public static int[] spellDropRareContents = { 0 };
        public static int[] spellDropEpicContents = { 0 };
        public static int[] spellDropLegenContents = { 0 };*/

        //Match Timeout
        public static float preMatchTimeout = 15f;
        public static float periMatchTimer = 15f;

        public static void Setup(ServerClient __instance)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath);
            string path = Path.Combine(directoryInfo.Parent.FullName, "TheStarterPack.txt");
            bool flag = !File.Exists(path);
            if (flag)
            {
                File.WriteAllLines(path, new string[]
                {
                    "//Roadwork = -191,132,-205",
                    "//Factory/WW2 = -377,198,-546",
                    "//Snow Castle = -378,171,-774",
                    "//City = -670,118,-22",
                    "//Tall Work = -431,120,59",
                    "//Finland West = -562,119,300",
                    "//Finland East = -445,119,333",
                    "//Actual Castle = -696,140,538",
                    "//Point of Impact = -62,118,583",
                    "//Area 64 = 405,132,473",
                    "//Western = 670,118,592",
                    "//Pyramid = 633,124,240",
                    "//Long Wall = 423,134,90",
                    "//Industry = 102,117,-50",
                    "//Chaos = -11,132,-558",
                    "//Big Work = 339,118,-659",
                    "//Big Power= 759,139,-671",
                    "//Small Power = 382,128,-360",
                    "//",
                    "//name:rarity%location:size,size.../",
                    "//string:int%int,int,int:int,int.../",
                    "RingSettings=",
                    "",
                    "",
                    "//Default,KillsToWin,or Debug (wont end unless timer expires)",
                    "WinCondition=Default",
                    "//How many kills to win in KillsToWin mode",
                    "KillsToWin=",
                    "",
                    "//Force kill the players out of the trucks",
                    "ForceKillAtStart=false",
                    "",
                    "",
                    "",
                    "//Drop Settings",
                    "DropItemsOnDeath=false",
                    "ItemsGiven=",
                    "",
                    "//Loadouts are defined using Item IDs and quantities in sets that are divided by forward slashes(int:int,int:int/int:int....)",
                    "//For example to give someone an AK, a red dot, and some normal ammo you'd use 152:1,38:1,6:200/etc",
                    "//When a player respawns they will be given a random loadout from the list of loadouts",
                    "Loadouts=",
                    "",
                    "//Will players be healed when they get a kill",
                    "HealOnKill=false",
                    "//What % of HP will people be healed when they get a kill",
                    "HealOnKillAmount=20",
                    "//Will players on a team together go down or just respawn instantly",
                    "CanGoDown=false",
                    "CanLockOut=false",
                    "",
                    "",
                    "//0(Tall Work),1(Circle Town),2(Western),3(Containers),4(Chaos),5(Factory)",
                    "//For example: 2 would have everyone spawn in Western. 0,2 would have everyone either spawn near Tall Work or Western",
                    "ValidSpawnPoints=2",
                    "//If you put 6 in there you will use the custom spawn point",
                    "//int,int,int",
                    "CustomSpawnPoint=",
                    "",
                    "",
                    "//Required percent of votes to start",
                    "PercentOfVotes=50",
                    "//Required number of players to start",
                    "MinNumberOfPlayers=2",
                    "//Time after voting for the match to begin",
                    "TimeToStart=30",
                    "",
                    "",
                    "SpelldropEnabled=true",
                    "//Min time in seconds between drops (int)",
                    "MinSpellDropDelay=180",
                    "//Max time in seconds between drops (int)",
                    "MaxSpellDropDelay=420",
                    "//Delay in seconds before first drop spawns (int)",
                    "SpellDropOffset=30",
                    /*"//Pool for rare drops to pull from(int[])",
                    "SpellDropRareLootPool=",
                    "//Pool for epic drops to pull from(int[])",
                    "SpellDropEpicLootPool=",
                    "//Pool for legendary drops to pull from(int[])",
                    "SpellDropLegenLootPool="*/
                    "",
                    "",
                    "//I reccomend keeping these values below 40 min combined. Odd server behaviour has been noticed after that point.",
                    "//Time in minutes that the PRE LOBBY can last for before forcing a restart",
                    "PreMatchTimeout=15",
                    "",
                    "//Time in minutes that the MATCH can last for before forcing a restart",
                    "PeriMatchTimeout=15"
                });
            }
            else
            {
                string[] array = File.ReadAllLines(path);
                Config.winCondition = WinCondition.Default;
                Config.killsToWin = 30;

                Config.forceKillOffStart = false;

                Config.dropItemsOnDeath = false;
                Config.givenItems = null;

                Config.ringPositions = new List<RingContainer>() { new RingContainer("name",5,new int[] {4000,1300,300},new float[] { 6f,6f,0f} , new Vector3(0,0,0)) };

                Config.loadouts = new List<Loadout>();
                Config.HealOnKill = false;
                Config.HealOnKillAmount = 20f;

                Config.canGoDown = false;
                Config.canLockOut = false;

                Config.spawnPoints = new int[] { 2 };
                Config.CustomSpawnPoint = new Vector3(0, 0, 0);

                Config.percentOfVotes = 50;
                Config.minNumberOfPlayers = 2;
                Config.timeToStart = 30;

                Config.spelldropEnabled = true;
                Config.minSpellDropDelay = 180;
                Config.maxSpellDropDelay = 420;
                Config.spellDropOffset = 0;
                /*Config.spellDropRareContents = new int[] { 0 };
                Config.spellDropEpicContents = new int[] { 0 };
                Config.spellDropLegenContents = new int[] { 0 };*/

                Config.preMatchTimeout = 15f;
                Config.periMatchTimer = 15f;

                foreach (string text in array)
                {
                    bool flag2 = text.Contains('=');
                    if (flag2)
                    {
                        string[] array3 = text.Split(new char[]
                        {
                            '='
                        });
                        string a = array3[0];
                        if(a == "WinCondition")
                        {
                            Config.winCondition = (WinCondition)Enum.Parse(typeof(WinCondition), array3[1]);
                        }
                        if(a == "KillsToWin")
                        {
                            Config.killsToWin = int.Parse(array3[1]);
                        }
                        if(a == "ForceKillAtStart")
                        {
                            Config.forceKillOffStart = bool.Parse(array3[1]);
                        }

                        if (a == "RingSettings")
                        {
                            Config.ringPositions = Config.GetRings(array3[1]);
                        }

                        if(a == "DropItemsOnDeath")
                        {
                            Config.dropItemsOnDeath = bool.Parse(array3[1]);
                        }
                        if(a == "ItemsGiven")
                        {
                            Config.givenItems = array3[1];
                        }

                        if (a == "Loadouts")
                        {
                            Config.loadouts = Config.GetLoadouts(array3[1]);
                        }
                        if (a == "HealOnKill")
                        {
                            Config.HealOnKill = bool.Parse(array3[1]);
                        }
                        if (a == "HealOnKillAmount")
                        {
                            Config.HealOnKillAmount = float.Parse(array3[1]);
                        }
                        if (a == "CanGoDown")
                        {
                            Config.canGoDown = bool.Parse(array3[1]);
                        }
                        if (a == "CanLockOut")
                        {
                            Config.canLockOut = bool.Parse(array3[1]);
                        }

                        if (a == "ValidSpawnPoints")
                        {
                            Config.spawnPoints = Config.GetIntArray(array3[1]);
                        }
                        if (a == "CustomSpawnPoint")
                        {
                            Config.CustomSpawnPoint = Config.GetVector3(array3[1]);
                        }

                        if (a == "PercentOfVotes")
                        {
                            Config.percentOfVotes = int.Parse(array3[1]);
                        }
                        if (a == "MinNumberOfPlayers")
                        {
                            Config.minNumberOfPlayers = int.Parse(array3[1]);
                        }
                        if (a == "TimeToStart")
                        {
                            Config.timeToStart = int.Parse(array3[1]);
                        }

                        if(a == "SpelldropEnabled")
                        {
                            Config.spelldropEnabled = bool.Parse(array3[1]);
                        }
                        if (a == "MinSpellDropDelay")
                        {
                            Config.minSpellDropDelay = int.Parse(array3[1]);
                        }
                        if (a == "MaxSpellDropDelay")
                        {
                            Config.maxSpellDropDelay = int.Parse(array3[1]);
                        }
                        if (a == "SpellDropOffset")
                        {
                            Config.spellDropOffset = int.Parse(array3[1]);
                        }
                        /*if (a == "SpellDropRareLootPool")
                        {
                            Config.spellDropRareContents = GetIntArray(array3[1]);
                        }
                        if (a == "SpellDropEpicLootPool")
                        {
                            Config.spellDropEpicContents = GetIntArray(array3[1]);
                        }
                        if (a == "SpellDropLegenLootPool")
                        {
                            Config.spellDropLegenContents = GetIntArray(array3[1]);
                        }*/

                        if (a == "PreMatchTimeout")
                        {
                            Config.preMatchTimeout = float.Parse(array3[1]);
                        }
                        if (a == "PeriMatchTimeout")
                        {
                            Config.periMatchTimer = float.Parse(array3[1]);
                        }
                    }
                }
                Config.chosenRing = Config.ChooseRing();
            }
        }

        private static List<Vector3> GetVector3List(string input)
        {
            List<Vector3> result = new List<Vector3>();
            string[] array = input.Split(new char[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < array.Length; i += 3)
            {
                if (i + 2 < array.Length)
                {
                    int x = int.Parse(array[i]);
                    int y = int.Parse(array[i + 1]);
                    int z = int.Parse(array[i + 2]);

                    result.Add(new Vector3(x, y, z));
                }
            }
            return result;
        }

        private static int[] GetIntArray(string IntArrayString)
        {
            string[] array = IntArrayString.Split(new char[] { ',' },StringSplitOptions.RemoveEmptyEntries);
            int[] array2 = new int[array.Length];
            for (int i = 0; i < array2.Length; i++)
            {
                array2[i] = int.Parse(array[i]);
            }
            return array2;
        }

        private static float[] GetFloatArray(string FloatArrayString)
        {
            string[] array = FloatArrayString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            float[] array2 = new float[array.Length];
            for (int i = 0; i < array2.Length; i++)
            {
                array2[i] = float.Parse(array[i]);
            }
            return array2;
        }

        private static Vector3 GetVector3(string input)
        {
            string[] array = input.Split(new char[] { ',' },StringSplitOptions.RemoveEmptyEntries);
            if (array.Length % 3 == 0 && array.Length >= 3)
            {
                return new Vector3(int.Parse(array[0]), int.Parse(array[1]), int.Parse(array[2]));
            }
            return new Vector3(0,0,0);
        }

        public static List<RingContainer> GetRings(string input)
        {
            if (input == null) { return null; }
            //name:rarity%location:size,size/
            //newRing.Name("name").Location(x,y,z).Sizes(size,size,size etc.).Speeds(6,6,0)
            List<RingContainer> toReturn = new List<RingContainer>();

            string[] sets = input.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string set in sets)
            {
                string[] nameraritySplit = set.Split('%');

                string[] arraynr = nameraritySplit[0].Split(':');
                string name = arraynr[0];
                int rare = int.Parse(arraynr[1]);

                string[] ints = nameraritySplit[1].Split(':');
                Vector3 loc = Config.GetVector3(ints[0]);

                int[] sizes = Config.GetIntArray(ints[1]);
                sizes = new int[] {4000}.Concat(sizes).ToArray();

                RingContainer l = new RingContainer(name, rare, sizes, new float[] {0} , loc);

                toReturn.Add(l);
            }

            return toReturn;
        }

        private static List<Loadout> GetLoadouts(string input)
        {
            if (input == null) { return null; }
            List<Loadout> toReturn = new List<Loadout>();

            string[] sets = input.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string set in sets)
            {
                //"name":#rarity%id:quan,id:quan etc.
                string[] nameraritySplit = set.Split('%');
                int rare;
                string name;

                string[] arraynr = nameraritySplit[0].Split(':');
                name = arraynr[0];
                rare = int.Parse(arraynr[1]);


                List<int> idList = new List<int>();
                List<int> quanList = new List<int>();

                string[] ints = nameraritySplit[1].Split(',');
                foreach (string s in ints)
                {
                    string[] num = s.Split(':');
                    // Split each string by ':' and convert to int
                    idList.Add(int.Parse(num[0]));
                    quanList.Add(int.Parse(num[1]));
                }
                Loadout l = new Loadout(name, rare, idList, quanList);

                toReturn.Add(l);
            }

            return toReturn;
        }

        public static Loadout ChooseLoadout()
        {
            int totalWeight = loadouts.Sum(l => l.rarity);
            int randomNumber = new System.Random().Next(1, totalWeight + 1);

            foreach (var loadout in loadouts)
            {
                if (randomNumber <= loadout.rarity)
                {
                    return loadout;
                }
                else
                {
                    randomNumber -= loadout.rarity;
                }
            }

            // This should never happen if the total weight is calculated correctly
            throw new InvalidOperationException("No loadout selected");
        }

        public static RingContainer ChooseRing()
        {
            int totalWeight = ringPositions.Sum(l => l.rarity);
            int randomNumber = new System.Random().Next(1, totalWeight + 1);

            foreach (var ring in ringPositions)
            {
                if (randomNumber <= ring.rarity)
                {
                    return ring;
                }
                else
                {
                    randomNumber -= ring.rarity;
                }
            }

            // This should never happen if the total weight is calculated correctly
            throw new InvalidOperationException("No ring selected");
        }
    }
    public enum WinCondition
    {
        Debug,
        KillsToWin,
        Default
    }

    public class Loadout
    {
        public string name;
        public int rarity;
        public List<int> itemIds;
        public List<int> itemQuantities;
        public Loadout(string name,int rarity, List<int> itemIds, List<int> itemQuantities)
        {
            this.name = name;
            this.rarity = rarity;
            this.itemIds = itemIds;
            this.itemQuantities = itemQuantities;
        }
    }

    public class RingContainer
    {
        public string name;
        public int rarity;

        public int[] sizes;
        public float[] speeds;
        public Vector3 location;

        public RingContainer(string name, int rarity, int[] sizes, float[] speeds, Vector3 location)
        {
            this.name = name;
            this.rarity = rarity;

            this.sizes = sizes;
            this.speeds = speeds;
            this.location = location;
            this.location.y += 1; //To remove the Vector3.zero edge case
        }
    }
}
