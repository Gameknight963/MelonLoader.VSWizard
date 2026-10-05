using System;
using System.IO;
using System.Diagnostics;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>Reads a Unity installation and its MelonLoader metadata without displaying UI.</summary>
    public sealed class GameInspector
    {
        /// <summary>Inspects the installation associated with a game executable.</summary>
        /// <param name="exe">The executable path. Relative paths are resolved against the current working directory.</param>
        /// <returns>An immutable snapshot of installation metadata. Unavailable game metadata may be <see langword="null"/> and the Unity version may be unknown.</returns>
        /// <remarks>Requires a Unity data folder and a readable MelonLoader installation at version 0.5 or newer.</remarks>
        /// <exception cref="InvalidOperationException">The path is blank, the installation lacks a Unity data folder or supported MelonLoader, or the loader DLL cannot be read.</exception>
        /// <exception cref="ArgumentException">The executable path is invalid.</exception>
        /// <exception cref="FormatException">The loader file version cannot be parsed.</exception>
        /// <exception cref="IOException">An installation file cannot be accessed.</exception>
        /// <exception cref="UnauthorizedAccessException">The filesystem denies access.</exception>
        public GameInfo Inspect(string exe)
        {
            GameInspectionData info = new();

            // likely won't occur, but may as well just in case
            if (string.IsNullOrWhiteSpace(exe))
            {
                throw new InvalidOperationException("Path does not contain an EXE.");
            }

            exe = Path.GetFullPath(exe);
            string dir = Path.GetDirectoryName(exe);

            info.GameDirectory = dir;

            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                throw new InvalidOperationException("Game path does not exist.");
            }

            string dataDir = Path.Combine(dir, Path.GetFileNameWithoutExtension(exe) + "_Data");
            if (!Directory.Exists(dataDir))
            {
                throw new InvalidOperationException("Path does not contain a Data folder. It might not be a Unity game.");
            }

            info.ExecutablePath = exe;
            info.DataDirectory = dataDir;

            info.IsIl2Cpp = File.Exists(Path.Combine(info.DataDirectory, "il2cpp_data", "Metadata", "global-metadata.dat"));

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

            info.LoaderVersion = Version.Parse(fvi.FileVersion);
            if (info.LoaderVersion < new Version(0, 5, 0))
            {
                throw new InvalidOperationException("The installed MelonLoader version is too old. This wizard only supports MelonLoader 0.5+.");
            }

            UnityDataParser.Run(info);
            return info.ToGameInfo();
        }

    }
}
