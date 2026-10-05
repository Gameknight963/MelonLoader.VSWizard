using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace MelonLoader.ProjectGeneration
{
    public sealed partial class ProjectGenerator
    {
        public Dictionary<string, string> CreateDualRuntimeReplacements(RuntimeTargetOptions mono,
            RuntimeTargetOptions il2Cpp, string author, ProjectKind kind = ProjectKind.Mod,
            string projectName = null, string rootNamespace = null)
        {
            if (mono?.Game == null) throw new ArgumentException("Supply a Mono target and its game information.", nameof(mono));
            if (il2Cpp?.Game == null) throw new ArgumentException("Supply an IL2CPP target and its game information.", nameof(il2Cpp));
            if (mono.Game.IsIl2Cpp) throw new ArgumentException("The Mono target must use Mono game information.", nameof(mono));
            if (!il2Cpp.Game.IsIl2Cpp) throw new ArgumentException("The IL2CPP target must use IL2CPP game information.", nameof(il2Cpp));
            if (!Enum.IsDefined(typeof(ProjectKind), kind)) throw new ArgumentException("Unknown project kind.", nameof(kind));
            Dictionary<string, string> replacements = new();
            foreach (KeyValuePair<string, RuntimeTargetOptions> target in new Dictionary<string, RuntimeTargetOptions>
                { ["MONO"] = mono, ["IL2CPP"] = il2Cpp })
            {
                RuntimeTargetOptions options = target.Value;
                Dictionary<string, string> targetReplacements = CreateReplacements(options.Game, author,
                    options.References, options.IncludeRequiredReferences, options.DeployOnBuild, options.CopyAssemblies);
                foreach (KeyValuePair<string, string> replacement in targetReplacements)
                    replacements.Add("$" + target.Key + "_" + replacement.Key.Trim('$') + "$", replacement.Value);
                string core = ReadTemplate(kind.ToString(), "Core.cs");
                foreach (KeyValuePair<string, string> replacement in targetReplacements)
                    core = core.Replace(replacement.Key, replacement.Value);
                // Visual Studio substitutes template tokens once; inserted source must already resolve host identity tokens.
                if (projectName != null)
                    core = core.Replace("$projectname$", EscapeCSharp(projectName));
                if (rootNamespace != null)
                    core = core.Replace("$safeprojectname$", rootNamespace);
                replacements.Add("$" + target.Key + "_CORE$", core);
            }
            replacements.Add("$DEPLOY_FOLDER$", kind == ProjectKind.Mod ? "Mods" : "Plugins");
            return replacements;
        }

        public ProjectGenerationResult GenerateDualRuntimePlan(DualRuntimeOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ProjectName) || options.ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Supply a project name suitable for a filename.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.RootNamespace))
                throw new ArgumentException("A root namespace is required.", nameof(options));
            Dictionary<string, string> replacements = CreateDualRuntimeReplacements(options.Mono, options.Il2Cpp, options.Author, options.Kind);
            Dictionary<string, string> files = new();
            foreach (string name in new[] { "ProjectTemplate.csproj", "Core.cs", "Directory.Build.props", "Solution.slnx" })
            {
                string content = ReadTemplate("DualRuntime", name);
                foreach (KeyValuePair<string, string> replacement in replacements)
                    content = content.Replace(replacement.Key, replacement.Value);
                content = content.Replace("$safeprojectname$", name == "Core.cs" ? options.RootNamespace : SecurityElement.Escape(options.RootNamespace));
                content = content.Replace("$projectname$", name == "Core.cs" ? EscapeCSharp(options.ProjectName) : SecurityElement.Escape(options.ProjectName));
                // The solution points to the project filename, independently of its namespace.
                if (name == "Solution.slnx")
                    content = ReadTemplate("DualRuntime", name).Replace("$safeprojectname$", SecurityElement.Escape(options.ProjectName));
                string filename = name == "ProjectTemplate.csproj" ? options.ProjectName + ".csproj"
                    : name == "Solution.slnx" ? options.ProjectName + ".slnx" : name;
                files.Add(filename, content);
            }
            List<AssemblyCopy> copies = new();
            foreach (RuntimeTargetOptions target in new[] { options.Mono, options.Il2Cpp })
            {
                if (!target.CopyAssemblies) continue;
                IReadOnlyList<AssemblyReference> resolved = ResolveReferences(target.Game, target.References, target.IncludeRequiredReferences);
                Dictionary<string, string> directories = GetReferenceDirectories(target.Game, resolved);
                copies.AddRange(resolved.Select(reference => new AssemblyCopy(reference.Path,
                    GetCopiedDirectory(target.Game, GetDirectoryProperty(reference, directories)) + "/" + Path.GetFileName(reference.Path))));
            }
            return new ProjectGenerationResult(files, copies);
        }

        private static string ReadTemplate(string kind, string name)
        {
            using Stream stream = typeof(ProjectGenerator).Assembly.GetManifestResourceStream("Templates." + kind + "." + name);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
    }
}
