using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Import_SHP.Gdal
{
    /// <summary>
    /// The paths of the GDAL command line tools that the plugin runs. The plugin needs GDAL only
    /// for the coordinate reference system: the translation and the detection of the EPSG code.
    /// </summary>
    public sealed class GdalTools
    {
        /// <summary>The folders that hold GDAL in a usual install. Searched after the PATH.</summary>
        private static readonly string[] KnownFolders =
        {
            "/run/current-system/sw/bin",   // Nix
            "/opt/homebrew/bin",            // Homebrew on Apple silicon
            "/usr/local/bin",               // Homebrew on Intel
            "/opt/local/bin",               // MacPorts
            "/Library/Frameworks/GDAL.framework/Programs",
            "/usr/bin",
            @"C:\OSGeo4W\bin",
            @"C:\Program Files\GDAL",
        };

        private GdalTools(string gdalTransformPath, string? gdalSrsInfoPath)
        {
            GdalTransformPath = gdalTransformPath;
            GdalSrsInfoPath = gdalSrsInfoPath;
        }

        /// <summary>The tool that translates coordinates to another CRS.</summary>
        public string GdalTransformPath { get; }

        /// <summary>The tool that finds the EPSG code of a CRS, or null when the folder has none.</summary>
        public string? GdalSrsInfoPath { get; }

        /// <summary>Finds the GDAL tools. The search order is the PATH, then the usual install folders.</summary>
        /// <exception cref="GdalNotFoundException">No folder holds gdaltransform.</exception>
        public static GdalTools Find()
        {
            foreach (var folder in SearchFolders())
            {
                var gdalTransformPath = Path.Combine(folder, ExecutableName("gdaltransform"));
                if (!File.Exists(gdalTransformPath))
                    continue;

                var gdalSrsInfoPath = Path.Combine(folder, ExecutableName("gdalsrsinfo"));
                return new GdalTools(gdalTransformPath, File.Exists(gdalSrsInfoPath) ? gdalSrsInfoPath : null);
            }

            throw new GdalNotFoundException(
                "The plugin did not find the GDAL tool \"gdaltransform\", which translates the "
                + "coordinates to the target CRS. Install GDAL, for example with \"brew install gdal\".");
        }

        private static IEnumerable<string> SearchFolders()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVariable))
            {
                foreach (var folder in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(folder))
                        yield return folder;
                }
            }

            foreach (var folder in KnownFolders)
                yield return folder;
        }

        private static string ExecutableName(string name)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? name + ".exe" : name;
        }
    }
}
