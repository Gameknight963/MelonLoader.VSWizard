using System.Collections.Generic;

namespace MelonLoader.ProjectGeneration
{
    public enum ProjectKind { Mod, Plugin }

    public sealed class ProjectOptions
    {
        public string ProjectName { get; set; }
        public string RootNamespace { get; set; }
        public string Author { get; set; }
        public ProjectKind Kind { get; set; }
        public GameInfo Game { get; set; }

        // Null selects recommended references; an empty list selects no optional references.
        public IReadOnlyList<AssemblyReference> References { get; set; }
        public bool IncludeRequiredReferences { get; set; } = true;
    }
}
