using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security;
using System.Linq;

namespace MelonLoader.ProjectGeneration
{
    public sealed class ProjectGenerator
    {
        public Dictionary<string, string> CreateReplacements(GameInfo game, string author,
            IEnumerable<AssemblyReference> references = null, bool includeRequiredReferences = true)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (author == null) throw new ArgumentNullException(nameof(author));
            string framework = GetFramework(game);
            return new Dictionary<string, string>
            {
                ["$GAME_DIR$"] = SecurityElement.Escape(game.Path),
                ["$GAME_DEV$"] = CSharpLiteral(game.GameDeveloper),
                ["$GAME_NAME$"] = CSharpLiteral(game.GameName),
                ["$FRAMEWORK_VER$"] = framework,
                ["$AUTHOR$"] = EscapeCSharp(author),
                ["$PROJ_REFERENCES$"] = GenerateReferences(game, framework, references, includeRequiredReferences),
                ["$INIT_METHOD_NAME$"] = game.MelonVersion >= new Version(0, 5, 5) ? "OnInitializeMelon" : "OnApplicationStart",
                ["$IMPLICIT_USINGS$"] = framework == "35" ? "disable" : "enable"
            };
        }

        // Returns file contents without writing files or depending on a host UI.
        public IReadOnlyDictionary<string, string> Generate(ProjectOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ProjectName))
                throw new ArgumentException("A project name is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.RootNamespace))
                throw new ArgumentException("A root namespace is required.", nameof(options));
            if (!Enum.IsDefined(typeof(ProjectKind), options.Kind))
                throw new ArgumentException("Unknown project kind.", nameof(options));

            Dictionary<string, string> replacements = CreateReplacements(options.Game, options.Author, options.References, options.IncludeRequiredReferences);
            Dictionary<string, string> files = new();
            string kind = options.Kind.ToString();
            foreach (string name in new[] { "ProjectTemplate.csproj", "Core.cs", "Directory.Build.props" })
            {
                using Stream stream = typeof(ProjectGenerator).Assembly.GetManifestResourceStream("Templates." + kind + "." + name);
                using StreamReader reader = new(stream);
                string content = reader.ReadToEnd();
                foreach (KeyValuePair<string, string> replacement in replacements)
                    content = content.Replace(replacement.Key, replacement.Value);
                bool source = name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
                content = content.Replace("$safeprojectname$", source ? options.RootNamespace : SecurityElement.Escape(options.RootNamespace));
                content = content.Replace("$projectname$", source ? EscapeCSharp(options.ProjectName) : SecurityElement.Escape(options.ProjectName));
                files.Add(name == "ProjectTemplate.csproj" ? options.ProjectName + ".csproj" : name, content);
            }
            return files;
        }

        private static string CSharpLiteral(string value) => value == null ? "null" : "\"" + EscapeCSharp(value) + "\"";
        private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        public string GetFramework(GameInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            string framework = "6.0";
            if (!info.IsMelon6Plus && info.IsIl2Cpp)
                framework = "472";

            if (!info.IsIl2Cpp)
            {
                if (info.EngineVersion >= new AssetRipper.Primitives.UnityVersion(2021, 2, 0))
                    framework = "standard2.1";
                else if (info.EngineVersion >= new AssetRipper.Primitives.UnityVersion(2018, 1, 0))
                    framework = "472";
                else if (info.EngineVersion >= new AssetRipper.Primitives.UnityVersion(2017, 1, 0))
                    framework = "35"; // possible for it to be 472, but this is a safer bet
                else
                    framework = "35";
            }

            return framework;
        }

        /// <summary>Lists selectable assemblies and expected loader dependencies, including missing required files.</summary>
        public IReadOnlyList<AssemblyReference> DiscoverReferences(GameInfo info)
            => DiscoverReferences(info, GetFramework(info));

        private IReadOnlyList<AssemblyReference> DiscoverReferences(GameInfo info, string framework)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            string il2cppDllDir = info.IsMelon6Plus ? Path.Combine(info.Path, "MelonLoader", "Il2CppAssemblies") : Path.Combine(info.Path, "MelonLoader", "Managed");
            string dllDir = info.IsIl2Cpp ? il2cppDllDir : Path.Combine(info.DataPath, "Managed");

            if (info.IsIl2Cpp && (!Directory.Exists(il2cppDllDir) || !File.Exists(Path.Combine(info.Path, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Config.cfg"))))
            {
                throw new InvalidOperationException("Game has no generated assemblies. Please run it once with MelonLoader installed before creating a project.");
            }

            List<AssemblyReference> references = new();
            foreach (string path in Directory.GetFiles(dllDir, "*.dll").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                AssemblyCategory category = ClassifyAssembly(name);
                references.Add(new AssemblyReference(path, category, isRecommended: category != AssemblyCategory.Framework));
            }
            List<string> files = new();
            if (info.MelonVersion <= new Version(0, 5, 3))
                files.Add(Path.Combine(info.Path, "MelonLoader", "MelonLoader.dll"));
            else if (info.MelonVersion <= new Version(0, 5, 7))
            {
                files.Add(Path.Combine(info.Path, "MelonLoader", "MelonLoader.dll"));
                files.Add(Path.Combine(info.Path, "MelonLoader", "0Harmony.dll"));
            }
            else // ML 0.6+
            {
                files.Add(Path.Combine(info.Path, "MelonLoader", info.IsIl2Cpp ? "net6" : "net35", "MelonLoader.dll"));
                files.Add(Path.Combine(info.Path, "MelonLoader", info.IsIl2Cpp ? "net6" : "net35", "0Harmony.dll"));

                if (info.IsIl2Cpp)
                {
                    files.Add(Path.Combine(info.Path, "MelonLoader", "net6", "Il2CppInterop.Runtime.dll"));
                    files.Add(Path.Combine(info.Path, "MelonLoader", "net6", "Il2CppInterop.Common.dll"));
                }
            }

            // this doesn't seem to be needed on all net35 mods for some reason, but at least on LiS:BtS it was, and it didn't seem to affect others so may as well add it
            if (framework == "35")
            {
                if (info.IsMelon6Plus)
                    files.Add(Path.Combine(info.Path, "MelonLoader", "net35", "ValueTupleBridge.dll"));
                else
                    files.Add(Path.Combine(info.Path, "MelonLoader", "ValueTupleBridge.dll"));
            }

            foreach (string file in files)
                references.Add(new AssemblyReference(file, ClassifyAssembly(Path.GetFileNameWithoutExtension(file)), isRequired: true));
            return references.GroupBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(reference => reference.IsRequired).First()).ToList().AsReadOnly();
        }

        /// <summary>Null selection uses recommendations; an empty selection includes only required references by default.</summary>
        public IReadOnlyList<AssemblyReference> ResolveReferences(GameInfo info,
            IEnumerable<AssemblyReference> selectedReferences = null, bool includeRequiredReferences = true)
            => ResolveReferences(info, selectedReferences, includeRequiredReferences, GetFramework(info));

        private IReadOnlyList<AssemblyReference> ResolveReferences(GameInfo info,
            IEnumerable<AssemblyReference> selectedReferences, bool includeRequiredReferences, string framework)
        {
            IReadOnlyList<AssemblyReference> discovered = DiscoverReferences(info, framework);
            List<AssemblyReference> selected = selectedReferences == null
                ? discovered.Where(reference => reference.IsRecommended).ToList()
                : selectedReferences.ToList();
            if (selected.Any(reference => reference == null))
                throw new ArgumentException("Reference selections cannot contain null entries.", nameof(selectedReferences));
            if (includeRequiredReferences)
                selected.AddRange(discovered.Where(reference => reference.IsRequired));
            return selected.GroupBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(reference => reference.IsRequired).First()).ToList().AsReadOnly();
        }

        /// <summary>Checks file availability and conflicting names. Does not verify API compatibility or transitive dependencies.</summary>
        public IReadOnlyList<ReferenceDiagnostic> ValidateReferences(IEnumerable<AssemblyReference> references)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            List<AssemblyReference> selection = references.ToList();
            if (selection.Any(reference => reference == null))
                throw new ArgumentException("Reference selections cannot contain null entries.", nameof(references));
            List<ReferenceDiagnostic> diagnostics = new();
            foreach (AssemblyReference reference in selection)
            {
                if (!reference.Exists)
                    diagnostics.Add(new ReferenceDiagnostic(ReferenceDiagnosticCode.MissingFile, reference,
                        "Assembly file does not exist: " + reference.Path));
            }
            foreach (IGrouping<string, AssemblyReference> group in selection.GroupBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Select(reference => reference.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                    diagnostics.Add(new ReferenceDiagnostic(ReferenceDiagnosticCode.ConflictingAssemblyName, group.First(),
                        "Multiple files have the assembly name '" + group.Key + "'. Select only one."));
            }
            return diagnostics.AsReadOnly();
        }

        public string GenerateReferences(GameInfo info, string framework,
            IEnumerable<AssemblyReference> references = null, bool includeRequiredReferences = true)
        {
            IReadOnlyList<AssemblyReference> resolved = ResolveReferences(info, references, includeRequiredReferences, framework);
            IReadOnlyList<ReferenceDiagnostic> diagnostics = ValidateReferences(resolved);
            if (diagnostics.Count != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));
            StringBuilder builder = new();
            foreach (AssemblyReference reference in resolved)
            {
                string filePath = MakeRelativePath(info.Path, reference.Path);
                string hintPath = Path.IsPathRooted(filePath) ? filePath : "$(GamePath)/" + filePath;
                builder.AppendLine($"\t\t<Reference Include=\"{SecurityElement.Escape(reference.Name)}\">");
                builder.AppendLine($"\t\t\t<HintPath>{SecurityElement.Escape(hintPath)}</HintPath>");
                builder.AppendLine("\t\t</Reference>");
            }
            return builder.ToString();
        }

        private static AssemblyCategory ClassifyAssembly(string name)
        {
            if (name == "MelonLoader") return AssemblyCategory.Loader;
            if (name == "0Harmony") return AssemblyCategory.Harmony;
            if (name.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Unhollower", StringComparison.OrdinalIgnoreCase))
                return AssemblyCategory.Interop;
            if (name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
                return AssemblyCategory.Unity;
            if (name == "mscorlib" || name == "netstandard" || name == "Mono.Security" || name == "ValueTupleBridge" || name.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                return AssemblyCategory.Framework;
            return AssemblyCategory.Game;
        }

        public static string MakeRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            // Normalize to absolute paths first
            fromPath = Path.GetFullPath(fromPath);
            toPath = Path.GetFullPath(toPath);

            // If fromPath is a file, use its directory
            if (File.Exists(fromPath))
                fromPath = Path.GetDirectoryName(fromPath);

            // Ensure directory path ends with separator so URI treats it as a directory
            if (!fromPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fromPath += Path.DirectorySeparatorChar;

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme)
                return toPath; // Can't make relative across schemes

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

            return relativePath;
        }
    }
}
