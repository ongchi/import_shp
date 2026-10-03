using System;
using System.Globalization;

namespace Import_SHP.Gdal
{
    /// <summary>Runs "gdalsrsinfo" to read facts about a coordinate reference system.</summary>
    public static class GdalSrsInfo
    {
        private const string EpsgPrefix = "EPSG:";

        /// <summary>
        /// Finds the EPSG code of a CRS definition, for example the path of a .prj file. Returns
        /// null when the tool is absent or when no code matches. The detection only fills a
        /// default value, so a failure of the tool is not an error of the import.
        /// </summary>
        public static string? FindEpsgCode(GdalTools tools, string definition)
        {
            return ParseEpsgCode(TryRun(tools, "-e", "-o", "epsg", definition) ?? string.Empty);
        }

        /// <summary>
        /// Reads the WKT of a CRS definition, for example "EPSG:25833". Returns null when the tool
        /// is absent or when it refuses the definition.
        /// </summary>
        public static string? ReadWkt(GdalTools tools, string definition)
        {
            var wkt = TryRun(tools, "-o", "wkt1", "--single-line", definition)?.Trim();
            return string.IsNullOrEmpty(wkt) ? null : wkt;
        }

        /// <summary>
        /// Reads the first code of the "gdalsrsinfo -e -o epsg" output. GDAL writes "EPSG:-1"
        /// when no code matches.
        /// </summary>
        public static string? ParseEpsgCode(string output)
        {
            foreach (var line in output.Split('\n'))
            {
                var text = line.Trim();
                if (!text.StartsWith(EpsgPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isNumber = int.TryParse(
                    text.Substring(EpsgPrefix.Length),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var code);

                return isNumber && code > 0
                    ? EpsgPrefix + code.ToString(CultureInfo.InvariantCulture)
                    : null;
            }

            return null;
        }

        private static string? TryRun(GdalTools tools, params string[] arguments)
        {
            if (tools.GdalSrsInfoPath is null)
                return null;

            try
            {
                return GdalProcess.Run(tools.GdalSrsInfoPath, arguments);
            }
            catch (GdalFailureException)
            {
                // gdalsrsinfo stops with an error for a definition that it cannot read.
                return null;
            }
        }
    }
}
