using System;
using System.IO;
using System.Text;

namespace Import_SHP.Formats
{
    /// <summary>Finds the files that belong to one shapefile: .dbf, .cpg and .prj.</summary>
    public sealed class SidecarFiles
    {
        private SidecarFiles(string shapePath, string? attributePath, string? codePagePath, string? projectionPath)
        {
            ShapePath = shapePath;
            AttributePath = attributePath;
            CodePagePath = codePagePath;
            ProjectionPath = projectionPath;
        }

        public string ShapePath { get; }

        /// <summary>The .dbf path, or null when the shapefile has no attribute table.</summary>
        public string? AttributePath { get; }

        public string? CodePagePath { get; }

        public string? ProjectionPath { get; }

        public static SidecarFiles Find(string shapePath)
        {
            if (string.IsNullOrEmpty(shapePath))
                throw new ArgumentException("The shapefile path is empty.", nameof(shapePath));

            return new SidecarFiles(
                shapePath,
                FindWithExtension(shapePath, ".dbf"),
                FindWithExtension(shapePath, ".cpg"),
                FindWithExtension(shapePath, ".prj"));
        }

        /// <summary>Reads the attribute encoding from the .cpg file. Falls back to the code page in the .dbf header.</summary>
        public Encoding? ReadCodePage()
        {
            if (CodePagePath is null)
                return null;

            try
            {
                var text = File.ReadAllText(CodePagePath).Trim();
                return text.Length == 0 ? null : TextEncodings.FromName(text);
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>Reads the coordinate system text of the .prj file, or null when the file is missing.</summary>
        public string? ReadProjectionText()
        {
            if (ProjectionPath is null)
                return null;

            try
            {
                var text = File.ReadAllText(ProjectionPath).Trim();
                return text.Length == 0 ? null : text;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>Looks for a companion file. The extension match ignores case on every file system.</summary>
        private static string? FindWithExtension(string shapePath, string extension)
        {
            var candidate = Path.ChangeExtension(shapePath, extension);
            if (File.Exists(candidate))
                return candidate;

            var directory = Path.GetDirectoryName(Path.GetFullPath(shapePath));
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

            var wanted = Path.GetFileNameWithoutExtension(shapePath);
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(path), wanted, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            return null;
        }
    }
}
