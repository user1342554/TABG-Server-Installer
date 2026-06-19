using System;
using System.Collections.Generic;
using System.IO;
using Landfall.Network;
using Landfall.Network.GameModes;
using UnityEngine;

namespace TabgInstaller.MatchCore
{
    internal static class MatchCoreRuntime
    {
        private static readonly HashSet<byte> Votes = new HashSet<byte>();
        private static readonly Dictionary<GameRoom, float> WaitingSince = new Dictionary<GameRoom, float>();
        private static readonly Dictionary<GameRoom, float> StartedSince = new Dictionary<GameRoom, float>();
        private static readonly Dictionary<GameRoom, float> RingDamageSince = new Dictionary<GameRoom, float>();
        private static RingProfile _selectedRing;

        public static void Reset()
        {
            Votes.Clear();
            WaitingSince.Clear();
            StartedSince.Clear();
            RingDamageSince.Clear();
            _selectedRing = null;
        }

        public static void ClearVotes()
        {
            Votes.Clear();
        }

        public static bool HandleVoteStart(ServerClient world, TABGPlayerServer sender)
        {
            var settings = MatchCorePlugin.Settings;
            if (world == null || sender == null || settings == null) return false;

            var room = world.GameRoomReference;
            if (room == null || room.Players == null || room.CurrentGameState != GameState.WaitingForPlayers) return true;

            var activeHumanIndexes = new HashSet<byte>();
            foreach (var player in room.Players)
            {
                if (player != null && !player.Bot)
                    activeHumanIndexes.Add(player.PlayerIndex);
            }

            if (!activeHumanIndexes.Contains(sender.PlayerIndex))
                return true;

            Votes.RemoveWhere(vote => !activeHumanIndexes.Contains(vote));

            int humanPlayers = activeHumanIndexes.Count;
            if (humanPlayers < settings.VoteMinimumPlayers)
            {
                Reply(sender, "Need " + settings.VoteMinimumPlayers + " player(s) before vote-start.");
                return true;
            }

            Votes.Add(sender.PlayerIndex);
            int required = Math.Max(1, Mathf.CeilToInt(humanPlayers * (settings.VotePercent / 100f)));
            Reply(sender, "Vote-start: " + Votes.Count + "/" + required);

            if (Votes.Count >= required)
            {
                room.ForceStartCountDown(settings.VoteStartCountdown);
                Votes.Clear();
                MatchCorePlugin.LoggerSafe("Vote-start threshold reached.");
            }

            return true;
        }

        public static void TickTimers(TABGBaseGameMode mode, GameState state)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null) return;

            var room = GetRoom(mode);
            if (room == null) return;

            if (state == GameState.WaitingForPlayers && settings.PreMatchTimeout > 0f)
            {
                if (!WaitingSince.ContainsKey(room))
                    WaitingSince[room] = Time.unscaledTime;

                if (Time.unscaledTime - WaitingSince[room] >= settings.PreMatchTimeout)
                {
                    MatchCorePlugin.LoggerSafe("Pre-match timeout reached; ending room.");
                    EndMatch(room, null);
                }
            }
            else if (state != GameState.WaitingForPlayers)
            {
                WaitingSince.Remove(room);
            }

            if (state == GameState.Started && settings.MatchTimeout > 0f)
            {
                if (!StartedSince.ContainsKey(room))
                    StartedSince[room] = Time.unscaledTime;

                if (Time.unscaledTime - StartedSince[room] >= settings.MatchTimeout)
                {
                    var winner = GetHighestKillTeam(room);
                    MatchCorePlugin.LoggerSafe("Match timeout reached; ending room.");
                    EndMatch(room, winner);
                }
            }
            else if (state != GameState.Started)
            {
                StartedSince.Remove(room);
            }

