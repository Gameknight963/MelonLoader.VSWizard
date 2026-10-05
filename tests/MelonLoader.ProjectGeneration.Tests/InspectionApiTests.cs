using System;
using System.IO;
using Xunit;

namespace MelonLoader.ProjectGeneration.Tests
{
    public sealed class InspectionApiTests
    {
        [Fact]
        public void PreservesUnityReleaseSuffixAndOrdering()
        {
            UnityVersion beta = UnityVersion.Parse("2021.2.0b1");
            UnityVersion release = UnityVersion.Parse("2021.2.0f1");
            Assert.Equal("2021.2.0b1", beta.ToString());
            Assert.Equal((ushort)2021, release.Major);
            Assert.Equal((ushort)2, release.Minor);
            Assert.Equal((ushort)0, release.Patch);
            Assert.True(beta < release);
            Assert.True(release < new UnityVersion(2022, 1, 0));
            Assert.True(release == UnityVersion.Parse("2021.2.0f1"));
            Assert.True(UnityVersion.Unknown < release);
        }

        [Fact]
        public void CustomGameMetadataCanBeCopiedWithoutMutatingTheOriginal()
        {
            GameInfo original = new()
            {
                GameDirectory = "game",
                DataDirectory = "game/Game_Data",
                LoaderVersion = new Version(0, 6, 0),
                UnityVersion = UnityVersion.Parse("2021.2.0f1")
            };
            GameInfo modified = original with { IsIl2Cpp = true, GameName = "Another Edition" };
            Assert.False(original.IsIl2Cpp);
            Assert.Null(original.GameName);
            Assert.True(modified.IsIl2Cpp);
            Assert.Equal(original.UnityVersion, modified.UnityVersion);
            Assert.Equal(original.GameDirectory, modified.GameDirectory);
        }

        [Fact]
        public void InspectionFailuresKeepActionableMessages()
        {
            GameInspector inspector = new();
            Assert.Equal("Path does not contain an EXE.",
                Assert.Throws<InvalidOperationException>(() => inspector.Inspect("")).Message);
            string root = Path.Combine(Path.GetTempPath(), "MelonInspect-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                string executable = Path.Combine(root, "Game.exe");
                Assert.Contains("Data folder", Assert.Throws<InvalidOperationException>(() => inspector.Inspect(executable)).Message);
                Directory.CreateDirectory(Path.Combine(root, "Game_Data"));
                Assert.Contains("Install and run MelonLoader once", Assert.Throws<InvalidOperationException>(() => inspector.Inspect(executable)).Message);
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
