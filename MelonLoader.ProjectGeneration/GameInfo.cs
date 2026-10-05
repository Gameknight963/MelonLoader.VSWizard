using System;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>Immutable installation metadata returned by inspection or supplied by the caller.</summary>
    /// <remarks>Custom instances can be created with object initializers. Use a <see langword="with"/> expression to create a modified copy.
    /// </remarks>
    public sealed record GameInfo
    {
        private static readonly Version _ml6Version = new(0, 6, 0);

        // Data
        /// <summary>Gets the game installation directory. Inspection returns an absolute path.</summary>
        public string GameDirectory { get; init; } = "";
        /// <summary>Gets the selected game executable path. Inspection returns an absolute path.</summary>
        public string ExecutablePath { get; init; } = "";
        /// <summary>Gets the Unity data directory. Inspection returns an absolute path.</summary>
        public string DataDirectory { get; init; } = "";
        /// <summary>Gets the installed MelonLoader version used for API and reference selection.</summary>
        public Version LoaderVersion { get; init; } = new();
        internal bool IsLoader6Plus { get => LoaderVersion >= _ml6Version; }
        /// <summary>Gets whether this installation uses IL2CPP. <see langword="false"/> indicates Mono.</summary>
        public bool IsIl2Cpp { get; init; } = false;

        /// <summary>Gets the game name for the MelonGame attribute, or <see langword="null"/> when unavailable.</summary>
        public string GameName { get; init; } = null;
        /// <summary>Gets the developer for the MelonGame attribute, or <see langword="null"/> when unavailable.</summary>
        public string GameDeveloper { get; init; } = null;
        /// <summary>Gets the detected Unity version, or <see cref="UnityVersion.Unknown"/> when unavailable.</summary>
        public UnityVersion UnityVersion { get; init; } = UnityVersion.Unknown;
    }
}
