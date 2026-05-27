using System.Linq;
using FluentAssertions;
using TabgInstaller.Core;
using TabgInstaller.UI.PluginCatalog;
using Xunit;

namespace TabgInstaller.Tests.Ui
{
    public class PluginCatalogGrouperTests
    {
        [Fact]
        public void Collapse_MergesDefinitionsThatShareSameDllSet()
        {
            var definitions = new[]
            {
                new PluginDefinition("A", "First Feature - one", new[] { "Shared.dll" }, true, PluginKind.Bundled),
                new PluginDefinition("B", "Second Feature - two", new[] { "Shared.dll" }, false, PluginKind.Bundled),
                new PluginDefinition("C", "Other Feature - three", new[] { "Other.dll" }, false, PluginKind.Bundled)
            };

            var groups = PluginCatalogGrouper.Collapse(definitions);

            groups.Should().HaveCount(2);
            groups.Should().ContainSingle(group =>
                group.Primary.Id == "A" &&
                group.Definitions.Select(definition => definition.Id).SequenceEqual(new[] { "A", "B" }) &&
                group.DllNames.SequenceEqual(new[] { "Shared.dll" }) &&
                group.Label == "First Feature / Second Feature - Shared.dll");
        }

        [Fact]
        public void Collapse_KeepsDllLessDefinitionsSeparateById()
        {
            var definitions = new[]
            {
                new PluginDefinition("CoreA", "Core A", System.Array.Empty<string>(), true, PluginKind.CoreDependency),
                new PluginDefinition("CoreB", "Core B", System.Array.Empty<string>(), true, PluginKind.CoreDependency)
            };

            var groups = PluginCatalogGrouper.Collapse(definitions);

            groups.Should().HaveCount(2);
            groups.Select(group => group.Primary.Id).Should().Equal("CoreA", "CoreB");
        }
    }
}
