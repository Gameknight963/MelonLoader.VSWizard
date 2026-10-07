using System.Collections.Generic;

namespace MelonLoader.VSWizard.ProjectGeneration
{
    /// <summary>Provides replacement values for hosts that render token-based templates.</summary>
    public sealed class TemplateRenderer
    {
        private readonly ProjectGenerator generator = new();

        /// <summary>Builds token replacements for a single-runtime template without writing files or copying assemblies.</summary>
        /// <param name="game">The installation metadata.</param>
        /// <param name="author">The author inserted into source, escaped as C# string content. Must not be <see langword="null"/>.</param>
        /// <param name="references">Selected references. <see langword="null"/> selects recommendations; an empty sequence selects no optional references.</param>
        /// <param name="includeRequiredReferences">Whether required loader references are included. Defaults to <see langword="true"/>.</param>
        /// <param name="deployOnBuild">The deployment default written into generated props. Defaults to <see langword="true"/>.</param>
        /// <param name="copyAssemblies">Whether reference paths should point to a local snapshot. Does not create a copy plan or copy files.</param>
        /// <returns>Replacement values keyed by dollar-delimited template tokens.</returns>
        /// <exception cref="System.ArgumentNullException">The game or author is <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">The reference selection contains <see langword="null"/>.</exception>
        /// <exception cref="System.InvalidOperationException">References are missing or conflicting, or generated IL2CPP assemblies are unavailable.</exception>
        /// <remarks>Values are escaped for the relevant XML or C# template context. For assembly copying, obtain the copy plan through <see cref="ProjectGenerator.Generate(ProjectOptions)"/>. Filesystem exceptions from discovery can propagate.</remarks>
        public Dictionary<string, string> CreateReplacements(GameInfo game, string author,
            IEnumerable<AssemblyReference> references = null, bool includeRequiredReferences = true,
            bool deployOnBuild = true, bool copyAssemblies = false)
            => generator.CreateReplacements(game, author, references, includeRequiredReferences, deployOnBuild, copyAssemblies);

        /// <summary>Builds token replacements for a shared Mono and IL2CPP template.</summary>
        /// <param name="mono">The required Mono installation and its reference/deployment settings.</param>
        /// <param name="il2Cpp">The required IL2CPP installation and its reference/deployment settings.</param>
        /// <param name="author">The source author. Must not be <see langword="null"/>.</param>
        /// <param name="kind">The entry type. Defaults to <see cref="ProjectKind.Mod"/>.</param>
        /// <param name="projectName">The display name to escape for source. <see langword="null"/> leaves the host's project-name token unresolved.</param>
        /// <param name="rootNamespace">The valid C# namespace for source. <see langword="null"/> leaves the host's safe-project-name token unresolved.</param>
        /// <returns>Runtime-prefixed settings and shared source replacements keyed by dollar-delimited tokens.</returns>
        /// <exception cref="System.ArgumentNullException">The author is <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">A target lacks game metadata, the targets have the wrong runtimes, the kind is invalid, or a selection contains <see langword="null"/>.</exception>
        /// <exception cref="System.InvalidOperationException">References are missing or conflicting, or generated IL2CPP assemblies are unavailable.</exception>
        /// <remarks>Supply <paramref name="projectName"/> and <paramref name="rootNamespace"/> for hosts that perform a single substitution pass. Copy settings change generated paths without executing copies; use <see cref="ProjectGenerator.Generate(DualRuntimeOptions)"/> to obtain copy operations. Filesystem exceptions from discovery can propagate.</remarks>
        public Dictionary<string, string> CreateDualRuntimeReplacements(RuntimeTargetOptions mono,
            RuntimeTargetOptions il2Cpp, string author, ProjectKind kind = ProjectKind.Mod,
            string projectName = null, string rootNamespace = null)
            => generator.CreateDualRuntimeReplacements(mono, il2Cpp, author, kind, projectName, rootNamespace);
    }
}
