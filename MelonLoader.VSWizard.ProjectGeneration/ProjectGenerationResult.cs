using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace MelonLoader.VSWizard.ProjectGeneration
{
    /// <summary>One planned assembly copy. Generation records paths without reading or copying file contents.</summary>
    public sealed class AssemblyCopy
    {
        internal AssemblyCopy(string sourcePath, string destinationRelativePath)
        {
            SourcePath = sourcePath;
            DestinationRelativePath = destinationRelativePath;
        }

        /// <summary>Gets the absolute assembly source path. The source is read when copies are executed.</summary>
        public string SourcePath { get; }
        /// <summary>Gets the destination relative to the generated project directory, using forward slashes.</summary>
        public string DestinationRelativePath { get; }
    }

    /// <summary>Generated text, optional assembly copy operations, and warnings. Creation has no filesystem write effects.</summary>
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

        /// <summary>Gets project-relative filenames and their complete text contents. The caller writes these files.</summary>
        public IReadOnlyDictionary<string, string> Files { get; }
        /// <summary>Gets compiler-reference copies to execute explicitly. Empty when copying is disabled.</summary>
        public IReadOnlyList<AssemblyCopy> AssemblyCopies { get; }
        /// <summary>Gets nonfatal generation warnings, including redistribution guidance when copies are planned.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Executes planned compiler-reference copies without writing the generated text files.</summary>
        /// <param name="projectDirectory">The destination project directory. Relative paths use the current working directory.</param>
        /// <param name="overwrite">Whether to replace existing destination files. Defaults to <see langword="false"/>.</param>
        /// <remarks>Missing sources and conflicting destinations are checked before copying. Source and destination paths that are identical are skipped. Directories are created as needed. Filesystem failures during copying can leave a partial snapshot.</remarks>
        /// <exception cref="ArgumentException">The project directory is blank or invalid.</exception>
        /// <exception cref="InvalidOperationException">A destination resolves outside the project directory.</exception>
        /// <exception cref="FileNotFoundException">A planned assembly source is missing.</exception>
        /// <exception cref="IOException">A destination exists and overwrite is <see langword="false"/>, a destination is a directory, or copying fails.</exception>
        /// <exception cref="UnauthorizedAccessException">The filesystem denies access.</exception>
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
