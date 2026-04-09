using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using TabgInstaller.Core;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class LoadoutEditorViewModelTests
    {
        private readonly Mock<IServerPathProvider> _serverPath = new();
        private readonly Mock<IToastService> _toast = new();

        public LoadoutEditorViewModelTests()
        {
            // Default: empty path so no I/O happens during construction.
            _serverPath.SetupGet(s => s.ServerPath).Returns(string.Empty);
        }

        private LoadoutEditorViewModel CreateSut() =>
            new(_serverPath.Object, _toast.Object);

        // ── Initial state ────────────────────────────────────────────────────────

        [Fact]
        public void InitialState_Loadouts_IsEmpty()
        {
            var sut = CreateSut();
            sut.Loadouts.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_SelectedLoadout_IsNull()
        {
            var sut = CreateSut();
            sut.SelectedLoadout.Should().BeNull();
        }

        [Fact]
        public void InitialState_IsDirty_IsFalse()
        {
            var sut = CreateSut();
            sut.IsDirty.Should().BeFalse();
        }

        [Fact]
        public void InitialState_SelectedMode_IsNormal()
        {
            var sut = CreateSut();
            sut.SelectedMode.Should().Be("Normal");
        }

        [Fact]
        public void InitialState_StatusText_IsEmpty()
        {
            var sut = CreateSut();
            sut.StatusText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_SearchResults_IsEmpty()
        {
            var sut = CreateSut();
            sut.SearchResults.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_CurrentItems_IsEmpty()
        {
            var sut = CreateSut();
            sut.CurrentItems.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_CurrentCurses_IsEmpty()
        {
            var sut = CreateSut();
            sut.CurrentCurses.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_SearchResultsVisible_IsFalse()
        {
            var sut = CreateSut();
            sut.SearchResultsVisible.Should().BeFalse();
        }

        [Fact]
        public void InitialState_CategorySelectVisible_IsTrue()
        {
            var sut = CreateSut();
            sut.CategorySelectVisible.Should().BeTrue();
        }

        [Fact]
        public void InitialState_QuantityText_IsOne()
        {
            var sut = CreateSut();
            sut.QuantityText.Should().Be("1");
        }

        // ── MarkDirty / ClearDirty ───────────────────────────────────────────────

        [Fact]
        public void MarkDirty_SetsIsDirtyTrue()
        {
            var sut = CreateSut();
            sut.MarkDirty();
            sut.IsDirty.Should().BeTrue();
        }

        [Fact]
        public void MarkDirty_SetsStatusTextToUnsavedChanges()
        {
            var sut = CreateSut();
            sut.MarkDirty();
            sut.StatusText.Should().Be("Unsaved changes");
        }

        [Fact]
        public void ClearDirty_SetsIsDirtyFalse()
        {
            var sut = CreateSut();
            sut.MarkDirty();
            sut.ClearDirty();
            sut.IsDirty.Should().BeFalse();
        }

        [Fact]
        public void MarkDirty_CalledTwice_SetsStatusOnlyOnce()
        {
            var sut = CreateSut();
            sut.MarkDirty();
            sut.StatusText = "other";
            sut.MarkDirty(); // second call — already dirty, should NOT overwrite
            sut.StatusText.Should().Be("other");
        }

        // ── AddLoadout command ───────────────────────────────────────────────────

        [Fact]
        public void AddLoadoutCommand_AddsOneLoadout()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.Loadouts.Should().HaveCount(1);
        }

        [Fact]
        public void AddLoadoutCommand_SelectsNewLoadout()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.SelectedLoadout.Should().NotBeNull();
            sut.SelectedLoadout!.Name.Should().Be("New Loadout");
        }

        [Fact]
        public void AddLoadoutCommand_MarksDirty()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.IsDirty.Should().BeTrue();
        }

        [Fact]
        public void AddLoadoutCommand_DefaultPercent_IsOneHundred()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.SelectedLoadout!.Percent.Should().Be(100);
        }

        // ── RemoveLoadout command (no confirmation in unit test scenario) ────────
        // RemoveLoadout shows a MessageBox — hard to unit test fully.
        // Instead test that without a selected loadout nothing explodes.

        [Fact]
        public void RemoveLoadoutCommand_WithNoSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.RemoveLoadoutCommand.Execute(null);
            act.Should().NotThrow();
        }

        // ── DuplicateLoadout command ─────────────────────────────────────────────

        [Fact]
        public void DuplicateLoadoutCommand_WithNoSelection_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.DuplicateLoadoutCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void DuplicateLoadoutCommand_DuplicatesLoadout()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.DuplicateLoadoutCommand.Execute(null);
            sut.Loadouts.Should().HaveCount(2);
        }

        [Fact]
        public void DuplicateLoadoutCommand_CopiedNameHasSuffix()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.DuplicateLoadoutCommand.Execute(null);
            sut.Loadouts[1].Name.Should().Contain("Copy");
        }

        [Fact]
        public void DuplicateLoadoutCommand_SelectsCopy()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.DuplicateLoadoutCommand.Execute(null);
            sut.SelectedLoadout.Should().BeSameAs(sut.Loadouts[1]);
        }

        // ── MoveUp / MoveDown commands ───────────────────────────────────────────

        [Fact]
        public void MoveUpCommand_WithNoSelection_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.MoveUpCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void MoveDownCommand_WithNoSelection_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.MoveDownCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void MoveUpCommand_MovesLoadoutUp()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.AddLoadoutCommand.Execute(null);
            var second = sut.Loadouts[1];
            sut.SelectedLoadout = second;
            sut.MoveUpCommand.Execute(null);
            sut.Loadouts[0].Should().BeSameAs(second);
        }

        [Fact]
        public void MoveDownCommand_MovesLoadoutDown()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.AddLoadoutCommand.Execute(null);
            var first = sut.Loadouts[0];
            sut.SelectedLoadout = first;
            sut.MoveDownCommand.Execute(null);
            sut.Loadouts[1].Should().BeSameAs(first);
        }

        [Fact]
        public void MoveUpCommand_AlreadyAtTop_DoesNotMove()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.AddLoadoutCommand.Execute(null);
            var first = sut.Loadouts[0];
            sut.SelectedLoadout = first;
            sut.MoveUpCommand.Execute(null);
            sut.Loadouts[0].Should().BeSameAs(first);
        }

        // ── SearchText → SearchResults ───────────────────────────────────────────

        [Fact]
        public void SearchText_EmptyString_ClearsSearchResultsAndShowsCategory()
        {
            var sut = CreateSut();
            sut.SearchText = "gun";
            sut.SearchText = "";
            sut.SearchResults.Should().BeEmpty();
            sut.SearchResultsVisible.Should().BeFalse();
            sut.CategorySelectVisible.Should().BeTrue();
        }

        [Fact]
        public void SearchText_NonEmptyQuery_PopulatesSearchResults()
        {
            var sut = CreateSut();
            sut.SearchText = "gun";
            // ItemDatabase has many items — some should match "gun"
            sut.SearchResults.Should().NotBeEmpty();
        }

        [Fact]
        public void SearchText_NonEmptyQuery_HidesCategorySelect()
        {
            var sut = CreateSut();
            sut.SearchText = "gun";
            sut.CategorySelectVisible.Should().BeFalse();
        }

        [Fact]
        public void SearchText_NonEmptyQuery_ShowsSearchResults()
        {
            var sut = CreateSut();
            sut.SearchText = "gun";
            sut.SearchResultsVisible.Should().BeTrue();
        }

        [Fact]
        public void SearchText_NoMatchingQuery_SearchResultsEmpty()
        {
            var sut = CreateSut();
            sut.SearchText = "xyzqwertynonexistent12345";
            sut.SearchResults.Should().BeEmpty();
            sut.SearchResultsVisible.Should().BeFalse();
        }

        [Fact]
        public void SearchText_Results_LimitedToThirty()
        {
            var sut = CreateSut();
            sut.SearchText = "a"; // broad query likely produces many results
            sut.SearchResults.Count.Should().BeLessOrEqualTo(30);
        }

        // ── AddItemFromSearch ─────────────────────────────────────────────────────

        [Fact]
        public void AddItemFromSearchCommand_WithNullSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var item = new ItemSearchResult { Id = "1", Name = "Test", Category = "Test" };
            var act = () => sut.AddItemFromSearchCommand.Execute(item);
            act.Should().NotThrow();
        }

        [Fact]
        public void AddItemFromSearchCommand_AddsItemToCurrentLoadout()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            var item = new ItemSearchResult { Id = "99", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            sut.SelectedLoadout!.Items.Should().ContainSingle(i => i.Id == "99");
        }

        [Fact]
        public void AddItemFromSearchCommand_UsesQuantityText()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.QuantityText = "5";
            var item = new ItemSearchResult { Id = "99", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            sut.SelectedLoadout!.Items[0].Quantity.Should().Be(5);
        }

        [Fact]
        public void AddItemFromSearchCommand_ClearsSearchText()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.SearchText = "gun";
            var item = new ItemSearchResult { Id = "99", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            sut.SearchText.Should().BeEmpty();
        }

        [Fact]
        public void AddItemFromSearchCommand_MarksDirty()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.ClearDirty();
            var item = new ItemSearchResult { Id = "99", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            sut.IsDirty.Should().BeTrue();
        }

        // ── RemoveItem command ───────────────────────────────────────────────────

        [Fact]
        public void RemoveItemCommand_WithNullSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var row = new ItemDisplayRow { Id = "1", Quantity = 1 };
            var act = () => sut.RemoveItemCommand.Execute(row);
            act.Should().NotThrow();
        }

        [Fact]
        public void RemoveItemCommand_RemovesMatchingItem()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            var item = new ItemSearchResult { Id = "55", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            var row = sut.CurrentItems.First(r => r.Id == "55");
            sut.RemoveItemCommand.Execute(row);
            sut.SelectedLoadout!.Items.Should().NotContain(i => i.Id == "55");
        }

        [Fact]
        public void RemoveItemCommand_MarksDirty()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            var item = new ItemSearchResult { Id = "55", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            sut.ClearDirty();
            var row = sut.CurrentItems.First(r => r.Id == "55");
            sut.RemoveItemCommand.Execute(row);
            sut.IsDirty.Should().BeTrue();
        }

        // ── UpdateItemQuantity ───────────────────────────────────────────────────

        [Fact]
        public void UpdateItemQuantity_UpdatesQuantityOnMatchingItem()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            var item = new ItemSearchResult { Id = "77", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            var row = sut.CurrentItems.First(r => r.Id == "77");
            sut.UpdateItemQuantity(row, 10);
            sut.SelectedLoadout!.Items.First(i => i.Id == "77").Quantity.Should().Be(10);
        }

        [Fact]
        public void UpdateItemQuantity_InvalidQty_DoesNotUpdate()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.QuantityText = "3";
            var item = new ItemSearchResult { Id = "77", Name = "TestItem", Category = "Test" };
            sut.AddItemFromSearchCommand.Execute(item);
            var row = sut.CurrentItems.First(r => r.Id == "77");
            sut.UpdateItemQuantity(row, 0); // invalid
            sut.SelectedLoadout!.Items.First(i => i.Id == "77").Quantity.Should().Be(3);
        }

        [Fact]
        public void UpdateItemQuantity_WithNoSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var row = new ItemDisplayRow { Id = "77", Quantity = 1 };
            var act = () => sut.UpdateItemQuantity(row, 5);
            act.Should().NotThrow();
        }

        // ── UpdateSelectedLoadoutName ────────────────────────────────────────────

        [Fact]
        public void UpdateSelectedLoadoutName_UpdatesName()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.UpdateSelectedLoadoutName("My Loadout");
            sut.SelectedLoadout!.Name.Should().Be("My Loadout");
        }

        [Fact]
        public void UpdateSelectedLoadoutName_WithNoSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.UpdateSelectedLoadoutName("anything");
            act.Should().NotThrow();
        }

        // ── UpdateSelectedLoadoutPercent ─────────────────────────────────────────

        [Fact]
        public void UpdateSelectedLoadoutPercent_UpdatesPercent()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.UpdateSelectedLoadoutPercent("75");
            sut.SelectedLoadout!.Percent.Should().Be(75);
        }

        [Fact]
        public void UpdateSelectedLoadoutPercent_InvalidText_DoesNotUpdate()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.UpdateSelectedLoadoutPercent("not-a-number");
            sut.SelectedLoadout!.Percent.Should().Be(100); // default
        }

        [Fact]
        public void UpdateSelectedLoadoutPercent_WithNoSelectedLoadout_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.UpdateSelectedLoadoutPercent("50");
            act.Should().NotThrow();
        }

        // ── SelectedLoadout changed → CurrentItems / CurrentCurses ──────────────

        [Fact]
        public void SelectedLoadout_Changed_RefreshesCurrentItems()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            var lo = sut.SelectedLoadout!;
            lo.Items.Add(new ItemVm("100", 2));
            // Trigger refresh by re-selecting
            sut.SelectedLoadout = null;
            sut.SelectedLoadout = lo;
            sut.CurrentItems.Should().ContainSingle(r => r.Id == "100");
        }

        [Fact]
        public void SelectedLoadout_SetToNull_ClearsCurrentItems()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.SelectedLoadout = null;
            sut.CurrentItems.Should().BeEmpty();
        }

        // ── ParseLoadoutCurses ───────────────────────────────────────────────────

        [Fact]
        public void ParseLoadoutCurses_EmptyString_ReturnsEmptyList()
        {
            var result = LoadoutEditorViewModel.ParseLoadoutCurses("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoadoutCurses_SingleGroup_ParsesIds()
        {
            var result = LoadoutEditorViewModel.ParseLoadoutCurses("1,2,3");
            result.Should().HaveCount(1);
            result[0].Should().Contain(1).And.Contain(2).And.Contain(3);
        }

        [Fact]
        public void ParseLoadoutCurses_MultipleGroups_ParsesAllGroups()
        {
            var result = LoadoutEditorViewModel.ParseLoadoutCurses("1,2/3,4/5");
            result.Should().HaveCount(3);
            result[0].Should().Contain(1).And.Contain(2);
            result[1].Should().Contain(3).And.Contain(4);
            result[2].Should().Contain(5);
        }

        [Fact]
        public void ParseLoadoutCurses_EmptyGroup_AddsEmptySet()
        {
            var result = LoadoutEditorViewModel.ParseLoadoutCurses("/1,2");
            result.Should().HaveCount(2);
            result[0].Should().BeEmpty();
            result[1].Should().Contain(1).And.Contain(2);
        }

        // ── BuildLoadoutCurses ───────────────────────────────────────────────────

        [Fact]
        public void BuildLoadoutCurses_NoLoadouts_ReturnsEmpty()
        {
            var sut = CreateSut();
            sut.BuildLoadoutCurses().Should().BeEmpty();
        }

        [Fact]
        public void BuildLoadoutCurses_AllEmptyCurses_ReturnsEmpty()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.AddLoadoutCommand.Execute(null);
            sut.BuildLoadoutCurses().Should().BeEmpty();
        }

        [Fact]
        public void BuildLoadoutCurses_WithCurses_ProducesSlashSeparated()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null);
            sut.AddLoadoutCommand.Execute(null);
            sut.Loadouts[0].Curses.Add(1);
            sut.Loadouts[0].Curses.Add(2);
            sut.Loadouts[1].Curses.Add(3);
            var result = sut.BuildLoadoutCurses();
            result.Should().Contain("/");
            result.Should().Contain("3");
        }

        [Fact]
        public void BuildLoadoutCurses_TrimsTrailingEmptyGroups()
        {
            var sut = CreateSut();
            sut.AddLoadoutCommand.Execute(null); // with curse
            sut.AddLoadoutCommand.Execute(null); // empty curses (trailing)
            sut.Loadouts[0].Curses.Add(5);
            var result = sut.BuildLoadoutCurses();
            result.Should().NotEndWith("/");
        }

        // ── BuildExportRaw ───────────────────────────────────────────────────────

        [Fact]
        public void BuildExportRaw_NoLoadouts_ReturnsEmptyOrPrefix()
        {
            var sut = CreateSut();
            var result = sut.BuildExportRaw();
            result.Should().NotBeNull();
        }

        [Fact]
        public void BuildExportRaw_GunGameMode_PrefixesResult()
        {
            var sut = CreateSut();
            sut.SelectedMode = "GunGame";
            sut.AddLoadoutCommand.Execute(null);
            var result = sut.BuildExportRaw();
            result.Should().StartWith("GunGame/");
        }

        [Fact]
        public void BuildExportRaw_NormalMode_NoPrefixAdded()
        {
            var sut = CreateSut();
            sut.SelectedMode = "Normal";
            var result = sut.BuildExportRaw();
            result.Should().NotStartWith("Normal/");
        }

        // ── ImportRaw command ────────────────────────────────────────────────────

        [Fact]
        public void ImportRawCommand_EmptyString_ShowsWarningToast()
        {
            var sut = CreateSut();
            sut.ImportRawCommand.Execute("");
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ImportRawCommand_ValidString_PopulatesLoadouts()
        {
            var sut = CreateSut();
            // Format: "Name:Percent% items/" — at least one loadout
            sut.ImportRawCommand.Execute("Default:100%/");
            sut.Loadouts.Should().NotBeEmpty();
        }

        [Fact]
        public void ImportRawCommand_GunGamePrefix_SetsMode()
        {
            var sut = CreateSut();
            sut.ImportRawCommand.Execute("GunGame/Default:100%/");
            sut.SelectedMode.Should().Be("GunGame");
        }

        [Fact]
        public void ImportRawCommand_ReverseGunGamePrefix_SetsMode()
        {
            var sut = CreateSut();
            sut.ImportRawCommand.Execute("ReverseGunGame/Default:100%/");
            sut.SelectedMode.Should().Be("ReverseGunGame");
        }

        [Fact]
        public void ImportRawCommand_KeepInventoryPrefix_SetsMode()
        {
            var sut = CreateSut();
            sut.ImportRawCommand.Execute("KeepInventory/Default:100%/");
            sut.SelectedMode.Should().Be("KeepInventory");
        }

        // ── LoadoutVm DisplayName ────────────────────────────────────────────────

        [Fact]
        public void LoadoutVm_DisplayName_ContainsNameAndPercent()
        {
            var vm = new LoadoutVm { Name = "Alpha", Percent = 75 };
            vm.DisplayName.Should().Contain("Alpha").And.Contain("75%");
        }

        [Fact]
        public void LoadoutVm_DisplayName_ShowsItemCount()
        {
            var vm = new LoadoutVm { Name = "Test", Percent = 100 };
            vm.Items.Add(new ItemVm("1", 1));
            vm.Items.Add(new ItemVm("2", 1));
            vm.DisplayName.Should().Contain("2 items");
        }

        [Fact]
        public void LoadoutVm_DisplayName_SingularItem()
        {
            var vm = new LoadoutVm { Name = "Test", Percent = 100 };
            vm.Items.Add(new ItemVm("1", 1));
            vm.DisplayName.Should().Contain("1 item");
            vm.DisplayName.Should().NotContain("1 items");
        }

        // ── ItemSearchResult DisplayText ─────────────────────────────────────────

        [Fact]
        public void ItemSearchResult_DisplayText_FormatsCorrectly()
        {
            var r = new ItemSearchResult { Id = "42", Name = "Shotgun", Category = "Weapons" };
            r.DisplayText.Should().Be("Shotgun (42) [Weapons]");
        }

        // ── SaveCommand — no server path ─────────────────────────────────────────

        [Fact]
        public void SaveCommand_WithEmptyServerPath_ShowsWarningToast()
        {
            var sut = CreateSut();
            sut.SaveCommand.Execute(null);
            _toast.Verify(t => t.Warning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void SaveCommand_WithEmptyServerPath_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.SaveCommand.Execute(null);
            act.Should().NotThrow();
        }

        // ── Dispose ──────────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_CanBeCalledTwiceWithoutException()
        {
            var sut = CreateSut();
            sut.Dispose();
            var act = () => sut.Dispose();
            act.Should().NotThrow();
        }
    }
}