            if (state == GameState.Started)
                TickServerRingDamage(room, settings);
            else
                RingDamageSince.Remove(room);
        }

        public static bool HandleWinCondition(BattleRoyaleGameMode mode, GameState state)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || state != GameState.Started) return true;

            var room = GetRoom(mode);
            if (room == null) return true;

            if (settings.WinCondition == WinConditionMode.Debug || settings.WinCondition == WinConditionMode.Endless)
                return false;

            if (settings.WinCondition == WinConditionMode.KillsToWin)
            {
                var highest = room.CurrentGameKills.GetHighestKillingTeam();
                if (highest.Value >= settings.KillsToWin)
                {
                    EndMatch(room, room.CurrentGameStats.GetTeam(highest.Key));
                    return false;
                }

                if (room.CurrentGameStats.GetAliveTeams() <= 1)
                {
                    EndMatch(room, room.CurrentGameStats.GetWinningTeam());
                    return false;
                }

                return false;
            }

            return true;
        }

        public static bool TryGetSpawnPoint(BattleRoyaleGameMode mode, out SpawnPointWrapper spawn)
        {
            spawn = null;
            var settings = MatchCorePlugin.Settings;
            if (settings == null) return false;

            if (settings.MatchSpawnPoints.Count > 0)
            {
                var point = settings.MatchSpawnPoints[UnityEngine.Random.Range(0, settings.MatchSpawnPoints.Count)];
                spawn = new SpawnPointWrapper(point, 0f);
                return true;
            }

            if (settings.LobbySpawnPoint < 0) return false;

            if (settings.LobbySpawnPoint == 6)
            {
                spawn = new SpawnPointWrapper(settings.CustomSpawnPoint, 0f);
                return true;
            }

            var room = GetRoom(mode);
            if (room == null) return false;

            var spawns = room.GetSpawnPoints(0);
            if (spawns == null || spawns.Count == 0) return false;

            int index = Mathf.Clamp(settings.LobbySpawnPoint, 0, spawns.Count - 1);
            spawn = spawns[index];
            return true;
        }

        public static void GiveRespawnLoadout(ServerClient world, List<TABGPlayerServer> players)
        {
            if (world == null || players == null) return;
            var loadout = PickLoadout();
            if (loadout == null) return;

            foreach (var player in players)
            {
                if (player == null) continue;
                var ids = new List<int>();
                var amounts = new List<byte>();
                foreach (var item in loadout.Items)
                {
                    ids.Add(item.Id);
                    amounts.Add((byte)Mathf.Clamp(item.Amount, 1, 255));
                }

                if (ids.Count > 0)
                    GivePickUpCommand.Run(null, world, player.PlayerIndex, ids.ToArray(), amounts.ToArray());
            }
        }

        public static void ApplyKillRewards(TABGPlayerServer victim, ServerClient world)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || victim == null || world == null) return;

            var attacker = world.GameRoomReference?.FindPlayer(victim.LastAttacker);
            if (attacker == null || attacker.GroupIndex == victim.GroupIndex) return;

            if (settings.HealOnKill && settings.HealOnKillAmount > 0f)
                HealPlayer(world, attacker, settings.HealOnKillAmount);

            if (settings.ItemsGiven.Count > 0)
                GiveItems(world, attacker, settings.ItemsGiven);
        }

        public static void DropControl(List<TABGPlayerServer> players)
        {
            if (players == null) return;
            foreach (var player in players)
                player?.ClearLoot();
        }

        public static void ConfigureSpellDrop(object spellDrop)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || spellDrop == null) return;

            var type = spellDrop.GetType();
            type.GetField("min")?.SetValue(spellDrop, settings.SpellDropsEnabled ? settings.MinSpellDropDelay : 999999f);
            type.GetField("max")?.SetValue(spellDrop, settings.SpellDropsEnabled ? settings.MaxSpellDropDelay : 999999f);

            float delay = settings.SpellDropsEnabled
                ? UnityEngine.Random.Range(settings.MinSpellDropDelay, settings.MaxSpellDropDelay) + settings.SpellDropOffset
                : 999999f;
            var timer = ReflectionHelpers.Field(type, "timeUntilNextDrop");
            timer?.SetValue(spellDrop, Mathf.Max(0f, delay));
        }

        public static void ApplyRingSettings(TheRing ring)
        {
            var profile = SelectedRing();
            if (ring == null || profile == null) return;

            if (profile.Sizes != null && profile.Sizes.Length > 0)
                ring.ringSizes = profile.Sizes;
            if (profile.Speeds != null && profile.Speeds.Length > 0)
                ring.ringSpeeds = profile.Speeds;
        }

        public static bool TryOverrideRingPosition(TheRing ring, float newCircleSize)
        {
            var profile = SelectedRing();
            if (ring == null || profile == null) return false;

            float size = newCircleSize;
            if (profile.Sizes != null && ring.currentRingID >= 0 && ring.currentRingID < profile.Sizes.Length)
                size = profile.Sizes[ring.currentRingID];

            ring.currentWhiteRingPosition = profile.Center;
            ring.currentWhiteSize = size;
            if (ring.white != null)
            {
                ring.white.transform.position = profile.Center;
                ring.white.transform.localScale = Vector3.one * size;
            }
            return true;
        }

        public static void ForceDrop(ServerClient world)
        {
            var settings = MatchCorePlugin.Settings;
            if (settings == null || !settings.ForceDropAtStart || world == null) return;

            try
            {
                AutoDropAllPlayersCommand.Run(null, world);
            }
            catch (Exception ex)
            {
                MatchCorePlugin.Warning("Auto-drop failed: " + ex.Message);
            }
        }

        private static void GiveItems(ServerClient world, TABGPlayerServer player, List<LootItem> items)
        {
            var ids = new List<int>();
            var amounts = new List<byte>();
            foreach (var item in items)
            {
                ids.Add(item.Id);
                amounts.Add((byte)Mathf.Clamp(item.Amount, 1, 255));
            }
            GivePickUpCommand.Run(null, world, player.PlayerIndex, ids.ToArray(), amounts.ToArray());
        }

        private static void HealPlayer(ServerClient world, TABGPlayerServer player, float amount)
        {
            float newHealth = Mathf.Min(100f, player.Health + amount);
            if (newHealth <= player.Health) return;

            player.UpdateHealth(newHealth);
            byte[] buffer = new byte[5];
            using (var output = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(output))
            {
                writer.Write(player.PlayerIndex);
                writer.Write(newHealth);
            }

            var watchers = ServerChunks.Instance.GetWatchers(player.ChunkData);
            var recipients = new byte[watchers.Count];
            for (int i = 0; i < recipients.Length; i++)
                recipients[i] = watchers[i].PlayerIndex;
            world.SendMessageToClients(EventCode.PlayerHealed, buffer, recipients, true, true);
        }

        private static LoadoutDefinition PickLoadout()
        {
            var loadouts = MatchCorePlugin.Settings?.Loadouts;
            if (loadouts == null || loadouts.Count == 0) return null;

            int total = 0;
            foreach (var loadout in loadouts)
                total += Math.Max(1, loadout.Weight);

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var loadout in loadouts)
            {
                roll -= Math.Max(1, loadout.Weight);
                if (roll < 0) return loadout;
            }
            return loadouts[0];
        }

        private static RingProfile SelectedRing()
        {
            if (_selectedRing != null) return _selectedRing;
            var rings = MatchCorePlugin.Settings?.Rings;
            if (rings == null || rings.Count == 0) return null;

            float total = 0f;
            foreach (var ring in rings)
                total += Math.Max(1f, ring.Rarity);

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (var ring in rings)
            {
                roll -= Math.Max(1f, ring.Rarity);
                if (roll <= 0f)
                {
                    _selectedRing = ring;
                    return _selectedRing;
                }
            }

            _selectedRing = rings[0];
            return _selectedRing;
        }

        private static TeamStanding GetHighestKillTeam(GameRoom room)
        {
            var highest = room.CurrentGameKills.GetHighestKillingTeam();
            var team = room.CurrentGameStats.GetTeam(highest.Key);
            return team ?? room.CurrentGameStats.GetWinningTeam();
        }

        private static void TickServerRingDamage(GameRoom room, MatchCoreConfig settings)
        {
            if (room?.Players == null || settings == null || !settings.ServerRingDamage || settings.ServerRingDamagePerSecond <= 0f)
                return;

            TheRing ring = TheRing.Instance;
            if (ring == null || !ring.hasStarted || ring.currentBlueSize <= 0f)
                return;

            float now = Time.unscaledTime;
            float lastTick;
            if (RingDamageSince.TryGetValue(room, out lastTick) && now - lastTick < settings.ServerRingDamageTickSeconds)
                return;

            RingDamageSince[room] = now;

            Vector3 center = ring.currentBluePosition;
            float radius = Mathf.Max(0f, ring.currentBlueSize * 0.5f + 25f);
            float progress = Mathf.Clamp01(1f - radius / 2200f);
            float damage = settings.ServerRingDamagePerSecond * settings.ServerRingDamageTickSeconds * Mathf.Lerp(1f, 3f, progress);
            if (ring.currentRingID >= ring.ringSpeeds.Length - 1)
                damage *= 2f;

            var players = room.Players.ToArray();
            for (int i = 0; i < players.Length; i++)
            {
                TABGPlayerServer player = players[i];
                if (player == null || player.IsDead || !player.HasDropped)
                    continue;

                Vector3 offset = player.PlayerPosition - center;
                offset.y = 0f;
                if (offset.magnitude <= radius)
                    continue;

                ApplyRingDamage(room, player, damage);
            }
        }

        private static void ApplyRingDamage(GameRoom room, TABGPlayerServer player, float damage)
        {
            ServerClient world = ReflectionHelpers.FieldValue<ServerClient>(room, typeof(GameRoom), "m_server");
            if (player.IsDowned || player.Health <= damage)
            {
                player.UpdateHealth(0f);
                room.CurrentGameMode.KillPlayer(player, null);
                room.CheckGameState();
                MatchCorePlugin.LoggerSafe("Ring killed " + player.PlayerName + ".");
                return;
            }

            player.TakeDamage(damage);
            world?.DamagePlayer(player);
        }

        private static void EndMatch(GameRoom room, TeamStanding winner)
        {
            if (room == null || room.CurrentGameState == GameState.Ended) return;
            room.EndMatch(winner);
            room.ChangeGameState(GameState.Ended);
        }

        private static GameRoom GetRoom(TABGBaseGameMode mode)
        {
            if (mode == null) return null;
            return ReflectionHelpers.FieldValue<GameRoom>(mode, typeof(TABGBaseGameMode), "m_GameRoom");
        }

        private static void Reply(TABGPlayerServer player, string message)
        {
            try
            {
                CitrusLib.Citrus.SelfParrot(player, message);
            }
            catch
            {
                MatchCorePlugin.LoggerSafe(message);
            }
        }
    }

}
