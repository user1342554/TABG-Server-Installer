using System;

namespace TabgInstaller.Core
{
    public interface IServerPathProvider
    {
        string ServerPath { get; }
        void SetPath(string path);
        event Action? PathChanged;
    }

    public class ServerPathProvider : IServerPathProvider
    {
        public string ServerPath { get; private set; } = "";
        public event Action? PathChanged;

        public void SetPath(string path)
        {
            ServerPath = path;
            PathChanged?.Invoke();
        }
    }
}
