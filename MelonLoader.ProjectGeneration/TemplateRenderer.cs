using System.Collections.Generic;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>Provides replacement values for hosts that render token-based templates.</summary>
    public sealed class TemplateRenderer
    {
        private readonly ProjectGenerator generator = new();

        public Dictionary<string, string> CreateReplacements(GameInfo game, string author,
            IEnumerable<AssemblyReference> references = null, bool includeRequiredReferences = true,
            bool deployOnBuild = true, bool copyAssemblies = false)
            => generator.CreateReplacements(game, author, references, includeRequiredReferences, deployOnBuild, copyAssemblies);

        public Dictionary<string, string> CreateDualRuntimeReplacements(RuntimeTargetOptions mono,
            RuntimeTargetOptions il2Cpp, string author, ProjectKind kind = ProjectKind.Mod,
            string projectName = null, string rootNamespace = null)
            => generator.CreateDualRuntimeReplacements(mono, il2Cpp, author, kind, projectName, rootNamespace);
    }
}
