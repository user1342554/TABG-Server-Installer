using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace ConfigSanitizer
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: ConfigSanitizer.exe <path_to_TheStarterPack.json> [path_to_WordBank.json]");
                Console.Error.WriteLine("Example: ConfigSanitizer.exe \"C:\\Path\\To\\Server\\TheStarterPack.json\" \"C:\\Path\\To\\Server\\LootTables\\WordBank.json\"");
                return 1; // Error code for incorrect usage
            }

            var packPath = args[0];
            var wordBankPathArg = (args.Length >= 2) ? args[1] : null;

            if (!File.Exists(packPath))
            {
                Console.Error.WriteLine($"[ERROR] TheStarterPack.json nicht gefunden unter: {packPath}");
                return 2; // Error code for file not found
            }

            // Wenn ein WordBank-Pfad angegeben wurde, prüfe dessen Existenz. Wenn nicht, ist es optional.
            if (wordBankPathArg != null && !File.Exists(wordBankPathArg))
            {
                Console.WriteLine($"[WARN] WordBank.json nicht gefunden unter: {wordBankPathArg}. ServerName/Password werden nicht aus WordBank generiert.");
                wordBankPathArg = null; // Setze auf null, damit WriteRandomNameAndPassword den Fallback nutzt
            }
            else if (wordBankPathArg == null)
            {
                Console.WriteLine("[INFO] Kein Pfad für WordBank.json angegeben. ServerName/Password werden nicht aus WordBank generiert, falls sie nicht im Standardpfad relativ zu TheStarterPack.json liegt oder Standardwerte verwendet.");
                // Versuche, einen Standardpfad für WordBankPath abzuleiten, falls nicht explizit gegeben
                // Dies ist eine Annahme: LootTables/WordBank.json relativ zum Ordner von TheStarterPack.json
                string? assumedWordBankDirectory = Path.GetDirectoryName(packPath);
                if (assumedWordBankDirectory != null)
                {
                    string potentialWordBankPath = Path.Combine(assumedWordBankDirectory, "LootTables", "WordBank.json");
                    if (File.Exists(potentialWordBankPath))
                    {
                        wordBankPathArg = potentialWordBankPath;
                        Console.WriteLine($"[INFO] WordBank.json im Standardpfad gefunden: {wordBankPathArg}");
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] WordBank.json auch nicht im Standardpfad gefunden: {potentialWordBankPath}");
                    }
                }
            }

            try
            {
                var cfgText = File.ReadAllText(packPath);
                var cfg = JObject.Parse(cfgText);

                Console.WriteLine("[INFO] Starte Konfigurationsanpassungen...");

                FixItemsGivenFormat(cfg);
                FixLoadoutsFormat(cfg);
                SanitizeConfigNumbers(cfg);
                WriteRandomNameAndPassword(cfg, wordBankPathArg); // wordBankPathArg kann hier null sein

                File.WriteAllText(packPath, cfg.ToString(Formatting.Indented));
                Console.WriteLine($"[INFO] TheStarterPack.json erfolgreich aktualisiert: {packPath}");
                return 0; // Success
            }
            catch (JsonReaderException jsonEx)
            {
                Console.Error.WriteLine($"[ERROR] Fehler beim Parsen der JSON-Datei ({packPath}): {jsonEx.Message}");
                return 3; // Error code for JSON parsing error
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Unerwarteter Fehler beim Verarbeiten der Datei ({packPath}): {ex.Message}");
                return 4; // General error
            }
        }

        private static void FixItemsGivenFormat(JObject cfg)
        {
            var itemsGivenToken = cfg["ItemsGiven"];
            if (itemsGivenToken == null) { 
                Console.WriteLine("[DEBUG] ItemsGiven ist null."); 
                cfg["ItemsGiven"] = new JArray(); // Erstelle leeres Array, wenn nicht vorhanden
                return; 
            }

            Console.WriteLine($"[DEBUG] ItemsGiven Typ: {itemsGivenToken.Type}, Wert: {itemsGivenToken.ToString(Formatting.None)}");

            if (itemsGivenToken.Type == JTokenType.String)
            {
                string itemsGivenStr = itemsGivenToken.ToString();
                if (string.IsNullOrWhiteSpace(itemsGivenStr) || itemsGivenStr == "0")
                {
                    cfg["ItemsGiven"] = new JArray();
                    Console.WriteLine("[INFO] ItemsGiven (String) war leer oder '0', umgewandelt zu leerem Array.");
                }
                else
                {
                    cfg["ItemsGiven"] = ParseItemListFromString(itemsGivenStr);
                    Console.WriteLine("[INFO] ItemsGiven (String) erfolgreich zu Array umgewandelt.");
                }
            }
            else if (itemsGivenToken.Type != JTokenType.Array)
            {
                cfg["ItemsGiven"] = new JArray();
                 Console.WriteLine("[WARN] ItemsGiven war weder String noch Array, zu leerem Array umgewandelt.");
            }
        }

        private static void FixLoadoutsFormat(JObject cfg)
        {
            var loadoutsToken = cfg["Loadouts"];
            if (loadoutsToken == null) { 
                Console.WriteLine("[DEBUG] Loadouts ist null."); 
                cfg["Loadouts"] = new JArray(); // Erstelle leeres Array, wenn nicht vorhanden
                return; 
            }
            
            Console.WriteLine($"[DEBUG] Loadouts Typ: {loadoutsToken.Type}, Wert: {loadoutsToken.ToString(Formatting.None)}");

            if (loadoutsToken.Type == JTokenType.String)
            {
                string loadoutsStr = loadoutsToken.ToString();
                var loadoutArray = new JArray();

                if (string.IsNullOrWhiteSpace(loadoutsStr) || loadoutsStr == "0")
                {
                    cfg["Loadouts"] = new JArray();
                    Console.WriteLine("[INFO] Loadouts (String) war leer oder '0', umgewandelt zu leerem Array.");
                    return;
                }

                foreach (var slotStr in loadoutsStr.Split('/'))
                {
                    if (string.IsNullOrWhiteSpace(slotStr) || slotStr == "0")
                    {
                        loadoutArray.Add(new JObject());
                        continue;
                    }
                    var slotObj = new JObject();
                    var itemsInSlot = slotStr.Split(',');
                    if (itemsInSlot.Length > 0 && !string.IsNullOrWhiteSpace(itemsInSlot[0]) && itemsInSlot[0] != "0") slotObj["Primary"] = ParseItemFromString(itemsInSlot[0]);
                    if (itemsInSlot.Length > 1 && !string.IsNullOrWhiteSpace(itemsInSlot[1]) && itemsInSlot[1] != "0") slotObj["Secondary"] = ParseItemFromString(itemsInSlot[1]);
                    if (itemsInSlot.Length > 2 && !string.IsNullOrWhiteSpace(itemsInSlot[2]) && itemsInSlot[2] != "0") slotObj["Melee"] = ParseItemFromString(itemsInSlot[2]);
                    if (itemsInSlot.Length > 3 && !string.IsNullOrWhiteSpace(itemsInSlot[3]) && itemsInSlot[3] != "0") slotObj["Throwable"] = ParseItemFromString(itemsInSlot[3]);
                    loadoutArray.Add(slotObj);
                }
                cfg["Loadouts"] = loadoutArray;
                Console.WriteLine("[INFO] Loadouts (String) erfolgreich zu Array umgewandelt.");
            }
            else if (loadoutsToken.Type != JTokenType.Array)
            {
                cfg["Loadouts"] = new JArray();
                Console.WriteLine("[WARN] Loadouts war weder String noch Array, zu leerem Array umgewandelt.");
            }
        }

        private static void SanitizeConfigNumbers(JObject cfg)
        {
            Console.WriteLine("[INFO] Starte Bereinigung der Zahlenwerte...");
            string[] directIntProps = { "MaxPlayers", "WarmupTimeSeconds", "CircleSpeed", "TeamSize", "LobbyTimer", "RingShrinkTime" };

            foreach (var propName in directIntProps)
            {
                var token = cfg[propName];
                if (token != null)
                {
                    if (token.Type == JTokenType.String && string.IsNullOrWhiteSpace(token.ToString()))
                    {
                        Console.WriteLine($"[DEBUG] {propName} ist ein leerer String, setze auf 0.");
                        cfg[propName] = 0;
                    }
                    else if (int.TryParse(token.ToString(), out int val))
                    {
                        cfg[propName] = Math.Max(0, val);
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] {propName} ('{token}') ist keine gültige Zahl, setze auf 0.");
                        cfg[propName] = 0;
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] {propName} nicht in JSON gefunden, setze auf Default 0 (oder spezifischen Default, falls definiert).");
                     // Setze einen sinnvollen Default, wenn das Property fehlt
                    if (propName == "MaxPlayers") cfg[propName] = 10; else cfg[propName] = 0;
                }
            }

            if (cfg["ItemsGiven"] is JArray itemsArray)
            {
                foreach (var itemEntry in itemsArray.OfType<JObject>())
                {
                    SanitizeJObjectIntField(itemEntry, "Item", null);
                    SanitizeJObjectIntField(itemEntry, "Ammo", null);
                }
            }

            if (cfg["Loadouts"] is JArray loadoutsArray)
            {
                foreach (var loadoutSlot in loadoutsArray.OfType<JObject>())
                {
                    SanitizeLoadoutItemField(loadoutSlot, "Primary");
                    SanitizeLoadoutItemField(loadoutSlot, "Secondary");
                    SanitizeLoadoutItemField(loadoutSlot, "Melee");
                    SanitizeLoadoutItemField(loadoutSlot, "Throwable");
                }
            }
            Console.WriteLine("[INFO] Bereinigung der Zahlenwerte abgeschlossen.");
        }

        private static void SanitizeLoadoutItemField(JObject loadoutSlot, string itemName)
        {
            var itemToken = loadoutSlot[itemName];
            if (itemToken is JObject itemObj) 
            {
                SanitizeJObjectIntField(itemObj, "Item", null); 
                SanitizeJObjectIntField(itemObj, "Ammo", null);
            }
            else if (itemToken != null && itemToken.Type != JTokenType.Null)
            {
                // Dieser Fall sollte nach FixLoadoutsFormat selten sein, da es JObjects erzeugt.
                // Wenn hier doch mal ein direkter Wert landet, der keine Zahl ist, entfernen.
                if (!int.TryParse(itemToken.ToString(), out _))
                {
                    Console.WriteLine($"[WARN] Loadout-Item '{itemName}' ('{itemToken}') ist ungültig und kein Objekt, wird entfernt.");
                    loadoutSlot.Remove(itemName);
                }
            }
        }

        private static void SanitizeJObjectIntField(JObject obj, string fieldName, int? defaultValueForInvalid)
        {
            var fieldToken = obj[fieldName];
            if (fieldToken != null && fieldToken.Type != JTokenType.Null)
            {
                if (int.TryParse(fieldToken.ToString(), out int val))
                {
                    obj[fieldName] = Math.Max(0, val);
                }
                else
                {
                    Console.WriteLine($"[WARN] Feld '{fieldName}' ('{fieldToken}') in JObject ist keine gültige Zahl.");
                    if (defaultValueForInvalid.HasValue)
                    {
                        obj[fieldName] = defaultValueForInvalid.Value;
                        Console.WriteLine($"[INFO] Setze '{fieldName}' auf Default: {defaultValueForInvalid.Value}.");
                    }
                    else
                    {
                        obj.Remove(fieldName);
                        Console.WriteLine($"[INFO] Entferne ungültiges Feld '{fieldName}'.");
                    }
                }
            }
            else if (defaultValueForInvalid.HasValue) // Feld nicht vorhanden oder null, aber Default ist gegeben
            {
                obj[fieldName] = defaultValueForInvalid.Value;
                 Console.WriteLine($"[DEBUG] Feld '{fieldName}' nicht vorhanden oder null, setze auf Default: {defaultValueForInvalid.Value}.");
            }
        }

        private static void WriteRandomNameAndPassword(JObject cfg, string? wordBankPath)
        {
            Console.WriteLine("[INFO] Versuche ServerNamen und Passwort zu setzen...");
            if (string.IsNullOrEmpty(wordBankPath) || !File.Exists(wordBankPath))
            {
                Console.WriteLine("[WARN] WordBank Pfad ist ungültig oder Datei nicht gefunden. Verwende Standard ServerNamen/Passwort.");
                cfg["ServerName"] = cfg["ServerName"]?.ToString() ?? "TABG Auto Server"; // Behalte existierenden Namen wenn möglich, sonst Default
                cfg["Password"] = cfg["Password"]?.ToString() ?? "";
                return;
            }

            try
            {
                var wordsText = File.ReadAllText(wordBankPath);
                var words = JsonConvert.DeserializeObject<string[]>(wordsText);

                if (words == null || words.Length == 0)
                {
                    Console.WriteLine("[WARN] WordBank ist leer. Verwende Standard ServerNamen/Passwort.");
                    cfg["ServerName"] = "TABG Fallback Server";
                    cfg["Password"] = "1234";
                    return;
                }

                var validWords = words.Where(w => !string.IsNullOrWhiteSpace(w) && w.Length >= 2).ToArray();
                if (validWords.Length == 0) validWords = new string[] { "Default", "Words" }; // Fallback, falls keine validen Wörter
                
                var rnd = new Random();
                string w1 = validWords[rnd.Next(validWords.Length)];
                string w2 = validWords.Length > 1 ? validWords[rnd.Next(validWords.Length)] : w1; // Stelle sicher, dass w2 einen Wert hat

                cfg["ServerName"] = $"{w1} {w2} Server"; // Beispiel: Zwei Wörter + "Server"
                cfg["Password"] = $"{validWords[rnd.Next(validWords.Length)]}{rnd.Next(1000, 9999)}";

                Console.WriteLine($"[INFO] ServerName gesetzt: {cfg["ServerName"]}");
                Console.WriteLine($"[INFO] Passwort gesetzt: {cfg["Password"]}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] Fehler beim Setzen von ServerName/Password aus WordBank: {ex.Message}");
                cfg["ServerName"] = cfg["ServerName"]?.ToString() ?? "ErrorName Server";
                cfg["Password"] = cfg["Password"]?.ToString() ?? "errorpass";
            }
        }

        private static JObject ParseItemFromString(string itemStr)
        {
            var itemObj = new JObject();
            if (string.IsNullOrWhiteSpace(itemStr) || itemStr == "0") return itemObj;
            var parts = itemStr.Split(':');
            if (parts.Length > 0 && int.TryParse(parts[0], out int itemId) && itemId != 0)
            {
                itemObj["Item"] = itemId;
                if (parts.Length > 1 && int.TryParse(parts[1], out int ammo) && ammo > 0)
                {
                    itemObj["Ammo"] = ammo;
                }
            }
            return itemObj;
        }

        private static JArray ParseItemListFromString(string itemListStr)
        {
            var jArray = new JArray();
            if (string.IsNullOrWhiteSpace(itemListStr)) return jArray;
            var items = itemListStr.Split(',');
            foreach (var itemStr in items)
            {
                if (!string.IsNullOrWhiteSpace(itemStr))
                {
                     var parsedItem = ParseItemFromString(itemStr);
                     if (parsedItem.HasValues) // Füge nur hinzu, wenn das Item tatsächlich geparst wurde (z.B. gültige ID)
                        jArray.Add(parsedItem);
                }
            }
            return jArray;
        }
    }
} 