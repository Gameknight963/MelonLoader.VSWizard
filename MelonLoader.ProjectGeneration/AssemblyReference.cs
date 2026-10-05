using System;
using System.IO;

namespace MelonLoader.ProjectGeneration
{
    public enum AssemblyCategory { Game, Unity, Loader, Harmony, Interop, Framework }

    public sealed class AssemblyReference
    {
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

        public string Name { get; }
        public string Path { get; }
        public AssemblyCategory Category { get; }
        public bool IsRequired { get; }
        public bool IsRecommended { get; }
        public bool Exists => File.Exists(Path);
    }

    public enum ReferenceDiagnosticCode { MissingFile, ConflictingAssemblyName }

    public sealed class ReferenceDiagnostic
    {
        internal ReferenceDiagnostic(ReferenceDiagnosticCode code, AssemblyReference reference, string message)
        {
            Code = code;
            Reference = reference;
            Message = message;
        }

        public ReferenceDiagnosticCode Code { get; }
        public AssemblyReference Reference { get; }
        public string Message { get; }
    }
}
