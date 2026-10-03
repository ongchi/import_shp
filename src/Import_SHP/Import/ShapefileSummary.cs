using System;
using System.Collections.Generic;
using System.Linq;
using Import_SHP.Formats;
using Import_SHP.Gdal;

namespace Import_SHP.Import
{
    /// <summary>The information that the option prompts need before the import starts.</summary>
    public sealed class ShapefileSummary
    {
        private ShapefileSummary(
            string shapePath,
            ShapefileHeader header,
            IReadOnlyList<DbfField> fields,
            string? projectionPath,
            DetectedCrs detectedCrs)
        {
            ShapePath = shapePath;
            Header = header;
            Fields = fields;
            ProjectionPath = projectionPath;
            DetectedCrs = detectedCrs;
        }

        /// <summary>The path of the .prj file, or null when the shapefile has none.</summary>
        public string? ProjectionPath { get; }

        /// <summary>The CRS that the .prj file states.</summary>
        public DetectedCrs DetectedCrs { get; }

        public string ShapePath { get; }

        public ShapefileHeader Header { get; }

        /// <summary>The attribute fields. The list is empty when the shapefile has no .dbf file.</summary>
        public IReadOnlyList<DbfField> Fields { get; }

        /// <summary>True when the shape type of the file stores Z values.</summary>
        public bool HasShapeZ => ShapeRecord.TypeHasZ(Header.ShapeType);

        public IReadOnlyList<string> FieldNames => Fields.Select(field => field.Name).ToArray();

        public IReadOnlyList<string> NumericFieldNames =>
            Fields.Where(field => field.IsNumeric).Select(field => field.Name).ToArray();

        public static ShapefileSummary Read(string shapePath)
        {
            if (string.IsNullOrEmpty(shapePath))
                throw new ArgumentException("The shapefile path is empty.", nameof(shapePath));

            using var reader = ShapefileReader.Open(shapePath);
            var sidecars = SidecarFiles.Find(shapePath);

            IReadOnlyList<DbfField> fields = Array.Empty<DbfField>();
            if (sidecars.AttributePath is not null)
            {
                using var table = DbfTable.Open(sidecars.AttributePath, sidecars.ReadCodePage());
                fields = table.Fields;
            }

            return new ShapefileSummary(shapePath, reader.Header, fields, sidecars.ProjectionPath, DetectCrs(sidecars));
        }

        /// <summary>
        /// Reads the CRS of the .prj file. GDAL finds the EPSG code. A machine with no GDAL gives
        /// the CRS text only, which still names the CRS to the user.
        /// </summary>
        private static DetectedCrs DetectCrs(SidecarFiles sidecars)
        {
            var wkt = sidecars.ReadProjectionText();
            if (wkt is null || sidecars.ProjectionPath is null)
                return DetectedCrs.None;

            try
            {
                return new DetectedCrs(GdalSrsInfo.FindEpsgCode(GdalTools.Find(), sidecars.ProjectionPath), wkt);
            }
            catch (GdalNotFoundException)
            {
                return new DetectedCrs(null, wkt);
            }
        }
    }
}
