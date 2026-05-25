using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using TabgInstaller.Core.Model;

namespace TabgInstaller.Gui.ViewModels
{
    public partial class PluginSettingValueViewModel : ObservableObject
    {
        public PluginSettingDefinition Definition { get; }

        [ObservableProperty] private string _value = "";

        public PluginSettingValueViewModel(PluginSettingDefinition definition, string value)
        {
            Definition = definition;
            _value = value;
        }

        public string Label => Definition.Label;
        public string Description => Definition.Description;
        public string DefaultText => string.IsNullOrEmpty(Definition.DefaultValue)
            ? "Default: empty"
            : $"Default: {Definition.DefaultValue}";
        public string[] Options => Definition.Options;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public bool HasOptions => Options.Length > 0;
        public string AutomationId => "PluginSetting" + new string(Definition.FullKey.Where(char.IsLetterOrDigit).ToArray());

        public bool BoolValue
        {
            get => Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            set
            {
                var next = value ? "true" : "false";
                if (Value.Equals(next, StringComparison.OrdinalIgnoreCase))
                    return;

                Value = next;
                OnPropertyChanged();
            }
        }

        public string ControlType
        {
            get
            {
                if (Definition.ValueType == PluginSettingValueType.Boolean)
                    return "CheckBox";

                if (HasOptions)
                    return "ComboBox";

                return Definition.IsMultiline ? "MultilineTextBox" : "TextBox";
            }
        }

        partial void OnValueChanged(string value)
        {
            OnPropertyChanged(nameof(BoolValue));
        }
    }

    public sealed class PluginSettingsGroupViewModel
    {
        public PluginConfigDefinition Definition { get; }
        public ObservableCollection<PluginSettingValueViewModel> Settings { get; }
        public string RootPath { get; }

        public PluginSettingsGroupViewModel(
            PluginConfigDefinition definition,
            string rootPath,
            ObservableCollection<PluginSettingValueViewModel> settings)
        {
            Definition = definition;
            RootPath = rootPath;
            Settings = settings;
        }

        public string DisplayName => Definition.DisplayName;
        public string Description => Definition.Description;
        public bool HasSettings => Settings.Count > 0;
        public string ScopeText => Definition.Scope == PluginSettingScope.Client ? "Client" : "Server";
        public string ConfigPath => string.IsNullOrWhiteSpace(Definition.ConfigFileName)
            ? ""
            : Path.Combine(RootPath, "BepInEx", "config", Definition.ConfigFileName);
        public string EmptyText => "No installer-configurable settings for this bundled plugin yet.";
    }
}
