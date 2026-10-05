using System;

namespace MelonLoader.ProjectGeneration
{
    public sealed record GameInfo
    {
        private static readonly Version _ml6Version = new(0, 6, 0);

        // Data
        public string GameDirectory { get; init; } = "";
        public string ExecutablePath { get; init; } = "";
        public string DataDirectory { get; init; } = "";
        public Version LoaderVersion { get; init; } = new();
        internal bool IsLoader6Plus { get => LoaderVersion >= _ml6Version; }
        public bool IsIl2Cpp { get; init; } = false;

        public string GameName { get; init; } = null;
        public string GameDeveloper { get; init; } = null;
        public UnityVersion UnityVersion { get; init; } = UnityVersion.Unknown;
    }
}
