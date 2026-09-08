using System;
using System.Collections.Generic;
using System.Linq;
using Import_SHP.Formats;

namespace Import_SHP.Import
{
    /// <summary>The information that the option prompts need before the import starts.</summary>
    public sealed class ShapefileSummary
    {
        private ShapefileSummary(string shapePath, ShapefileHeader header, IReadOnlyList<DbfField> fields)
        {
            ShapePath = shapePath;
            Header = header;
            Fields = fields;
        }

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

            return new ShapefileSummary(shapePath, reader.Header, fields);
        }
    }
}
