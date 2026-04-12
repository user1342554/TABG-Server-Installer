using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using TabgInstaller.Core.Model;
using Xunit;

namespace TabgInstaller.Tests.Model
{
    public class PluginManifestTests
    {
        [Fact]
        public void Deserialize_ValidJson_AllFieldsPopulated()
        {
            var json = @"{
  ""id"": ""test-plugin"",
  ""name"": ""Test Plugin"",
  ""version"": ""1.0.0"",
  ""description"": ""A test plugin."",
  ""author"": ""TestAuthor"",
  ""downloadUrl"": ""https://github.com/TestAuthor/TestPlugin/releases/latest"",
  ""dllNames"": [""TestPlugin.dll""],
  ""type"": ""server"",
  ""compatibleTabgVersions"": [""*""],
  ""minInstallerVersion"": ""4.0.0"",
  ""bepInExVersion"": ""5.4.22"",
  ""dependencies"": [""citruslib""],
  ""tags"": [""test""],
  ""requiresClientMod"": false
}";

            var manifest = JsonConvert.DeserializeObject<PluginManifest>(json)!;

            manifest.Id.Should().Be("test-plugin");
            manifest.Name.Should().Be("Test Plugin");
            manifest.Version.Should().Be("1.0.0");
            manifest.Description.Should().Be("A test plugin.");
            manifest.Author.Should().Be("TestAuthor");
            manifest.DownloadUrl.Should().Be("https://github.com/TestAuthor/TestPlugin/releases/latest");
            manifest.DllNames.Should().BeEquivalentTo(new[] { "TestPlugin.dll" });
            manifest.Type.Should().Be("server");
            manifest.CompatibleTabgVersions.Should().BeEquivalentTo(new[] { "*" });
            manifest.MinInstallerVersion.Should().Be("4.0.0");
            manifest.BepInExVersion.Should().Be("5.4.22");
            manifest.Dependencies.Should().BeEquivalentTo(new[] { "citruslib" });
            manifest.Tags.Should().BeEquivalentTo(new[] { "test" });
            manifest.RequiresClientMod.Should().BeFalse();
        }

        [Fact]
        public void Deserialize_OptionalFieldsMissing_DefaultsApplied()
        {
            var json = @"{
  ""id"": ""minimal-plugin"",
  ""name"": ""Minimal"",
  ""version"": ""0.1.0"",
  ""description"": ""Bare minimum."",
  ""author"": ""Someone"",
  ""downloadUrl"": ""https://example.com/plugin.zip"",
  ""dllNames"": [""Minimal.dll""],
  ""type"": ""server"",
  ""compatibleTabgVersions"": [""*""],
  ""minInstallerVersion"": ""4.0.0"",
  ""bepInExVersion"": ""5.4.22""
}";

            var manifest = JsonConvert.DeserializeObject<PluginManifest>(json)!;

            manifest.Dependencies.Should().BeEmpty();
            manifest.Tags.Should().BeEmpty();
            manifest.AuthorUrl.Should().BeNull();
            manifest.RepositoryUrl.Should().BeNull();
            manifest.IconUrl.Should().BeNull();
            manifest.RequiresClientMod.Should().BeFalse();
            manifest.ClientPluginId.Should().BeNull();
            manifest.Changelog.Should().BeNull();
        }

        [Fact]
        public void Deserialize_RegistryResponse_ParsesPluginList()
        {
            var json = @"{
  ""version"": 1,
  ""generatedAt"": ""2026-04-12T00:00:00Z"",
  ""plugins"": [
    {
      ""id"": ""test-plugin"",
      ""name"": ""Test Plugin"",
      ""version"": ""1.0.0"",
      ""description"": ""A test plugin."",
      ""author"": ""TestAuthor"",
      ""downloadUrl"": ""https://example.com/plugin.zip"",
      ""dllNames"": [""TestPlugin.dll""],
      ""type"": ""server"",
      ""compatibleTabgVersions"": [""*""],
      ""minInstallerVersion"": ""4.0.0"",
      ""bepInExVersion"": ""5.4.22""
    }
  ]
}";

            var response = JsonConvert.DeserializeObject<PluginRegistryResponse>(json)!;

            response.Version.Should().Be(1);
            response.GeneratedAt.Should().Be("2026-04-12T00:00:00Z");
            response.Plugins.Should().HaveCount(1);
            response.Plugins[0].Id.Should().Be("test-plugin");
            response.Plugins[0].Name.Should().Be("Test Plugin");
        }

        [Fact]
        public void Deserialize_InstalledPluginsData_RoundTrip()
        {
            var data = new InstalledPluginsData
            {
                Plugins = new List<InstalledPluginEntry>
                {
                    new InstalledPluginEntry
                    {
                        Id = "test-plugin",
                        InstalledVersion = "1.0.0",
                        InstalledAt = "2026-04-12T10:00:00Z",
                        UpdatedAt = null,
                        Pinned = false,
                        DllNames = new[] { "TestPlugin.dll" }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(data);
            var deserialized = JsonConvert.DeserializeObject<InstalledPluginsData>(json)!;

            deserialized.Plugins.Should().HaveCount(1);
            deserialized.Plugins[0].Id.Should().Be("test-plugin");
            deserialized.Plugins[0].InstalledVersion.Should().Be("1.0.0");
            deserialized.Plugins[0].InstalledAt.Should().Be("2026-04-12T10:00:00Z");
            deserialized.Plugins[0].UpdatedAt.Should().BeNull();
            deserialized.Plugins[0].Pinned.Should().BeFalse();
            deserialized.Plugins[0].DllNames.Should().BeEquivalentTo(new[] { "TestPlugin.dll" });
        }
    }
}
