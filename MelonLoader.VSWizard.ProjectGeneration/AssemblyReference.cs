using System;
using System.IO;

namespace MelonLoader.VSWizard.ProjectGeneration
{
    /// <summary>The role inferred from an assembly filename or supplied by the caller.</summary>
    public enum AssemblyCategory
    {
        /// <summary>A game assembly or an unrecognized custom assembly.</summary>
        Game,
        /// <summary>A Unity engine assembly.</summary>
        Unity,
        /// <summary>The MelonLoader assembly.</summary>
        Loader,
        /// <summary>The Harmony patching assembly.</summary>
        Harmony,
        /// <summary>An IL2CPP interoperability assembly.</summary>
        Interop,
        /// <summary>A .NET framework or compatibility assembly.</summary>
        Framework
    }

    /// <summary>An immutable compiler reference descriptor. Assembly metadata is not inspected.</summary>
    public sealed class AssemblyReference
    {
        /// <summary>Creates a reference descriptor and normalizes its path without requiring the file to exist.</summary>
        /// <param name="path">An assembly file path. Relative paths are resolved against the current working directory.</param>
        /// <param name="category">The assembly role. Defaults to <see cref="AssemblyCategory.Game"/>.</param>
        /// <param name="isRequired">Whether default resolution includes this reference regardless of optional selections.</param>
        /// <param name="isRecommended">Whether automatic selection includes this reference. Defaults to <see langword="true"/>.</param>
        /// <exception cref="ArgumentException">The path is blank or invalid.</exception>
        public AssemblyReference(string path, AssemblyCategory category = AssemblyCategory.Game,
            bool isRequired = false, bool isRecommended = true)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An assembly path is required.", nameof(path));
            Path = System.IO.Path.GetFullPath(path);
            Name = System.IO.Path.GetFileNameWithoutExtension(Path);
            Category = category;
            IsRequired = isRequired;
            IsRecommended = isRecommended;
        }

        /// <summary>Gets the filename stem used as the reference name.</summary>
        public string Name { get; }
        /// <summary>Gets the normalized absolute file path.</summary>
        public string Path { get; }
        /// <summary>Gets the role assigned to this assembly.</summary>
        public AssemblyCategory Category { get; }
        /// <summary>Gets whether reference resolution includes this assembly when required references are enabled.</summary>
        public bool IsRequired { get; }
        /// <summary>Gets whether this assembly is included when no explicit selection is supplied.</summary>
        public bool IsRecommended { get; }
        /// <summary>Gets whether the file currently exists. Evaluated on each access rather than cached.</summary>
        public bool Exists => File.Exists(Path);
    }

    /// <summary>The kind of reference validation failure.</summary>
    public enum ReferenceDiagnosticCode
    {
        /// <summary>The referenced file does not exist.</summary>
        MissingFile,
        /// <summary>Different file paths share the same filename stem, ignoring case.</summary>
        ConflictingAssemblyName
    }

    /// <summary>A missing-file or conflicting-filename diagnostic returned by reference validation.</summary>
    public sealed class ReferenceDiagnostic
    {
        internal ReferenceDiagnostic(ReferenceDiagnosticCode code, AssemblyReference reference, string message)
        {
            Code = code;
            Reference = reference;
            Message = message;
        }

        /// <summary>Gets the kind of validation failure.</summary>
        public ReferenceDiagnosticCode Code { get; }
        /// <summary>Gets the affected reference. For conflicts, this is the first reference in the conflicting group.</summary>
        public AssemblyReference Reference { get; }
        /// <summary>Gets a descriptive message suitable for displaying or logging.</summary>
        public string Message { get; }
    }
}
