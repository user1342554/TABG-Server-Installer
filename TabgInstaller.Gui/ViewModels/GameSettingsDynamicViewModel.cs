using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TabgInstaller.Core.Model;
using System.Reflection;

namespace TabgInstaller.Gui.ViewModels
{
    public class GameSettingsDynamicViewModel : INotifyPropertyChanged
    {
        private readonly GameSettingsData _model;
        private bool _showAdvanced = false;
        private SettingPropertyVM? _gameModeProperty;
        private bool _suppressPropertyEvents = false;
        
        public ObservableCollection<SettingPropertyVM> Properties { get; }

        public GameSettingsDynamicViewModel(GameSettingsData model)
        {
            _model = model;
            var props = typeof(GameSettingsData).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Use a comprehensive order; remaining properties are appended alphabetically
            string[] order =
            {
                // Identity & networking
                "ServerName", "ServerDescription", "ServerBrowserIP", "Password",
                "Port", "MaxPlayers",
                // Lobby & start flow
                "Relay", "AutoTeam", "LAN", "AllowSpectating",
                "UseTimedForceStart", "ForceStartTime", "MinPlayersToForceStart", "PlayersToStart", "Countdown",
                // Gameplay pacing
                "CarSpawnRate",
                "RingSizes", "RingSpeeds",
                "AllowRespawnMinigame",
                "BaseRingTime", "TimeBeforeFirstRing",
                // Extra tuning
                "StripLootByPercentage", "WeaponDissapearTime", "BombDefuseTime",
                // Modes & scoring
                "GroupsToStart", "KillsToWin", "RoundsToWin", "NumberOfLivesPerTeam", "MaxNumberOfTeamsAuto",
                "SpawnBots", "UseSouls",
                // Toggles
                "NoRing", "UseKicks", "DEBUG_DEATHMATCH", "UsePlayFabStats", "AllowRejoins", "AntiCheatDebugLogging", "AntiCheatEventLogging",
                // Enums
                "TeamMode", "GameMode", "AntiCheat"
            };
            var propDict = props.ToDictionary(p => p.Name);
            var orderedProps = order.Where(propDict.ContainsKey).Select(name => propDict[name]);
            var extraProps = props.Where(p => !order.Contains(p.Name)).OrderBy(p => p.Name);

            Properties = new ObservableCollection<SettingPropertyVM>(
                orderedProps
                    .Concat(extraProps)
                    .Select(p => new SettingPropertyVM(p, _model))
            );
            
            // Find the GameMode property to watch for changes
            _gameModeProperty = Properties.FirstOrDefault(p => p.Name == "GameMode");
            
            // Subscribe to property changes to update visibility when GameMode changes
            foreach (var prop in Properties)
            {
                prop.PropertyChanged += OnPropertyChanged;
            }
        }

        public bool ShowAdvanced
        {
            get => _showAdvanced;
            set
            {
                if (_showAdvanced != value)
                {
                    _showAdvanced = value;
                    OnPropertyChanged();
                    
                    // Batch update: temporarily suppress per-item change events
                    _suppressPropertyEvents = true;
                    try
                    {
                        foreach (var prop in Properties)
                        {
                            prop.ShowAdvanced = value;
                        }
                    }
                    finally
                    {
                        _suppressPropertyEvents = false;
                    }

                    // Explicitly refresh visibility once after the batch
                    RefreshAllVisibility();
                }
            }
        }

        public bool ShowGameModeWarning
        {
            get
            {
                try
                {
                    var gameMode = _model?.GameMode ?? "BattleRoyale";
                    return gameMode != "BattleRoyale";
                }
                catch
                {
                    return false;
                }
            }
        }

        public string GameModeWarningText
        {
            get
            {
                try
                {
                    var gameMode = _model?.GameMode ?? "BattleRoyale";
                    return gameMode switch
                    {
                        "Bomb" => "⚠️ Bomb mode is experimental and may not work properly",
                        "Brawl" => "⚠️ Brawl mode is experimental and may not work properly",
                        "Test" => "⚠️ Test mode is for debugging only",
                        "Deception" => "⚠️ Deception mode is experimental and may not work properly",
                        _ => ""
                    };
                }
                catch
                {
                    return "";
                }
            }
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (_suppressPropertyEvents) return;
                // When GameMode changes, update visibility for all properties and warnings
                if (sender is SettingPropertyVM prop && prop.Name == "GameMode")
                {
                    RefreshAllVisibility();
                    // Update main warning display
                    OnPropertyChanged(nameof(ShowGameModeWarning));
                    OnPropertyChanged(nameof(GameModeWarningText));
                }
            }
            catch
            {
                // Ignore property change cascade errors
            }
        }

        private void RefreshAllVisibility()
        {
            try
            {
                foreach (var prop in Properties)
                {
                    try
                    {
                        // Each SettingPropertyVM needs to notify about its IsVisible change and warning changes
                        prop.NotifyPropertyChanged(nameof(SettingPropertyVM.IsVisible));
                        prop.NotifyPropertyChanged(nameof(SettingPropertyVM.ShowGameModeWarning));
                        prop.NotifyPropertyChanged(nameof(SettingPropertyVM.GameModeWarningText));
                    }
                    catch
                    {
                        // Continue with next property if one fails
                    }
                }
            }
            catch
            {
                // Ignore bulk refresh errors
            }
        }

        public GameSettingsData ToModel() => _model;
        
        public void UpdateFromGameSettings(GameSettingsData newSettings)
        {
            // Update each property with new values
            foreach (var prop in Properties)
            {
                var propInfo = typeof(GameSettingsData).GetProperty(prop.Name);
                if (propInfo != null)
                {
                    var newValue = propInfo.GetValue(newSettings);
                    if (prop.IsBool && newValue is bool boolValue)
                    {
                        prop.BoolValue = boolValue;
                    }
                    else if (newValue != null)
                    {
                        prop.ValueString = newValue.ToString() ?? "";
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            catch
            {
                // Ignore PropertyChanged event errors
            }
        }
    }
} 