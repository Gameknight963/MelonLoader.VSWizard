using System;
using System.IO;
using System.Diagnostics;

namespace MelonLoader.ProjectGeneration
{
    public sealed class GameInspector
    {
        public GameInfo Inspect(string exe)
        {
            GameInfo info = new();

            // likely won't occur, but may as well just in case
            if (string.IsNullOrWhiteSpace(exe))
            {
                throw new InvalidOperationException("Path does not contain an EXE.");
            }

            exe = Path.GetFullPath(exe);
            string dir = Path.GetDirectoryName(exe);

            info.Path = dir;

            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                throw new InvalidOperationException("Game path does not exist.");
            }

            string dataDir = Path.Combine(dir, Path.GetFileNameWithoutExtension(exe) + "_Data");
            if (!Directory.Exists(dataDir))
            {
                throw new InvalidOperationException("Path does not contain a Data folder. It may not be a Unity game.");
            }

            info.ExePath = exe;
            info.DataPath = dataDir;

            info.IsIl2Cpp = File.Exists(Path.Combine(info.DataPath, "il2cpp_data", "Metadata", "global-metadata.dat"));

            string loaderRoot = Path.Combine(dir, "MelonLoader");
            string melonPath = Path.Combine(loaderRoot, info.IsIl2Cpp ? "net6" : "net35", "MelonLoader.dll");
            if (!File.Exists(melonPath))
                melonPath = Path.Combine(loaderRoot, "MelonLoader.dll");
            if (!File.Exists(melonPath))
                throw new InvalidOperationException("Game does not have MelonLoader installed. Install and run MelonLoader once before using this wizard.");

            FileVersionInfo fvi = null;

            try
            {
                fvi = FileVersionInfo.GetVersionInfo(melonPath);
            }
            catch
            {
                throw new InvalidOperationException("Failed to read MelonLoader DLL. It may be corrupt.");
            }

            info.MelonVersion = Version.Parse(fvi.FileVersion);
            if (info.MelonVersion < new Version(0, 5, 0))
            {
                throw new InvalidOperationException("The installed MelonLoader version is too old. This wizard only supports MelonLoader 0.5+.");
            }

            UnityDataParser.Run(info);
            return info;
        }

    }
}
