using System.Collections.Generic;

namespace MelonLoader.ProjectGeneration
{
    /// <summary>The MelonLoader entry type to generate.</summary>
    public enum ProjectKind
    {
        /// <summary>A mod loaded from the game's Mods directory.</summary>
        Mod,
        /// <summary>A plugin loaded from the game's Plugins directory.</summary>
        Plugin
    }

    /// <summary>Options for a mod or plugin targeting one game installation.</summary>
    public sealed class ProjectOptions
    {
        /// <summary>Gets or sets the display name and output filename stem. A nonblank filename-compatible value is required.</summary>
        public string ProjectName { get; set; }
        /// <summary>Gets or sets the C# root namespace. The caller must supply a valid, nonblank namespace.</summary>
        public string RootNamespace { get; set; }
        /// <summary>Gets or sets the author for the generated MelonInfo attribute. Must not be <see langword="null"/>.</summary>
        public string Author { get; set; }
        /// <summary>Gets or sets whether to generate a mod or plugin. Defaults to <see cref="ProjectKind.Mod"/>.</summary>
        public ProjectKind Kind { get; set; }
        /// <summary>Gets or sets the installation metadata used to select frameworks, references, and deployment paths. Required.</summary>
        public GameInfo Game { get; set; }

        /// <summary>Gets or sets the selected assemblies. <see langword="null"/> uses recommended references; an empty list selects no optional references.</summary>
        public IReadOnlyList<AssemblyReference> References { get; set; }
        /// <summary>Gets or sets whether required loader references are added to the selection. Defaults to <see langword="true"/>.</summary>
        public bool IncludeRequiredReferences { get; set; } = true;
        /// <summary>Gets or sets the generated deployment default. Defaults to <see langword="true"/>; deployment requires an existing GamePath.</summary>
        public bool DeployOnBuild { get; set; } = true;
        /// <summary>Gets or sets whether to plan a project-local snapshot of selected references. Defaults to <see langword="false"/>; generation does not execute copies.</summary>
        public bool CopyAssemblies { get; set; }
    }
}
