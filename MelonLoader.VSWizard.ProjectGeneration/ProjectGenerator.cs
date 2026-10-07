using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security;
using System.Linq;

namespace MelonLoader.VSWizard.ProjectGeneration
{
    /// <summary>Generates projects and resolves compiler references without UI or implicit filesystem writes.</summary>
    public sealed partial class ProjectGenerator
    {
        internal Dictionary<string, string> CreateReplacements(GameInfo game, string author,
            IEnumerable<AssemblyReference> references = null, bool includeRequiredReferences = true, bool deployOnBuild = true, bool copyAssemblies = false)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (author == null) throw new ArgumentNullException(nameof(author));
            string framework = GetFramework(game);
            IReadOnlyList<AssemblyReference> resolved = ResolveReferences(game, references, includeRequiredReferences, framework);
            EnsureValidReferences(resolved);
            Dictionary<string, string> directories = GetReferenceDirectories(game, resolved);
            return new Dictionary<string, string>
            {
                ["$GAME_DIR$"] = SecurityElement.Escape(game.GameDirectory),
                ["$GAME_DEV$"] = CSharpLiteral(game.GameDeveloper),
                ["$GAME_NAME$"] = CSharpLiteral(game.GameName),
                ["$FRAMEWORK_VER$"] = framework,
                ["$AUTHOR$"] = EscapeCSharp(author),
                ["$PROJ_REFERENCES$"] = RenderReferences(resolved, directories, copyAssemblies),
                ["$REFERENCE_PATHS$"] = RenderReferenceDirectories(game, directories, copyAssemblies),
                ["$DEPLOY_ON_BUILD$"] = deployOnBuild ? "true" : "false",
                ["$INIT_METHOD_NAME$"] = game.LoaderVersion >= new Version(0, 5, 5) ? "OnInitializeMelon" : "OnApplicationStart",
                ["$IMPLICIT_USINGS$"] = framework == "35" ? "disable" : "enable"
            };
        }

