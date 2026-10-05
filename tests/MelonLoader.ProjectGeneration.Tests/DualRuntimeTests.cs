using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using AssetRipper.Primitives;
using Xunit;

namespace MelonLoader.ProjectGeneration.Tests
{
    public sealed class DualRuntimeTests
    {
        [Theory]
        [InlineData("Mono", "Debug", false)]
        [InlineData("Mono", "Release", false)]
        [InlineData("Il2Cpp", "Debug", false)]
        [InlineData("Il2Cpp", "Release", false)]
        [InlineData("Mono", "Debug", true)]
        [InlineData("Mono", "Release", true)]
        [InlineData("Il2Cpp", "Debug", true)]
        [InlineData("Il2Cpp", "Release", true)]
        public void EvaluatesRuntimeAndBuildSettings(string runtime, string platform, bool copy)
        {
            string root = Path.Combine(Path.GetTempPath(), "MelonDual-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                RuntimeTargetOptions mono = CreateTarget(root, false, copy);
                RuntimeTargetOptions il2Cpp = CreateTarget(root, true, copy);
                ProjectGenerator generator = new();
                DualRuntimeOptions options = new()
                {
                    ProjectName = "Example",
                    RootNamespace = "DifferentNamespace",
                    Author = "Me",
                    Mono = mono,
                    Il2Cpp = il2Cpp
                };
                ProjectGenerationResult result = generator.GenerateDualRuntimePlan(options);
                string projectDirectory = Path.Combine(root, "project");
                Directory.CreateDirectory(projectDirectory);
                foreach (KeyValuePair<string, string> file in result.Files)
                    File.WriteAllText(Path.Combine(projectDirectory, file.Key), file.Value);
                if (copy) result.CopyAssembliesTo(projectDirectory);
                Assert.Equal(copy, result.AssemblyCopies.Count > 0);
                Assert.Contains("DifferentNamespace.Core", result.Files["Core.cs"]);
                Assert.Equal("Example.csproj", XDocument.Parse(result.Files["Example.slnx"]).Root.Element("Project").Attribute("Path").Value);
                if (copy)
                {
                    Assert.Contains(result.AssemblyCopies, item => item.DestinationRelativePath.StartsWith("references/Mono/"));
                    Assert.Contains(result.AssemblyCopies, item => item.DestinationRelativePath.StartsWith("references/IL2CPP/"));
                    Directory.Delete(Path.Combine(root, "mono"), true);
                    Directory.Delete(Path.Combine(root, "il2cpp"), true);
                }
                string json = RunDotnet(projectDirectory, "msbuild", "Example.csproj", "-nologo", "-p:Configuration=" + runtime,
                    "-p:Platform=" + platform,
                    "-getProperty:TargetFramework,DefineConstants,Optimize,DebugType,PlatformTarget,GamePath,DeployOnBuild,MSBuildProjectExtensionsPath,OutputPath",
                    "-getItem:Reference");
                using JsonDocument evaluated = JsonDocument.Parse(json);
                JsonElement properties = evaluated.RootElement.GetProperty("Properties");
                Assert.Equal(runtime == "Mono" ? "netstandard2.1" : "net6.0", properties.GetProperty("TargetFramework").GetString());
                string[] symbols = properties.GetProperty("DefineConstants").GetString().Split(';');
                Assert.Contains(runtime == "Mono" ? "MONO" : "IL2CPP", symbols);
                Assert.DoesNotContain(runtime == "Mono" ? "IL2CPP" : "MONO", symbols);
                Assert.Equal(platform == "Debug", symbols.Contains("DEBUG"));
                Assert.Contains("TRACE", symbols);
                Assert.Equal(platform == "Release" ? "true" : "false", properties.GetProperty("Optimize").GetString());
                Assert.Equal("portable", properties.GetProperty("DebugType").GetString());
                Assert.Equal("AnyCPU", properties.GetProperty("PlatformTarget").GetString());
                Assert.Equal("false", properties.GetProperty("DeployOnBuild").GetString());
                Assert.Contains(runtime, properties.GetProperty("MSBuildProjectExtensionsPath").GetString());
                Assert.Contains(platform, properties.GetProperty("MSBuildProjectExtensionsPath").GetString());
                List<string> names = new();
                foreach (JsonElement reference in evaluated.RootElement.GetProperty("Items").GetProperty("Reference").EnumerateArray())
                {
                    names.Add(reference.GetProperty("Identity").GetString());
                    string path = reference.GetProperty("HintPath").GetString();
                    Assert.True(File.Exists(path), path);
                    if (copy) Assert.StartsWith(projectDirectory, Path.GetFullPath(path));
                }
                Assert.Contains(runtime == "Mono" ? "MonoGame" : "Il2CppGame", names);
                Assert.DoesNotContain(runtime == "Mono" ? "Il2CppGame" : "MonoGame", names);
                Assert.Equal(runtime == "Il2Cpp", names.Contains("Il2CppInterop.Runtime"));
                Assert.Contains("Example.csproj", RunDotnet(projectDirectory, "sln", "Example.slnx", "list"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void BuildsAllFourSolutionConfigurationsWithCompilerFixtures()
        {
            string root = Path.Combine(Path.GetTempPath(), "MelonDualBuild-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                string stubDirectory = Path.Combine(root, "stub");
                Directory.CreateDirectory(stubDirectory);
                File.WriteAllText(Path.Combine(stubDirectory, "Loader.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>netstandard2.0</TargetFramework><AssemblyName>MelonLoader</AssemblyName></PropertyGroup></Project>");
                File.WriteAllText(Path.Combine(stubDirectory, "Loader.cs"), @"
using System;
namespace MelonLoader
{
    public class MelonMod { public virtual void OnInitializeMelon() {} public virtual void OnApplicationStart() {} }
    public class MelonPlugin : MelonMod {}
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonInfoAttribute : Attribute
    { public MelonInfoAttribute(Type type, string name, string version, string author, string url) {} }
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class MelonGameAttribute : Attribute
    { public MelonGameAttribute(string developer, string name) {} }
}");
                RunDotnet(stubDirectory, "build", "Loader.csproj", "-o", "output", "--nologo");
                RuntimeTargetOptions mono = CreateTarget(root, false, false);
                RuntimeTargetOptions il2Cpp = CreateTarget(root, true, false);
                foreach (RuntimeTargetOptions target in new[] { mono, il2Cpp })
                {
                    string loaderPath = Path.Combine(target.Game.Path, "MelonLoader", target.Game.IsIl2Cpp ? "net6" : "net35", "MelonLoader.dll");
                    File.Copy(Path.Combine(stubDirectory, "output", "MelonLoader.dll"), loaderPath, true);
                    target.IncludeRequiredReferences = false;
                    target.References = new[] { new AssemblyReference(loaderPath) };
                }
                ProjectGenerator generator = new();
                ProjectGenerationResult result = generator.GenerateDualRuntimePlan(new DualRuntimeOptions
                {
                    ProjectName = "Example",
                    RootNamespace = "Example",
                    Author = "Me",
                    Mono = mono,
                    Il2Cpp = il2Cpp
                });
                string projectDirectory = Path.Combine(root, "project");
                Directory.CreateDirectory(projectDirectory);
                foreach (KeyValuePair<string, string> file in result.Files)
                    File.WriteAllText(Path.Combine(projectDirectory, file.Key), file.Value);
                foreach (string runtime in new[] { "Mono", "Il2Cpp" })
                {
                    foreach (string platform in new[] { "Debug", "Release" })
                    {
                        RunDotnet(projectDirectory, "build", "Example.slnx", "-c", runtime, "-p:Platform=" + platform, "--nologo");
                        string framework = runtime == "Mono" ? "netstandard2.1" : "net6.0";
                        Assert.True(File.Exists(Path.Combine(projectDirectory, "bin", runtime, platform, framework, "Example.dll")));
                    }
                }
            }
            finally { Directory.Delete(root, true); }
        }

        [Theory]
        [InlineData(ProjectKind.Mod)]
        [InlineData(ProjectKind.Plugin)]
        public void KeepsMetadataAndLoaderCompatibilitySeparate(ProjectKind kind)
        {
            string root = Path.Combine(Path.GetTempPath(), "MelonDualMetadata-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                RuntimeTargetOptions mono = CreateTarget(root, false, false);
                RuntimeTargetOptions il2Cpp = CreateTarget(root, true, false);
                mono.Game.MelonVersion = new Version(0, 5, 0);
                File.WriteAllText(Path.Combine(mono.Game.Path, "MelonLoader", "MelonLoader.dll"), "legacy fixture");
                mono.Game.GameName = "Mono Edition";
                il2Cpp.Game.GameName = "IL2CPP Edition";
                mono.DeployOnBuild = true;
                ProjectGenerator generator = new();
                ProjectGenerationResult result = generator.GenerateDualRuntimePlan(new DualRuntimeOptions
                {
                    ProjectName = "Example",
                    RootNamespace = "Example",
                    Author = "Me",
                    Kind = kind,
                    Mono = mono,
                    Il2Cpp = il2Cpp
                });
                string source = result.Files["Core.cs"];
                string monoSource = source.Split("#elif MONO")[1].Split("#elif IL2CPP")[0];
                string il2CppSource = source.Split("#elif IL2CPP")[1].Split("#else")[0];
                Assert.Contains("Mono Edition", monoSource);
                Assert.Contains("OnApplicationStart", monoSource);
                Assert.DoesNotContain("OnInitializeMelon", monoSource);
                Assert.Contains("IL2CPP Edition", il2CppSource);
                Assert.Contains("OnInitializeMelon", il2CppSource);
                Assert.Contains(kind == ProjectKind.Mod ? "MelonMod" : "MelonPlugin", source);
                XDocument props = XDocument.Parse(result.Files["Directory.Build.props"]);
                XElement[] groups = props.Root.Elements("PropertyGroup").ToArray();
                Assert.Equal("true", groups[0].Element("DeployOnBuild").Value);
                Assert.Equal("false", groups[1].Element("DeployOnBuild").Value);
                Assert.Contains("$(GamePath)/MelonLoader", groups[0].Element("LoaderAssembliesPath").Value);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void RejectsSwappedRuntimeSources()
        {
            ProjectGenerator generator = new();
            Assert.Throws<ArgumentException>(() => generator.CreateDualRuntimeReplacements(
                new RuntimeTargetOptions { Game = new GameInfo { IsIl2Cpp = true } },
                new RuntimeTargetOptions { Game = new GameInfo { IsIl2Cpp = false } }, "Me"));
        }

        private static RuntimeTargetOptions CreateTarget(string root, bool il2Cpp, bool copy)
        {
            string gamePath = Path.Combine(root, il2Cpp ? "il2cpp" : "mono");
            string data = Path.Combine(gamePath, "Game_Data");
            string managed = il2Cpp ? Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies") : Path.Combine(data, "Managed");
            string loader = Path.Combine(gamePath, "MelonLoader", il2Cpp ? "net6" : "net35");
            Directory.CreateDirectory(managed);
            Directory.CreateDirectory(loader);
            File.WriteAllText(Path.Combine(managed, il2Cpp ? "Il2CppGame.dll" : "MonoGame.dll"), "fixture");
            foreach (string name in new[] { "MelonLoader", "0Harmony", "Il2CppInterop.Runtime", "Il2CppInterop.Common" })
                File.WriteAllText(Path.Combine(loader, name + ".dll"), "fixture");
            if (il2Cpp)
            {
                string config = Path.Combine(gamePath, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
                Directory.CreateDirectory(config);
                File.WriteAllText(Path.Combine(config, "Config.cfg"), "fixture");
            }
            return new RuntimeTargetOptions
            {
                Game = new GameInfo
                {
                    Path = gamePath,
                    DataPath = data,
                    IsIl2Cpp = il2Cpp,
                    MelonVersion = new Version(0, 6, 0),
                    EngineVersion = new UnityVersion(2021, 2, 0)
                },
                CopyAssemblies = copy,
                DeployOnBuild = false
            };
        }

        private static string RunDotnet(string directory, params string[] arguments)
        {
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start);
            System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
            System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000))
            {
                process.Kill(true);
                throw new TimeoutException("dotnet did not finish within 60 seconds.");
            }
            Assert.True(process.ExitCode == 0, output.Result + error.Result);
            return output.Result;
        }
    }
}
