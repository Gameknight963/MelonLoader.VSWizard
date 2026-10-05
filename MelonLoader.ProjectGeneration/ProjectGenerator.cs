using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security;

namespace MelonLoader.ProjectGeneration
{
    public sealed class ProjectGenerator
    {
        public Dictionary<string, string> CreateReplacements(GameInfo game, string author)
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
                ["$PROJ_REFERENCES$"] = GenerateReferences(game, framework),
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

            Dictionary<string, string> replacements = CreateReplacements(options.Game, options.Author);
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

        public string GenerateReferences(GameInfo info, string framework)
        {
            string il2cppDllDir = info.IsMelon6Plus ? Path.Combine(info.Path, "MelonLoader", "Il2CppAssemblies") : Path.Combine(info.Path, "MelonLoader", "Managed");
            string dllDir = info.IsIl2Cpp ? il2cppDllDir : Path.Combine(info.DataPath, "Managed");

            if (info.IsIl2Cpp && (!Directory.Exists(il2cppDllDir) || !File.Exists(Path.Combine(info.Path, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Config.cfg"))))
            {
                throw new InvalidOperationException("Game has no generated assemblies. Please run it once with MelonLoader installed before creating a project.");
            }

            StringBuilder referencesBuilder = new();
            List<string> files = [.. Directory.GetFiles(dllDir, "*.dll")];
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
            {
                if (IsBlacklistedReference(Path.GetFileName(file)))
                    continue;

                string filePath = MakeRelativePath(info.Path, file);

                referencesBuilder.AppendLine($"\t\t<Reference Include=\"{SecurityElement.Escape(Path.GetFileNameWithoutExtension(filePath))}\">");
                referencesBuilder.AppendLine($"\t\t\t<HintPath>$(GamePath)/{SecurityElement.Escape(filePath)}</HintPath>");
                referencesBuilder.AppendLine($"\t\t</Reference>");
            }

            return referencesBuilder.ToString();
        }

        private bool IsBlacklistedReference(string fileName)
        {
            if (fileName == "mscorlib.dll" || fileName == "netstandard.dll" || fileName == "Mono.Security.dll")
                return true;

            if (fileName.StartsWith("System"))
                return true;

            return false;
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