        /// <summary>Generates a mod or plugin for one installation without writing files or executing copies.</summary>
        /// <param name="options">Project identity, installation, reference selection, and copy/deployment settings.</param>
        /// <returns>Generated file contents, optional assembly copy operations, and warnings.</returns>
        /// <exception cref="ArgumentNullException">Options, game metadata, or the author is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Required project identity is blank, the kind is invalid, or a reference selection contains <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">References are missing or conflict, or required generated IL2CPP assemblies are unavailable.</exception>
        /// <remarks>The caller supplies a valid C# namespace and filename-compatible project name. Discovery can also propagate filesystem exceptions. Use the result's <see cref="ProjectGenerationResult.Files"/> to write text and <see cref="ProjectGenerationResult.CopyAssembliesTo(string, bool)"/> to execute copies.</remarks>
        public ProjectGenerationResult Generate(ProjectOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ProjectName))
                throw new ArgumentException("A project name is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.RootNamespace))
                throw new ArgumentException("A root namespace is required.", nameof(options));
            if (!Enum.IsDefined(typeof(ProjectKind), options.Kind))
                throw new ArgumentException("Unknown project kind.", nameof(options));

            IReadOnlyList<AssemblyReference> resolved = ResolveReferences(options.Game, options.References, options.IncludeRequiredReferences);
            Dictionary<string, string> replacements = CreateReplacements(options.Game, options.Author, resolved, false, options.DeployOnBuild, options.CopyAssemblies);
            List<AssemblyCopy> copies = new();
            if (options.CopyAssemblies)
            {
                Dictionary<string, string> directories = GetReferenceDirectories(options.Game, resolved);
                foreach (AssemblyReference reference in resolved)
                {
                    string property = GetDirectoryProperty(reference, directories);
                    string destination = GetCopiedDirectory(options.Game, property) + "/" + Path.GetFileName(reference.Path);
                    copies.Add(new AssemblyCopy(reference.Path, destination));
                }
            }
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
            return new ProjectGenerationResult(files, copies);
        }

        private static string CSharpLiteral(string value) => value == null ? "null" : "\"" + EscapeCSharp(value) + "\"";
        private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        private string GetFramework(GameInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            string framework = "6.0";
            if (!info.IsLoader6Plus && info.IsIl2Cpp)
                framework = "472";

            if (!info.IsIl2Cpp)
            {
                if (info.UnityVersion >= new UnityVersion(2021, 2, 0))
                    framework = "standard2.1";
                else if (info.UnityVersion >= new UnityVersion(2018, 1, 0))
                    framework = "472";
                else if (info.UnityVersion >= new UnityVersion(2017, 1, 0))
                    framework = "35"; // possible for it to be 472, but this is a safer bet
                else
                    framework = "35";
            }

            return framework;
        }

        /// <summary>Lists game assemblies and expected loader references, including required loader files that are missing.</summary>
        /// <param name="info">The installation metadata to inspect.</param>
        /// <returns>A path-deduplicated list with categories, recommendation flags, and required-reference flags.</returns>
        /// <exception cref="ArgumentNullException">Installation metadata is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">An IL2CPP installation lacks its generated assembly directory or generator configuration.</exception>
        /// <remarks>Enumerates DLLs on disk without reading their assembly metadata. Framework assemblies are not recommended automatically. Filesystem exceptions can propagate.</remarks>
        public IReadOnlyList<AssemblyReference> DiscoverReferences(GameInfo info)
            => DiscoverReferences(info, GetFramework(info));

        private IReadOnlyList<AssemblyReference> DiscoverReferences(GameInfo info, string framework)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            string il2cppDllDir = info.IsLoader6Plus ? Path.Combine(info.GameDirectory, "MelonLoader", "Il2CppAssemblies") : Path.Combine(info.GameDirectory, "MelonLoader", "Managed");
            string dllDir = info.IsIl2Cpp ? il2cppDllDir : Path.Combine(info.DataDirectory, "Managed");

            if (info.IsIl2Cpp && (!Directory.Exists(il2cppDllDir) || !File.Exists(Path.Combine(info.GameDirectory, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Config.cfg"))))
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
            if (info.LoaderVersion <= new Version(0, 5, 3))
                files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "MelonLoader.dll"));
            else if (info.LoaderVersion <= new Version(0, 5, 7))
            {
                files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "MelonLoader.dll"));
                files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "0Harmony.dll"));
            }
            else // ML 0.6+
            {
                files.Add(Path.Combine(info.GameDirectory, "MelonLoader", info.IsIl2Cpp ? "net6" : "net35", "MelonLoader.dll"));
                files.Add(Path.Combine(info.GameDirectory, "MelonLoader", info.IsIl2Cpp ? "net6" : "net35", "0Harmony.dll"));

                if (info.IsIl2Cpp)
                {
                    files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "net6", "Il2CppInterop.Runtime.dll"));
                    files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "net6", "Il2CppInterop.Common.dll"));
                }
            }

            // this doesn't seem to be needed on all net35 mods for some reason, but at least on LiS:BtS it was, and it didn't seem to affect others so may as well add it
            if (framework == "35")
            {
                if (info.IsLoader6Plus)
                    files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "net35", "ValueTupleBridge.dll"));
                else
                    files.Add(Path.Combine(info.GameDirectory, "MelonLoader", "ValueTupleBridge.dll"));
            }

            foreach (string file in files)
                references.Add(new AssemblyReference(file, ClassifyAssembly(Path.GetFileNameWithoutExtension(file)), isRequired: true));
            return references.GroupBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(reference => reference.IsRequired).First()).ToList().AsReadOnly();
        }

        /// <summary>Resolves an optional selection and adds required loader references when enabled.</summary>
        /// <param name="info">The installation metadata used for discovery and required references.</param>
        /// <param name="selectedReferences">Selected references. <see langword="null"/> uses recommendations; an empty sequence selects no optional references.</param>
        /// <param name="includeRequiredReferences">Whether to add required loader references. Defaults to <see langword="true"/>.</param>
        /// <returns>References deduplicated by absolute path, ignoring case. Required descriptors take precedence.</returns>
        /// <exception cref="ArgumentNullException">Installation metadata is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The selection contains <see langword="null"/> entries.</exception>
        /// <exception cref="InvalidOperationException">Required generated IL2CPP assemblies are unavailable.</exception>
        /// <remarks>Discovery still runs for custom selections. Missing files and conflicting names are reported separately by <see cref="ProjectGenerator.ValidateReferences(System.Collections.Generic.IEnumerable{AssemblyReference})"/>.</remarks>
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

        /// <summary>Checks current file availability and conflicts between filename stems, ignoring case.</summary>
        /// <param name="references">The resolved or custom references to validate.</param>
        /// <returns>Missing-file and conflicting-name diagnostics, or an empty list when these checks pass.</returns>
        /// <exception cref="ArgumentNullException">The sequence is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The sequence contains <see langword="null"/> entries.</exception>
        /// <remarks>Does not inspect assembly metadata, resolve transitive dependencies, or verify API compatibility.</remarks>
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

        private void EnsureValidReferences(IReadOnlyList<AssemblyReference> references)
        {
            IReadOnlyList<ReferenceDiagnostic> diagnostics = ValidateReferences(references);
            if (diagnostics.Count != 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        private static Dictionary<string, string> GetReferenceDirectories(GameInfo game, IReadOnlyList<AssemblyReference> references)
        {
            string gameDirectory = game.IsIl2Cpp
                ? Path.Combine(game.GameDirectory, "MelonLoader", game.IsLoader6Plus ? "Il2CppAssemblies" : "Managed")
                : Path.Combine(game.DataDirectory, "Managed");
            string loaderDirectory = game.LoaderVersion <= new Version(0, 5, 7)
                ? Path.Combine(game.GameDirectory, "MelonLoader")
                : Path.Combine(game.GameDirectory, "MelonLoader", game.IsIl2Cpp ? "net6" : "net35");
            Dictionary<string, string> directories = new()
            {
                ["GameAssembliesPath"] = Path.GetFullPath(gameDirectory),
                ["LoaderAssembliesPath"] = Path.GetFullPath(loaderDirectory)
            };
            int additionalDirectory = 1;
            foreach (string directory in references.Select(reference => Path.GetDirectoryName(reference.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase))
            {
                if (!directories.Values.Contains(directory, StringComparer.OrdinalIgnoreCase))
                    directories.Add("ReferenceAssembliesPath" + additionalDirectory++, directory);
            }
            return directories;
        }

        private static string RenderReferenceDirectories(GameInfo game, Dictionary<string, string> directories, bool copyAssemblies = false)
        {
            StringBuilder builder = new();
            foreach (KeyValuePair<string, string> directory in directories)
            {
                string relative = MakeRelativePath(game.GameDirectory, directory.Value);
                // Only factor out GamePath for directories within that installation.
                string normalized = relative.Replace('\\', '/');
                bool withinGame = !Path.IsPathRooted(relative) && normalized != ".." && !normalized.StartsWith("../", StringComparison.Ordinal);
                string path = withinGame ? "$(GamePath)" + (normalized.Length == 0 ? "" : "/" + normalized.TrimEnd('/')) : directory.Value;
                if (copyAssemblies)
                    path = "$(MSBuildThisFileDirectory)" + GetCopiedDirectory(game, directory.Key);
                builder.AppendLine($"    <{directory.Key}>{SecurityElement.Escape(path)}</{directory.Key}>");
            }
            return builder.ToString().TrimEnd();
        }

        private static string RenderReferences(IReadOnlyList<AssemblyReference> references, Dictionary<string, string> directories, bool copyAssemblies = false)
        {
            StringBuilder builder = new();
            foreach (AssemblyReference reference in references)
            {
                string property = GetDirectoryProperty(reference, directories);
                string hintPath = "$(" + property + ")/" + Path.GetFileName(reference.Path);
                builder.AppendLine($"\t\t<Reference Include=\"{SecurityElement.Escape(reference.Name)}\">");
                builder.AppendLine($"\t\t\t<HintPath>{SecurityElement.Escape(hintPath)}</HintPath>");
                if (copyAssemblies)
                    builder.AppendLine("\t\t\t<Private>false</Private>");
                builder.AppendLine("\t\t</Reference>");
            }
            return builder.ToString();
        }

        private static string GetDirectoryProperty(AssemblyReference reference, Dictionary<string, string> directories)
            => directories.First(entry => string.Equals(entry.Value, Path.GetDirectoryName(reference.Path), StringComparison.OrdinalIgnoreCase)).Key;

        private static string GetCopiedDirectory(GameInfo game, string property)
        {
            string folder = property == "GameAssembliesPath" ? "game"
                : property == "LoaderAssembliesPath" ? "loader" : property;
            return "references/" + (game.IsIl2Cpp ? "IL2CPP" : "Mono") + "/" + folder;
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

        private static string MakeRelativePath(string fromPath, string toPath)
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
