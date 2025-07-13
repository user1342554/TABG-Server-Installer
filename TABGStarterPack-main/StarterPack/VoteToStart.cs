using Landfall.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StarterPack
{
    internal class VoteToStart
    {
        public static void Log(string text)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.Write("VoteStart");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("] - ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(text);
        }

        public static void ChatCommandPostfix(byte[] msgData, ServerClient world, byte sender)
        {
            VoteToStart.VoteStart(msgData, world, sender);
        }

        public static void VoteStart(byte[] data, ServerClient world, byte sender)
        {
            byte b = data[0];
            TABGPlayerServer tabgplayerServer = world.GameRoomReference.FindPlayer(b);
            string @string;
            using (MemoryStream memoryStream = new MemoryStream(data))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream))
                {
                    binaryReader.ReadByte();
                    byte count = binaryReader.ReadByte();
                    @string = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)count));
                }
            }

            if (@string.ToLower() == "/votestart")
            {
                if (!hasVoted.Contains(tabgplayerServer.PlayFabID))
                {
                    VoteToStart.votes++;
                    VoteToStart.Log(string.Concat(new object[]
                    {
                        "Vote Start by ",
                        tabgplayerServer.PlayerName,
                        " ",
                        "Total votes: ",
                        VoteToStart.votes
                    }));
                    hasVoted.Add(tabgplayerServer.PlayFabID);
                    VoteToStart.StartGameWithVotes(world.GameRoomReference);
                }
                else
                {
                    VoteToStart.Log("Player " + tabgplayerServer.PlayerName + " has already voted");
                }
            }
        }

        public static int votes = 0;

        public static List<string> hasVoted = new List<string>();

        public static void StartGameWithVotes(GameRoom __instance)
        {
            if (VoteToStart.votes >= __instance.Players.Count * Config.percentOfVotes/100 && __instance.Players.Count >= Config.minNumberOfPlayers)
            {
                if (Time.timeSinceLevelLoad >= __instance.CurrentGameSettings.ForceStartTime)
                {
                    VoteToStart.Log("Vote Start Completed");
                    __instance.StartCountDown(30f);
                }
            }
        }

        public static void Reset()
        {
            VoteToStart.votes = 0;
            hasVoted.Clear();
        }
    }
}
