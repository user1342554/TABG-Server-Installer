using System.Collections.ObjectModel;
using System.Linq;
using TabgInstaller.Core.Model;
using System.Reflection;

namespace TabgInstaller.Gui.ViewModels
{
    public class GameSettingsDynamicViewModel
    {
        private readonly GameSettingsData _model;
        public ObservableCollection<SettingPropertyVM> Properties { get; }

        public GameSettingsDynamicViewModel(GameSettingsData model)
        {
            _model = model;
            var props = typeof(GameSettingsData).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Use the same order as defined in ConfigIO for consistency
            string[] order =
            {
                "ServerName", "ServerDescription", "Port", "MaxPlayers", "Relay", "AutoTeam", "Password",
                "CarSpawnRate", "UseTimedForceStart", "ForceStartTime", "MinPlayersToForceStart", "PlayersToStart", "Countdown",
                "RingSizes", "AllowRespawnMinigame", "TeamMode", "GameMode", "AntiCheat"
            };
            var propDict = props.ToDictionary(p => p.Name);
            var orderedProps = order.Where(propDict.ContainsKey).Select(name => propDict[name]);

            Properties = new ObservableCollection<SettingPropertyVM>(orderedProps.Select(p => new SettingPropertyVM(p, _model)));
        }

        public GameSettingsData ToModel() => _model;
    }
} 