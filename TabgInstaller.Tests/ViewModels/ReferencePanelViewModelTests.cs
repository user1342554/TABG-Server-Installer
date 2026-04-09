using FluentAssertions;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class ReferencePanelViewModelTests
    {
        private static ReferencePanelViewModel CreateSut() => new();

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_ReferenceText_IsEmpty()
        {
            var sut = CreateSut();
            sut.ReferenceText.Should().BeEmpty();
        }

        // ── ShowCommands ─────────────────────────────────────────────────────────

        [Fact]
        public void ShowCommandsCommand_SetsReferenceText()
        {
            var sut = CreateSut();
            sut.ShowCommandsCommand.Execute(null);
            sut.ReferenceText.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowCommandsCommand_ContainsCommandsHeader()
        {
            var sut = CreateSut();
            sut.ShowCommandsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("TABG SERVER COMMANDS REFERENCE");
        }

        [Fact]
        public void ShowCommandsCommand_ContainsCitrusLibSection()
        {
            var sut = CreateSut();
            sut.ShowCommandsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("CITRUSLIB COMMANDS");
        }

        [Fact]
        public void ShowCommandsCommand_ContainsMoreModsSection()
        {
            var sut = CreateSut();
            sut.ShowCommandsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("MOREMODS COMMANDS");
        }

        // ── ShowItems ────────────────────────────────────────────────────────────

        [Fact]
        public void ShowItemsCommand_SetsReferenceText()
        {
            var sut = CreateSut();
            sut.ShowItemsCommand.Execute(null);
            sut.ReferenceText.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowItemsCommand_ContainsItemsHeader()
        {
            var sut = CreateSut();
            sut.ShowItemsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("TABG ITEM IDS REFERENCE");
        }

        [Fact]
        public void ShowItemsCommand_ContainsRiflesSection()
        {
            var sut = CreateSut();
            sut.ShowItemsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("RIFLES");
        }

        [Fact]
        public void ShowItemsCommand_ContainsCursesSection()
        {
            var sut = CreateSut();
            sut.ShowItemsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("CURSES");
        }

        // ── ShowLoadouts ─────────────────────────────────────────────────────────

        [Fact]
        public void ShowLoadoutsCommand_SetsReferenceText()
        {
            var sut = CreateSut();
            sut.ShowLoadoutsCommand.Execute(null);
            sut.ReferenceText.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowLoadoutsCommand_ContainsLoadoutsHeader()
        {
            var sut = CreateSut();
            sut.ShowLoadoutsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("LOADOUT PRESETS REFERENCE");
        }

        [Fact]
        public void ShowLoadoutsCommand_ContainsGunGameSection()
        {
            var sut = CreateSut();
            sut.ShowLoadoutsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("GUN GAME");
        }

        [Fact]
        public void ShowLoadoutsCommand_ContainsScavengeSection()
        {
            var sut = CreateSut();
            sut.ShowLoadoutsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("SCAVENGE");
        }

        // ── ShowSpawns ───────────────────────────────────────────────────────────

        [Fact]
        public void ShowSpawnsCommand_SetsReferenceText()
        {
            var sut = CreateSut();
            sut.ShowSpawnsCommand.Execute(null);
            sut.ReferenceText.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowSpawnsCommand_ContainsSpawnsHeader()
        {
            var sut = CreateSut();
            sut.ShowSpawnsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("SPAWN LOCATIONS REFERENCE");
        }

        [Fact]
        public void ShowSpawnsCommand_ContainsSingleSpawnSection()
        {
            var sut = CreateSut();
            sut.ShowSpawnsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("SINGLE SPAWN POINTS");
        }

        [Fact]
        public void ShowSpawnsCommand_ContainsMultiSpawnSection()
        {
            var sut = CreateSut();
            sut.ShowSpawnsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("MULTI-SPAWN AREAS");
        }

        // ── ShowMatchSettings ────────────────────────────────────────────────────

        [Fact]
        public void ShowMatchSettingsCommand_SetsReferenceText()
        {
            var sut = CreateSut();
            sut.ShowMatchSettingsCommand.Execute(null);
            sut.ReferenceText.Should().NotBeEmpty();
        }

        [Fact]
        public void ShowMatchSettingsCommand_ContainsMatchSettingsHeader()
        {
            var sut = CreateSut();
            sut.ShowMatchSettingsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("MATCH SETTINGS GUIDE");
        }

        [Fact]
        public void ShowMatchSettingsCommand_ContainsRingSettingsSection()
        {
            var sut = CreateSut();
            sut.ShowMatchSettingsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("RING SETTINGS");
        }

        [Fact]
        public void ShowMatchSettingsCommand_ContainsGameModeSection()
        {
            var sut = CreateSut();
            sut.ShowMatchSettingsCommand.Execute(null);
            sut.ReferenceText.Should().Contain("GAME MODE INDEXES");
        }

        // ── Property-change notification ─────────────────────────────────────────

        [Fact]
        public void ShowCommandsCommand_RaisesPropertyChangedForReferenceText()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.ReferenceText))
                    raised = true;
            };

            sut.ShowCommandsCommand.Execute(null);

            raised.Should().BeTrue();
        }

        [Fact]
        public void ShowItemsCommand_RaisesPropertyChangedForReferenceText()
        {
            var sut = CreateSut();
            var raised = false;
            sut.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(sut.ReferenceText))
                    raised = true;
            };

            sut.ShowItemsCommand.Execute(null);

            raised.Should().BeTrue();
        }

        // ── Each command sets distinct content ───────────────────────────────────

        [Fact]
        public void AllFiveCommands_ProduceDifferentContent()
        {
            var sut = CreateSut();

            sut.ShowCommandsCommand.Execute(null);
            var commands = sut.ReferenceText;

            sut.ShowItemsCommand.Execute(null);
            var items = sut.ReferenceText;

            sut.ShowLoadoutsCommand.Execute(null);
            var loadouts = sut.ReferenceText;

            sut.ShowSpawnsCommand.Execute(null);
            var spawns = sut.ReferenceText;

            sut.ShowMatchSettingsCommand.Execute(null);
            var matchSettings = sut.ReferenceText;

            commands.Should().NotBe(items);
            commands.Should().NotBe(loadouts);
            commands.Should().NotBe(spawns);
            commands.Should().NotBe(matchSettings);
            items.Should().NotBe(loadouts);
            items.Should().NotBe(spawns);
            items.Should().NotBe(matchSettings);
            loadouts.Should().NotBe(spawns);
            loadouts.Should().NotBe(matchSettings);
            spawns.Should().NotBe(matchSettings);
        }
    }
}
