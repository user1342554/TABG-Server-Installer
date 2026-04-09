using FluentAssertions;
using Moq;
using TabgInstaller.Gui.Services;
using TabgInstaller.Gui.ViewModels;
using Xunit;

namespace TabgInstaller.Tests.ViewModels
{
    public class SuperSecretSettingsViewModelTests
    {
        private readonly Mock<IToastService> _toast = new();

        private SuperSecretSettingsViewModel CreateSut() =>
            new(_toast.Object);

        [Fact]
        public void InitialState_IsNotUnlocked()
        {
            var sut = CreateSut();
            sut.IsUnlocked.Should().BeFalse();
        }

        [Fact]
        public void InitialState_IsNotRunning()
        {
            var sut = CreateSut();
            sut.IsRunning.Should().BeFalse();
        }

        [Fact]
        public void InitialState_PasswordInput_IsEmpty()
        {
            var sut = CreateSut();
            sut.PasswordInput.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_ErrorText_IsEmpty()
        {
            var sut = CreateSut();
            sut.ErrorText.Should().BeEmpty();
        }

        [Fact]
        public void InitialState_EnterButtonEnabled()
        {
            var sut = CreateSut();
            sut.EnterButtonEnabled.Should().BeTrue();
        }

        [Fact]
        public void InitialState_StopButtonNotVisible()
        {
            var sut = CreateSut();
            sut.StopButtonVisible.Should().BeFalse();
        }

        [Fact]
        public void EnterCommand_WrongPassword_SetsErrorText()
        {
            var sut = CreateSut();
            sut.PasswordInput = "wrong";

            // EnterAsync triggers a MessageBox for correct password only;
            // wrong password returns early with ErrorText set synchronously.
            sut.EnterCommand.Execute(null);

            sut.ErrorText.Should().Be("Incorrect password.");
        }

        [Fact]
        public void EnterCommand_WrongPassword_DoesNotUnlock()
        {
            var sut = CreateSut();
            sut.PasswordInput = "wrong";

            sut.EnterCommand.Execute(null);

            sut.IsUnlocked.Should().BeFalse();
        }

        [Fact]
        public void EnterCommand_WrongPassword_DoesNotSetIsRunning()
        {
            var sut = CreateSut();
            sut.PasswordInput = "wrong";

            sut.EnterCommand.Execute(null);

            sut.IsRunning.Should().BeFalse();
        }

        [Fact]
        public void EnterCommand_EmptyPassword_SetsErrorText()
        {
            var sut = CreateSut();
            sut.PasswordInput = "";

            sut.EnterCommand.Execute(null);

            sut.ErrorText.Should().Be("Incorrect password.");
        }

        [Fact]
        public void EnterCommand_ClearsErrorTextFirst()
        {
            var sut = CreateSut();
            sut.PasswordInput = "bad";
            sut.EnterCommand.Execute(null); // sets error text

            // Provide a different wrong password — error text should be cleared then re-set
            sut.PasswordInput = "also-bad";
            sut.EnterCommand.Execute(null);

            sut.ErrorText.Should().Be("Incorrect password.");
        }

        [Fact]
        public void Stop_WhenNotRunning_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.StopCommand.Execute(null);
            act.Should().NotThrow();
        }

        [Fact]
        public void Stop_WhenNotRunning_IsRunningRemainsUnchanged()
        {
            var sut = CreateSut();
            sut.StopCommand.Execute(null);
            sut.IsRunning.Should().BeFalse();
        }

        [Fact]
        public void Cleanup_WhenNotRunning_DoesNotThrow()
        {
            var sut = CreateSut();
            var act = () => sut.Cleanup();
            act.Should().NotThrow();
        }

        [Fact]
        public void PasswordInput_CanBeSet()
        {
            var sut = CreateSut();
            sut.PasswordInput = "test";
            sut.PasswordInput.Should().Be("test");
        }

        [Fact]
        public void StopButtonVisible_DefaultIsFalse()
        {
            var sut = CreateSut();
            sut.StopButtonVisible.Should().BeFalse();
        }

        [Fact]
        public void EnterButtonContent_DefaultIsEnter()
        {
            var sut = CreateSut();
            sut.EnterButtonContent.Should().Be("Enter");
        }
    }
}
