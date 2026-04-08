namespace TabgInstaller.Gui.Services
{
    public interface IToastService
    {
        void Success(string message);
        void Error(string message);
        void Warning(string message);
        void Info(string message);
    }
}
