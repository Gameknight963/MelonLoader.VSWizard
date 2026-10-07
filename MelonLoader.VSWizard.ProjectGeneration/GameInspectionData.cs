using System;

namespace MelonLoader.VSWizard.ProjectGeneration
{
    internal sealed class GameInspectionData
    {
        // Data
        public string GameDirectory { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string DataDirectory { get; set; } = "";
        public Version LoaderVersion { get; set; } = new();
        public bool IsIl2Cpp { get; set; } = false;

        public string GameName { get; set; } = null;
        public string GameDeveloper { get; set; } = null;
        public UnityVersion UnityVersion { get; set; } = UnityVersion.Unknown;
        internal GameInfo ToGameInfo() => new()
        {
            GameDirectory = GameDirectory,
            ExecutablePath = ExecutablePath,
            DataDirectory = DataDirectory,
            LoaderVersion = LoaderVersion,
            IsIl2Cpp = IsIl2Cpp,
            GameName = GameName,
            GameDeveloper = GameDeveloper,
            UnityVersion = UnityVersion
        };
    }
}
