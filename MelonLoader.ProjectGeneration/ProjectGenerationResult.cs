using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace MelonLoader.ProjectGeneration
{
    public sealed class AssemblyCopy
    {
        internal AssemblyCopy(string sourcePath, string destinationRelativePath)
        {
            SourcePath = sourcePath;
            DestinationRelativePath = destinationRelativePath;
        }

        public string SourcePath { get; }
        public string DestinationRelativePath { get; }
    }

    public sealed class ProjectGenerationResult
    {
        internal ProjectGenerationResult(Dictionary<string, string> files, List<AssemblyCopy> copies)
        {
            Files = new ReadOnlyDictionary<string, string>(files);
            AssemblyCopies = copies.AsReadOnly();
            Warnings = copies.Count == 0 ? Array.Empty<string>() : new[]
            {
                "Copied assemblies may have redistribution restrictions. Check their licenses before publishing them."
            };
        }

        public IReadOnlyDictionary<string, string> Files { get; }
        public IReadOnlyList<AssemblyCopy> AssemblyCopies { get; }
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Copies compiler references only. The caller writes Files separately. Existing files are preserved unless overwrite is true.</summary>
        public void CopyAssembliesTo(string projectDirectory, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("A project directory is required.", nameof(projectDirectory));
            string root = Path.GetFullPath(projectDirectory);
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Dictionary<AssemblyCopy, string> destinations = new();
            foreach (AssemblyCopy copy in AssemblyCopies)
            {
                string destination = Path.GetFullPath(Path.Combine(root, copy.DestinationRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Copy destination must remain inside the project directory.");
                if (!File.Exists(copy.SourcePath))
                    throw new FileNotFoundException("Assembly source is missing.", copy.SourcePath);
                if (string.Equals(destination, copy.SourcePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!overwrite && File.Exists(destination))
                    throw new IOException("An assembly already exists at: " + destination);
                if (Directory.Exists(destination))
                    throw new IOException("A directory exists at the assembly destination: " + destination);
                destinations.Add(copy, destination);
            }
            foreach (KeyValuePair<AssemblyCopy, string> destination in destinations)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination.Value));
                File.Copy(destination.Key.SourcePath, destination.Value, overwrite);
            }
        }
    }
}
