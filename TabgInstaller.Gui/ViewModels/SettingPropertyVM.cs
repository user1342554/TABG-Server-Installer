using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public class SettingPropertyVM : INotifyPropertyChanged
    {
        private readonly PropertyInfo _prop;
        private readonly object _model;
        private bool _showAdvanced = false;
        
        public SettingPropertyVM(PropertyInfo prop, object model)
        {
            _prop = prop;
            _model = model;
        }

        public string Name => _prop.Name;
        public Type PropType => _prop.PropertyType;

        public bool IsBool => PropType == typeof(bool);

        public bool BoolValue
        {
            get 
            {
                try
                {
                    return (bool)_prop.GetValue(_model)!;
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                try
                {
                    if (value != BoolValue)
                    {
                        _prop.SetValue(_model, value);
                        OnPropertyChanged(nameof(BoolValue));
                        OnPropertyChanged(nameof(ValueString));
                    }
                }
                catch
                {
                    // Ignore property setting errors
                }
            }
        }

        public string ValueString
        {
            get
            {
                try
                {
                    var val = _prop.GetValue(_model);
                    return val switch
                    {
                        null => string.Empty,
                        float f => f.ToString(CultureInfo.InvariantCulture),
                        _ => val.ToString() ?? string.Empty
                    };
                }
                catch
                {
                    return string.Empty;
                }
            }
            set
            {
                try
                {
                    if (value == null) return;
                    
                    object? converted = null;
                    try
                    {
                        converted = PropType switch
                        {
                            Type t when t == typeof(string) => value,
                            Type t when t == typeof(int) => string.IsNullOrEmpty(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture),
                            Type t when t == typeof(float) => string.IsNullOrEmpty(value) ? 0f : float.Parse(value, CultureInfo.InvariantCulture),
                            Type t when t == typeof(bool) => string.IsNullOrEmpty(value) ? false : bool.Parse(value),
                            _ => value
                        };
                    }
                    catch
                    {
                        // If conversion fails, ignore
                        return;
                    }
                    
                    if (converted != null)
                    {
                        try
                        {
                            _prop.SetValue(_model, converted);
                            OnPropertyChanged();
                        }
                        catch
                        {
                            // Ignore property set errors
                            return;
                        }
                        
                        // Safely trigger visibility updates
                        try
                        {
                            if (Name == "GameMode" || Name == "TeamMode")
                            {
                                OnPropertyChanged(nameof(IsVisible));
                            }
                        }
                        catch
                        {
                            // Ignore visibility update errors
                        }
                    }
                }
                catch
                {
                    // Catch-all for any remaining errors
                }
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
                    OnPropertyChanged(nameof(IsVisible));
                } 
            } 
        }

        public bool IsVisible
        {
            get
            {
                try
                {
                    // Always show core settings
                    if (IsCoreSettings) return true;
                    
                    // Game mode specific settings
                    var gameMode = GetCurrentGameMode();
                    if (IsRingSettings && gameMode != "BattleRoyale") return false;
                    if (IsBombSettings && gameMode != "Bomb") return false;
                    if (IsBrawlSettings && gameMode != "Brawl") return false;
                    
                    // Advanced/Debug settings require toggle
                    if (IsAdvancedSettings) return ShowAdvanced;
                    
                    return true;
                }
                catch
                {
                    // If there's any error, show the property by default
                    return true;
                }
            }
        }

        public bool ShowGameModeWarning
        {
            get
            {
                try
                {
                    var gameMode = GetCurrentGameMode();
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
                    var gameMode = GetCurrentGameMode();
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

        public string ControlType
        {
            get
            {
                return Name switch
                {
                    "GameMode" => "ComboBox",
                    "TeamMode" => "ComboBox", 
                    "CarSpawnRate" or "StripLootByPercentage" => "Slider",
                    "MaxPlayers" or "Port" or "ForceStartTime" or "Countdown" or "BaseRingTime" or 
                    "TimeBeforeFirstRing" or "WeaponDissapearTime" or "BombDefuseTime" or
                    "GroupsToStart" or "KillsToWin" or "MinPlayersToForceStart" or "PlayersToStart" or
                    "BombTime" or "RoundTime" or "RoundsToWin" or "MaxNumberOfTeamsAuto" or "SpawnBots" => "NumericUpDown",
                    _ when IsBool => "CheckBox",
                    _ => "TextBox"
                };
            }
        }

        public object? ComboBoxItems
        {
            get
            {
                return Name switch
                {
                    "GameMode" => new[] { "BattleRoyale", "Bomb", "Brawl", "Test", "Deception" },
                    "TeamMode" => new[] { "SQUAD", "DUO", "SOLO" },
                    _ => null
                };
            }
        }

        public double SliderMinimum
        {
            get
            {
                return Name switch
                {
                    "CarSpawnRate" or "StripLootByPercentage" => 0.0,
                    _ => 0.0
                };
            }
        }

        public double SliderMaximum
        {
            get
            {
                return Name switch
                {
                    "CarSpawnRate" or "StripLootByPercentage" => 1.0,
                    _ => 1.0
                };
            }
        }

        public double NumericMinimum
        {
            get
            {
                return Name switch
                {
                    "Port" => 1024,
                    "MaxPlayers" => 1,
                    _ => 0
                };
            }
        }

        public double NumericMaximum
        {
            get
            {
                return Name switch
                {
                    "Port" => 65535,
                    "MaxPlayers" => 253,
                    "ForceStartTime" or "BaseRingTime" or "TimeBeforeFirstRing" => 3600,
                    "Countdown" or "WeaponDissapearTime" or "BombDefuseTime" => 300,
                    "GroupsToStart" or "KillsToWin" => 100,
                    "MinPlayersToForceStart" or "PlayersToStart" => 50,
                    "BombTime" or "RoundTime" => 600,
                    "RoundsToWin" or "MaxNumberOfTeamsAuto" => 20,
                    "SpawnBots" => 50,
                    _ => double.MaxValue
                };
            }
        }

        private bool IsCoreSettings => Name switch
        {
            "ServerName" or "ServerDescription" or "ServerBrowserIP" or "Password" or
            "Port" or "MaxPlayers" or "Relay" or "AutoTeam" or "LAN" or
            "UseTimedForceStart" or "ForceStartTime" or "MinPlayersToForceStart" or "PlayersToStart" or "Countdown" or
            "CarSpawnRate" or "AllowRespawnMinigame" or "AllowSpectating" or
            "TeamMode" or "GameMode" or "AntiCheat" => true,
            _ => false
        };

        private bool IsRingSettings => Name switch
        {
            "RingSizes" or "RingSpeeds" or "BaseRingTime" or "TimeBeforeFirstRing" or "NoRing" => true,
            _ => false
        };

        private bool IsBombSettings => Name switch
        {
            "BombTime" or "BombDefuseTime" or "RoundTime" or "RoundsToWin" => true,
            _ => false
        };

        private bool IsBrawlSettings => Name switch
        {
            "GroupsToStart" or "KillsToWin" or "WeaponDissapearTime" or "NumberOfLivesPerTeam" or "MaxNumberOfTeamsAuto" => true,
            _ => false
        };

        private bool IsAdvancedSettings => Name switch
        {
            "UseSouls" or "UseKicks" or "SpawnBots" or "DEBUG_DEATHMATCH" or
            "UsePlayFabStats" or "AllowRejoins" or "AntiCheatDebugLogging" or "AntiCheatEventLogging" or
            "StripLootByPercentage" => true,
            _ => false
        };

        private string GetCurrentGameMode()
        {
            try
            {
                if (_model is GameSettingsData gameSettings)
                {
                    return gameSettings.GameMode ?? "BattleRoyale";
                }
            }
            catch
            {
                // Ignore errors during property access
            }
            return "BattleRoyale"; // default
        }

        public void NotifyPropertyChanged(string propertyName)
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string? name = null)
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
            }
            catch
            {
                // Ignore PropertyChanged event errors
            }
        }
    }
} 