using System;

namespace Import_SHP.Gdal
{
    /// <summary>The coordinate reference system that the plugin found in a source file.</summary>
    public sealed class DetectedCrs
    {
        /// <summary>The result for a file that states no CRS.</summary>
        public static readonly DetectedCrs None = new(null, null);

        public DetectedCrs(string? epsgCode, string? wkt)
        {
            EpsgCode = string.IsNullOrWhiteSpace(epsgCode) ? null : epsgCode.Trim();
            Wkt = string.IsNullOrWhiteSpace(wkt) ? null : wkt.Trim();
        }

        /// <summary>The code that GDAL matched, for example "EPSG:32633", or null when no code matches.</summary>
        public string? EpsgCode { get; }

        /// <summary>The CRS text of the file, or null when the file states no CRS.</summary>
        public string? Wkt { get; }

        /// <summary>The CRS name in the text of the file, for example "WGS 84 / UTM zone 33N".</summary>
        public string? Name => ParseName(Wkt);

        /// <summary>True when the file states a CRS.</summary>
        public bool IsKnown => Wkt is not null;

        /// <summary>The text that names the CRS to the user: the code, else the name, else nothing.</summary>
        public string DisplayText => EpsgCode ?? Name ?? string.Empty;

        /// <summary>Reads the CRS name, which is the first quoted text of a WKT.</summary>
        public static string? ParseName(string? wkt)
        {
            if (string.IsNullOrWhiteSpace(wkt))
                return null;

            var start = wkt.IndexOf('"');
            if (start < 0)
                return null;

            var end = wkt.IndexOf('"', start + 1);
            if (end < 0)
                return null;

            var name = wkt.Substring(start + 1, end - start - 1).Trim();
            return name.Length == 0 ? null : name;
        }

        /// <summary>True when the WKT states a geographic CRS, which counts in degrees.</summary>
        public static bool IsGeographic(string? wkt)
        {
            return wkt is not null && wkt.TrimStart().StartsWith("GEOG", StringComparison.OrdinalIgnoreCase);
        }
    }
}
