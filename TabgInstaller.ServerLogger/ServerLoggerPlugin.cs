using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace TabgInstaller.ServerLogger
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "BepInEx plugins are Unity behaviours; patches are released from OnDestroy.")]
    public sealed class ServerLoggerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tabginstaller.serverlogger";
        public const string PluginName = "TABG Server Logger";
        public const string PluginVersion = "1.0.0";

        internal static ServerLoggerPlugin Instance;
        internal static ManualLogSource LogSource;
        private static readonly char[] SpaceSeparator = { ' ' };
        private static readonly char[] CsvQuotedCharacters = { ',', '"', '\r', '\n' };

        private readonly Dictionary<TABGPlayerServer, PlayerIdentity> _loggedPlayers = new Dictionary<TABGPlayerServer, PlayerIdentity>();
        private readonly HashSet<string> _loggedIdentityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Harmony _harmony;
        private float _nextScanTime;

        private ConfigEntry<bool> _logToConsole;
        private ConfigEntry<bool> _writeCsv;
        private ConfigEntry<bool> _writeLegacyTextFile;
        private ConfigEntry<bool> _fallbackScan;
        private ConfigEntry<float> _fallbackScanInterval;
        private ConfigEntry<string> _logDirectory;
        private ConfigEntry<string> _csvFileName;
        private ConfigEntry<string> _legacyFileName;

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;

            _logToConsole = Config.Bind("Logging", "LogToBepInExConsole", true, "Log player identities to the BepInEx console/log.");
            _writeCsv = Config.Bind("Logging", "WriteCsv", true, "Append player identities to BepInEx/server-logs/players.csv.");
            _writeLegacyTextFile = Config.Bind("Logging", "WriteLegacyServerLoggerTxt", true, "Keep the old ServerLogger.txt format for tools that already parse it.");
            _fallbackScan = Config.Bind("Logging", "FallbackPlayerScan", true, "Also scan connected players in case the Epic token callback is changed by another mod.");
            _fallbackScanInterval = Config.Bind("Logging", "FallbackScanIntervalSeconds", 2.0f, "How often to scan connected players when fallback scanning is enabled.");
            _logDirectory = Config.Bind("Paths", "LogDirectory", "server-logs", "Relative to BepInEx, or an absolute path.");
            _csvFileName = Config.Bind("Paths", "CsvFileName", "players.csv", "CSV file name inside LogDirectory.");
            _legacyFileName = Config.Bind("Paths", "LegacyFileName", "ServerLogger.txt", "Legacy text file name, stored in the server root unless absolute.");

            _harmony = new Harmony(PluginGuid);
            PatchEpicTokenCallback();

            _nextScanTime = Time.unscaledTime + Mathf.Max(0.5f, _fallbackScanInterval.Value);
            Logger.LogInfo("[ServerLogger] Loaded owned player identity logger.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            if (_fallbackScan == null || !_fallbackScan.Value)
                return;

            if (Time.unscaledTime < _nextScanTime)
                return;

            _nextScanTime = Time.unscaledTime + Mathf.Max(0.5f, _fallbackScanInterval.Value);
            ScanConnectedPlayers();
        }

        private void PatchEpicTokenCallback()
        {
            try
            {
                MethodInfo callback = AccessTools.Method(typeof(RoomInitRequestCommand), "OnVerifiesEpicToken");
                MethodInfo postfix = AccessTools.Method(typeof(ServerLoggerPlugin), nameof(OnVerifiesEpicTokenPostfix));

                if (callback == null || postfix == null)
                {
                    Logger.LogWarning("[ServerLogger] Could not find Epic token callback. Fallback scanning remains active.");
                    return;
                }

                _harmony.Patch(callback, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[ServerLogger] Could not patch Epic token callback: " + ex.Message);
            }
        }

        private static void OnVerifiesEpicTokenPostfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length == 0 || __args[0] == null)
                    return;

                object clientData = ReadMember(__args[0], "ClientData");
                var player = clientData as TABGPlayerServer;
                if (player != null)
                    Instance?.LogPlayer(player, "epic-token");
            }
            catch (Exception ex)
            {
                LogSource?.LogWarning("[ServerLogger] Token callback logging failed: " + ex.Message);
            }
        }

        private void ScanConnectedPlayers()
        {
            try
            {
                ServerClient server = UnityEngine.Object.FindObjectOfType<ServerClient>();
                if (server == null || server.GameRoomReference == null || server.GameRoomReference.Players == null)
                    return;

                foreach (TABGPlayerServer player in server.GameRoomReference.Players)
                {
                    if (player != null)
                        LogPlayer(player, "player-scan");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[ServerLogger] Fallback player scan failed: " + ex.Message);
            }
        }

        private void LogPlayer(TABGPlayerServer player, string source)
        {
            PlayerIdentity identity = PlayerIdentity.From(player);
            string key = identity.IdentityKey;

            PlayerIdentity previousIdentity;
            if (_loggedPlayers.TryGetValue(player, out previousIdentity) && !identity.IsRicherThan(previousIdentity))
                return;

            if (!string.IsNullOrWhiteSpace(key) && _loggedIdentityKeys.Contains(key))
            {
                if (!_loggedPlayers.ContainsKey(player))
                {
                    _loggedPlayers[player] = identity;
                    return;
                }
            }

            _loggedPlayers[player] = identity;
            if (!string.IsNullOrWhiteSpace(key))
                _loggedIdentityKeys.Add(key);

            if (_logToConsole.Value)
            {
                Logger.LogInfo("[ServerLogger] Player joined: name=\"" + identity.PlayerName + "\" playFab=\"" + identity.PlayFabId + "\" epic=\"" + identity.EpicId + "\" index=" + identity.PlayerIndex);
            }

            if (_writeCsv.Value)
                AppendCsv(identity, source);

            if (_writeLegacyTextFile.Value)
                UpdateLegacyLog(identity);
        }

        private void AppendCsv(PlayerIdentity identity, string source)
        {
            string path = ResolveCsvPath();
            EnsureParentDirectory(path);

            bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            using (var writer = new StreamWriter(path, append: true, Encoding.UTF8))
            {
                if (needsHeader)
                    writer.WriteLine("TimestampUtc,Source,PlayerName,PlayFabId,EpicId,PlayerIndex,GroupIndex");

                writer.WriteLine(string.Join(",",
                    Csv(DateTime.UtcNow.ToString("o")),
                    Csv(source),
                    Csv(identity.PlayerName),
                    Csv(identity.PlayFabId),
                    Csv(identity.EpicId),
                    Csv(identity.PlayerIndex.ToString(CultureInfo.InvariantCulture)),
                    Csv(identity.GroupIndex.ToString(CultureInfo.InvariantCulture))));
            }
        }

        private void UpdateLegacyLog(PlayerIdentity identity)
        {
            string path = ResolveLegacyPath();
            EnsureParentDirectory(path);

            string name = NormalizeSingleLine(string.IsNullOrWhiteSpace(identity.PlayerName) ? "Player" : identity.PlayerName);
            string playFab = NormalizeSingleLine(identity.PlayFabId);
            string epic = NormalizeSingleLine(identity.EpicId);
            string stableIdentity = !string.IsNullOrWhiteSpace(epic) ? epic : playFab;

            var lines = new List<string>();
            if (File.Exists(path))
                lines.AddRange(File.ReadAllLines(path));

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i] ?? string.Empty;
                string[] parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                string lineIdentity = parts[parts.Length - 1];
                if (string.IsNullOrWhiteSpace(stableIdentity) || !lineIdentity.Equals(stableIdentity, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    File.WriteAllLines(path, lines.ToArray());
                    return;
                }

                lines[i] = name + " " + line;
                File.WriteAllLines(path, lines.ToArray());
                return;
            }

            if (!string.IsNullOrWhiteSpace(epic))
                lines.Add(name + " " + playFab + " " + epic);
            else if (!string.IsNullOrWhiteSpace(playFab))
                lines.Add(name + " " + playFab);
            else
                lines.Add(name);

            File.WriteAllLines(path, lines.ToArray());
        }

        private string ResolveCsvPath()
        {
            string directory = _logDirectory.Value;
            if (string.IsNullOrWhiteSpace(directory))
                directory = "server-logs";

            if (!Path.IsPathRooted(directory))
                directory = Path.Combine(GetBepInExRoot(), directory);

            string fileName = string.IsNullOrWhiteSpace(_csvFileName.Value) ? "players.csv" : _csvFileName.Value;
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(directory, fileName);
        }

        private string ResolveLegacyPath()
        {
            string fileName = string.IsNullOrWhiteSpace(_legacyFileName.Value) ? "ServerLogger.txt" : _legacyFileName.Value;
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(GetServerRoot(), fileName);
        }

        private static string GetBepInExRoot()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Paths.BepInExRootPath))
                    return Paths.BepInExRootPath;
            }
            catch
            {
            }

            return Path.Combine(GetServerRoot(), "BepInEx");
        }

        private static string GetServerRoot()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Paths.GameRootPath))
                    return Paths.GameRootPath;
            }
            catch
            {
            }

            try
            {
                var dataDirectory = new DirectoryInfo(Application.dataPath);
                if (dataDirectory.Parent != null)
                    return dataDirectory.Parent.FullName;
            }
            catch
            {
            }

            return Directory.GetCurrentDirectory();
        }

        private static void EnsureParentDirectory(string path)
        {
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            bool quote = value.IndexOfAny(CsvQuotedCharacters) >= 0;
            value = value.Replace("\"", "\"\"");
            return quote ? "\"" + value + "\"" : value;
        }

        private static string NormalizeSingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null)
                return property.GetValue(instance, null);

            FieldInfo field = type.GetField(name, flags);
            return field != null ? field.GetValue(instance) : null;
        }

        private struct PlayerIdentity
        {
            public string PlayerName;
            public string PlayFabId;
            public string EpicId;
            public byte PlayerIndex;
            public byte GroupIndex;

            public string IdentityKey
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(EpicId))
                        return "epic:" + EpicId;
                    if (!string.IsNullOrWhiteSpace(PlayFabId))
                        return "playfab:" + PlayFabId;
                    return "slot:" + PlayerIndex + ":" + PlayerName;
                }
            }

            public bool IsRicherThan(PlayerIdentity previous)
            {
                return Score() > previous.Score();
            }

            private int Score()
            {
                int score = 0;
                if (!string.IsNullOrWhiteSpace(PlayerName)) score++;
                if (!string.IsNullOrWhiteSpace(PlayFabId)) score += 2;
                if (!string.IsNullOrWhiteSpace(EpicId)) score += 4;
                return score;
            }

            public static PlayerIdentity From(TABGPlayerServer player)
            {
                return new PlayerIdentity
                {
                    PlayerName = ReadString(player, "PlayerName"),
                    PlayFabId = ReadString(player, "PlayFabID"),
                    EpicId = ReadString(player, "EpicUserName"),
                    PlayerIndex = ReadByte(player, "PlayerIndex"),
                    GroupIndex = ReadByte(player, "GroupIndex")
                };
            }

            private static string ReadString(object instance, string member)
            {
                object value = ReadMember(instance, member);
                return ValueToString(value);
            }

            private static byte ReadByte(object instance, string member)
            {
                object value = ReadMember(instance, member);
                if (value is byte b)
                    return b;

                try
                {
                    return Convert.ToByte(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return 0;
                }
            }
        }

        private static string ValueToString(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is string text)
                return text;

            try
            {
                Type type = value.GetType();
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "op_Implicit" || method.ReturnType != typeof(string))
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == type)
                    {
                        object converted = method.Invoke(null, new[] { value });
                        return converted as string ?? string.Empty;
                    }
                }
            }
            catch
            {
            }

            return value.ToString() ?? string.Empty;
        }
    }
}
