using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using MelonLoader.ProjectGeneration;

using Xunit;

namespace MelonLoader.ProjectGeneration.Tests
{
    public sealed class ProjectGenerationTests
    {
        [Fact]
        public void ReportsConflictingCustomReferencesAndMissingFiles()
        {
            ProjectGenerator generator = new();
            AssemblyReference first = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Custom.dll"));
            AssemblyReference second = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Custom.dll"));
            IReadOnlyList<ReferenceDiagnostic> diagnostics = generator.ValidateReferences(new[] { first, second });
            Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Code == ReferenceDiagnosticCode.MissingFile));
            Assert.Single(diagnostics.Where(diagnostic => diagnostic.Code == ReferenceDiagnosticCode.ConflictingAssemblyName));
        }

        [Theory]
        [InlineData(false, ProjectKind.Mod)]
        [InlineData(false, ProjectKind.Plugin)]
        [InlineData(true, ProjectKind.Mod)]
        [InlineData(true, ProjectKind.Plugin)]
        public void GeneratesProjectsFromInspectedInstallations(bool il2Cpp, ProjectKind kind)
        {
            string root = Path.Combine(Path.GetTempPath(), "MelonWizard-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                {
                    string gamePath = Path.Combine(root, il2Cpp ? "IL2CPP & Game" : "Mono & Game");
                    string data = Path.Combine(gamePath, "Game_Data");
                    string managed = il2Cpp ? Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies") : Path.Combine(data, "Managed");
                    Directory.CreateDirectory(managed);
                    Directory.CreateDirectory(data);
                    File.WriteAllText(Path.Combine(data, "app.info"), "Developer\nGame Name");
                    File.WriteAllText(Path.Combine(managed, "UnityEngine.CoreModule.dll"), "fixture");
                    File.WriteAllText(Path.Combine(managed, "mscorlib.dll"), "fixture");
                    string loader = Path.Combine(gamePath, "MelonLoader", il2Cpp ? "net6" : "net35");
                    Directory.CreateDirectory(loader);
                    File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(loader, "MelonLoader.dll"));
                    File.WriteAllText(Path.Combine(loader, "0Harmony.dll"), "fixture");
                    File.WriteAllText(Path.Combine(loader, "ValueTupleBridge.dll"), "fixture");
                    File.WriteAllText(Path.Combine(loader, "Il2CppInterop.Runtime.dll"), "fixture");
                    File.WriteAllText(Path.Combine(loader, "Il2CppInterop.Common.dll"), "fixture");
                    if (il2Cpp)
                    {
                        string metadata = Path.Combine(data, "il2cpp_data", "Metadata");
                        Directory.CreateDirectory(metadata);
                        File.WriteAllText(Path.Combine(metadata, "global-metadata.dat"), "fixture");
                        string generatorDirectory = Path.Combine(gamePath, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator");
                        Directory.CreateDirectory(generatorDirectory);
                        File.WriteAllText(Path.Combine(generatorDirectory, "Config.cfg"), "fixture");
                    }
                    GameInspector inspector = new();
                    GameInfo game = inspector.Inspect(Path.Combine(gamePath, "Game.exe"));
                    Assert.Equal(il2Cpp, game.IsIl2Cpp);
                    Assert.Equal("Game Name", game.GameName);
                    ProjectGenerator generator = new();
                    IReadOnlyList<AssemblyReference> catalog = generator.DiscoverReferences(game);
                    Assert.Contains(catalog, reference => reference.Name == "mscorlib" && !reference.IsRecommended);
                    Assert.Contains(catalog, reference => reference.Name == "UnityEngine.CoreModule" && reference.Category == AssemblyCategory.Unity);
                    IReadOnlyList<AssemblyReference> requiredOnly = generator.ResolveReferences(game, Array.Empty<AssemblyReference>());
                    Assert.All(requiredOnly, reference => Assert.True(reference.IsRequired));
                    Assert.Contains(requiredOnly, reference => reference.Name == "MelonLoader");
                    Assert.Equal(il2Cpp, requiredOnly.Any(reference => reference.Name == "Il2CppInterop.Runtime"));
                    Assert.Empty(generator.ResolveReferences(game, Array.Empty<AssemblyReference>(), includeRequiredReferences: false));
                    {
                        AssemblyReference unity = catalog.Single(reference => reference.Name == "UnityEngine.CoreModule");
                        string selectedXml = generator.GenerateReferences(game, generator.GetFramework(game), new[] { unity });
                        Assert.Contains("UnityEngine.CoreModule", selectedXml);
                        Assert.DoesNotContain("Include=\"mscorlib\"", selectedXml);
                        Assert.Single(generator.ResolveReferences(game, new[] { unity, unity }, false));
                        IReadOnlyDictionary<string, string> files = generator.Generate(new ProjectOptions
                        {
                            ProjectName = "Example",
                            RootNamespace = "Example",
                            Author = "Author\"Name",
                            Kind = kind,
                            Game = game
                        });
                        XDocument project = XDocument.Parse(files["Example.csproj"]);
                        XDocument props = XDocument.Parse(files["Directory.Build.props"]);
                        Assert.Equal(gamePath, props.Root.Element("PropertyGroup").Element("GamePath").Value);
                        XElement settings = props.Root.Element("PropertyGroup");
                        Assert.StartsWith("$(GamePath)/", settings.Element("GameAssembliesPath").Value);
                        Assert.StartsWith("$(GamePath)/", settings.Element("LoaderAssembliesPath").Value);
                        Assert.Equal("true", settings.Element("DeployOnBuild").Value);
                        Assert.DoesNotContain("$(GamePath)", string.Join("", project.Descendants("HintPath").Select(element => element.Value)));
                        Dictionary<string, string> evaluated = EvaluateProperties(props);
                        foreach (XElement reference in project.Descendants("Reference"))
                        {
                            string hint = ExpandProperties(reference.Element("HintPath").Value, evaluated);
                            AssemblyReference source = catalog.Single(candidate => candidate.Name == reference.Attribute("Include").Value);
                            Assert.Equal(source.Path, Path.GetFullPath(hint));
                        }
                        // Moving the installation only requires changing the shared GamePath.
                        string movedGame = Path.Combine(root, "Moved Game");
                        settings.Element("GamePath").Value = movedGame;
                        Dictionary<string, string> relocated = EvaluateProperties(props);
                        Assert.Equal(Path.GetFullPath(Path.Combine(movedGame, ProjectGenerator.MakeRelativePath(gamePath, managed))),
                            Path.GetFullPath(relocated["GameAssembliesPath"]));
                        Assert.Equal(Path.GetFullPath(Path.Combine(movedGame, ProjectGenerator.MakeRelativePath(gamePath, loader))),
                            Path.GetFullPath(relocated["LoaderAssembliesPath"]));

                        string customDirectory = Path.Combine(root, "Custom & Libraries");
                        Directory.CreateDirectory(customDirectory);
                        string customFile = Path.Combine(customDirectory, "Custom.dll");
                        File.WriteAllText(customFile, "fixture");
                        IReadOnlyDictionary<string, string> custom = generator.Generate(new ProjectOptions
                        {
                            ProjectName = "CustomExample",
                            RootNamespace = "CustomExample",
                            Author = "Me",
                            Kind = kind,
                            Game = game,
                            References = new[] { new AssemblyReference(customFile) },
                            DeployOnBuild = false
                        });
                        XDocument customProps = XDocument.Parse(custom["Directory.Build.props"]);
                        XElement customSettings = customProps.Root.Element("PropertyGroup");
                        Assert.Equal("false", customSettings.Element("DeployOnBuild").Value);
                        customSettings.Element("GamePath").Value = movedGame;
                        Dictionary<string, string> customEvaluated = EvaluateProperties(customProps);
                        XDocument customProject = XDocument.Parse(custom["CustomExample.csproj"]);
                        XElement customReference = customProject.Descendants("Reference").Single(element => element.Attribute("Include").Value == "Custom");
                        Assert.Equal(customFile, Path.GetFullPath(ExpandProperties(customReference.Element("HintPath").Value, customEvaluated)));
                        XElement deployment = customProject.Descendants("Target").Single(element => element.Attribute("Name").Value == "DeployOutput");
                        Assert.Equal("'$(DeployOnBuild)' == 'true'", deployment.Attribute("Condition").Value);
                        Assert.Equal("$(GamePath)/" + (kind == ProjectKind.Mod ? "Mods" : "Plugins"), deployment.Element("Copy").Attribute("DestinationFolder").Value);
                        Assert.DoesNotContain("Include=\"mscorlib\"", files["Example.csproj"]);
                        Assert.Contains(kind == ProjectKind.Mod ? "MelonMod" : "MelonPlugin", files["Core.cs"]);
                        Assert.Contains("Author\\\"Name", files["Core.cs"]);
                        IReadOnlyDictionary<string, string> minimal = generator.Generate(new ProjectOptions
                        {
                            ProjectName = "Minimal",
                            RootNamespace = "Minimal",
                            Author = "Me",
                            Game = game,
                            Kind = kind,
                            References = Array.Empty<AssemblyReference>()
                        });
                        Assert.DoesNotContain("UnityEngine.CoreModule", minimal["Minimal.csproj"]);
                        Assert.Contains("Include=\"MelonLoader\"", minimal["Minimal.csproj"]);
                        File.Delete(Path.Combine(loader, "0Harmony.dll"));
                        IReadOnlyList<ReferenceDiagnostic> missing = generator.ValidateReferences(generator.ResolveReferences(game));
                        Assert.Contains(missing, diagnostic => diagnostic.Code == ReferenceDiagnosticCode.MissingFile && diagnostic.Reference.Name == "0Harmony");
                        Assert.Throws<InvalidOperationException>(() => generator.GenerateReferences(game, generator.GetFramework(game)));
                        foreach (string content in files.Values)
                        {
                            Assert.DoesNotContain("$safeprojectname$", content);
                            Assert.DoesNotContain("$PROJ_REFERENCES$", content);
                        }
                    }
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
        private static Dictionary<string, string> EvaluateProperties(XDocument props)
        {
            Dictionary<string, string> values = new();
            foreach (XElement property in props.Root.Element("PropertyGroup").Elements())
                values.Add(property.Name.LocalName, ExpandProperties(property.Value, values));
            return values;
        }

        private static string ExpandProperties(string value, Dictionary<string, string> properties)
        {
            foreach (KeyValuePair<string, string> property in properties)
                value = value.Replace("$(" + property.Key + ")", property.Value);
            return value;
        }
    }
}
