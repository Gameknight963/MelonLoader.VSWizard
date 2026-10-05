using System.Collections.Generic;

namespace MelonLoader.ProjectGeneration
{
    public sealed class RuntimeTargetOptions
    {
        public GameInfo Game { get; set; }
        public IReadOnlyList<AssemblyReference> References { get; set; }
        public bool IncludeRequiredReferences { get; set; } = true;
        public bool CopyAssemblies { get; set; }
        public bool DeployOnBuild { get; set; } = true;
    }

    public sealed class DualRuntimeOptions
    {
        public string ProjectName { get; set; }
        public string RootNamespace { get; set; }
        public string Author { get; set; }
        public ProjectKind Kind { get; set; } = ProjectKind.Mod;
        public RuntimeTargetOptions Mono { get; set; }
        public RuntimeTargetOptions Il2Cpp { get; set; }
    }
}
