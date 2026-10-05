using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MelonLoader.ProjectGeneration.Tests
{
    public sealed class AssemblyCopyTests
    {
        [Theory]
        [InlineData(false, ProjectKind.Mod)]
        [InlineData(false, ProjectKind.Plugin)]
        [InlineData(true, ProjectKind.Mod)]
        [InlineData(true, ProjectKind.Plugin)]
        public void CopiesSelectedReferencesAndKeepsProjectsPortable(bool il2Cpp, ProjectKind kind)
        {
            string root = Path.Combine(Path.GetTempPath(), "MelonCopy-" + Guid.NewGuid());
            string source = Path.Combine(root, "source");
            string output = Path.Combine(root, "project");
            string managed = il2Cpp ? Path.Combine(source, "MelonLoader", "Il2CppAssemblies") : Path.Combine(source, "Game_Data", "Managed");
            string loader = Path.Combine(source, "MelonLoader", il2Cpp ? "net6" : "net35");
            Directory.CreateDirectory(managed);
            Directory.CreateDirectory(loader);
            try
            {
                foreach (string name in new[] { "MelonLoader", "0Harmony", "ValueTupleBridge", "Il2CppInterop.Runtime", "Il2CppInterop.Common" })
                    File.WriteAllText(Path.Combine(loader, name + ".dll"), "fixture " + name);
                File.WriteAllText(Path.Combine(managed, "UnityEngine.CoreModule.dll"), "unity fixture");
                File.WriteAllText(Path.Combine(managed, "Unused.dll"), "excluded fixture");
                string customDirectory = Path.Combine(root, "custom & libraries");
                Directory.CreateDirectory(customDirectory);
                string custom = Path.Combine(customDirectory, "Custom.dll");
                File.WriteAllText(custom, "custom fixture");
                if (il2Cpp)
                {
                    string generatorDirectory = Path.Combine(source, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
                    Directory.CreateDirectory(generatorDirectory);
                    File.WriteAllText(Path.Combine(generatorDirectory, "Config.cfg"), "fixture");
                }
                GameInfo game = new()
                {
                    Path = source,
                    DataPath = Path.Combine(source, "Game_Data"),
                    IsIl2Cpp = il2Cpp,
                    MelonVersion = new Version(0, 6, 0)
                };
                ProjectGenerator generator = new();
                ProjectOptions options = new()
                {
                    ProjectName = "Example",
                    RootNamespace = "Example",
                    Author = "Me",
                    Game = game,
                    Kind = kind,
                    DeployOnBuild = false,
                    CopyAssemblies = true,
                    References = new[]
                    {
                        new AssemblyReference(Path.Combine(managed, "UnityEngine.CoreModule.dll")),
                        new AssemblyReference(custom)
                    }
                };
                ProjectGenerationResult result = generator.GeneratePlan(options);
                Assert.False(Directory.Exists(output));
                Assert.Single(result.Warnings);
                Assert.DoesNotContain(result.AssemblyCopies, copy => copy.SourcePath.EndsWith("Unused.dll"));
                Assert.Contains(result.AssemblyCopies, copy => copy.SourcePath.EndsWith("MelonLoader.dll"));
                Assert.Equal(il2Cpp, result.AssemblyCopies.Any(copy => copy.SourcePath.EndsWith("Il2CppInterop.Runtime.dll")));
                Assert.Equal(generator.ResolveReferences(game, options.References).Count, result.AssemblyCopies.Count);

                // A missing source is reported before any assembly is copied.
                File.Delete(custom);
                Assert.Throws<FileNotFoundException>(() => result.CopyAssembliesTo(output));
                Assert.False(Directory.Exists(output));
                File.WriteAllText(custom, "custom fixture");
                result.CopyAssembliesTo(output);
                foreach (AssemblyCopy copy in result.AssemblyCopies)
                    Assert.Equal(File.ReadAllBytes(copy.SourcePath), File.ReadAllBytes(Path.Combine(output, copy.DestinationRelativePath)));
                Assert.Throws<IOException>(() => result.CopyAssembliesTo(output));
                File.WriteAllText(custom, "updated fixture");
                result.CopyAssembliesTo(output, overwrite: true);
                AssemblyCopy customCopy = result.AssemblyCopies.Single(copy => copy.SourcePath == custom);
                Assert.Equal("updated fixture", File.ReadAllText(Path.Combine(output, customCopy.DestinationRelativePath)));
                foreach (KeyValuePair<string, string> file in result.Files)
                    File.WriteAllText(Path.Combine(output, file.Key), file.Value);

                // The generated project still resolves its compiler inputs after both relocation and removal of the installation.
                string moved = Path.Combine(root, "moved project");
                Directory.Move(output, moved);
                Directory.Delete(source, true);
                Directory.Delete(customDirectory, true);
                XDocument props = XDocument.Load(Path.Combine(moved, "Directory.Build.props"));
                Dictionary<string, string> properties = new()
                {
                    ["MSBuildThisFileDirectory"] = moved + Path.DirectorySeparatorChar
                };
                foreach (XElement property in props.Root.Element("PropertyGroup").Elements())
                {
                    string value = property.Value;
                    foreach (KeyValuePair<string, string> existing in properties)
                        value = value.Replace("$(" + existing.Key + ")", existing.Value);
                    properties.Add(property.Name.LocalName, value);
                }
                XDocument project = XDocument.Load(Path.Combine(moved, "Example.csproj"));
                foreach (XElement reference in project.Descendants("Reference"))
                {
                    string hint = reference.Element("HintPath").Value;
                    foreach (KeyValuePair<string, string> property in properties)
                        hint = hint.Replace("$(" + property.Key + ")", property.Value);
                    Assert.True(File.Exists(hint), hint);
                    Assert.Equal("false", reference.Element("Private").Value);
                }
                Assert.Equal("false", properties["DeployOnBuild"]);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
