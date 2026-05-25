using System;

namespace TabgInstaller.Core.Model
{
    public enum PluginSettingScope
    {
        Server,
        Client
    }

    public enum PluginSettingValueType
    {
        Boolean,
        Int32,
        Single,
        String,
        KeyCode
    }

    public sealed class PluginConfigDefinition
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public PluginSettingScope Scope { get; set; }
        public string? ConfigFileName { get; set; }
        public PluginSettingDefinition[] Settings { get; set; } = Array.Empty<PluginSettingDefinition>();
    }

    public sealed class PluginSettingDefinition
    {
        public string Section { get; set; } = "";
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public PluginSettingValueType ValueType { get; set; } = PluginSettingValueType.String;
        public string DefaultValue { get; set; } = "";
        public string[] Options { get; set; } = Array.Empty<string>();
        public bool IsMultiline { get; set; }

        public string FullKey => string.IsNullOrEmpty(Section) ? Key : $"{Section}.{Key}";
    }
}
