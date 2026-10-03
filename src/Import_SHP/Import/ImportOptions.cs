using Rhino;
using Rhino.Geometry;

namespace Import_SHP.Import
{
    /// <summary>Where the elevation of each imported vertex comes from.</summary>
    public enum ZSource
    {
        /// <summary>Use the Z values of the shape. Shape types without Z import flat.</summary>
        ShapeZ,

        /// <summary>Use a numeric field of the attribute table.</summary>
        AttributeField,

        /// <summary>Use one constant elevation for every object.</summary>
        Constant,
    }

    /// <summary>The user settings of one import operation.</summary>
    public sealed class ImportOptions
    {
        public ZSource ZSource { get; set; } = ZSource.ShapeZ;

        /// <summary>The attribute field that holds the elevation. Used when <see cref="ZSource"/> is AttributeField.</summary>
        public string ZFieldName { get; set; } = string.Empty;

        /// <summary>The elevation used when <see cref="ZSource"/> is Constant.</summary>
        public double ConstantZ { get; set; }

        /// <summary>The attribute field that gives each object its name. Empty means no object name.</summary>
        public string NameFieldName { get; set; } = string.Empty;

        /// <summary>
        /// The coordinate reference system of the source. An empty text means the CRS that the
        /// .prj file states.
        /// </summary>
        public string SourceCrs { get; set; } = string.Empty;

        /// <summary>
        /// The coordinate reference system of the result. An empty text means no translation.
        /// </summary>
        public string TargetCrs { get; set; } = string.Empty;

        /// <summary>
        /// The EPSG code that the plugin detected in the .prj file. While <see cref="SourceCrs"/>
        /// holds this text, the import reads the CRS from the file itself, because the code is a
        /// match and not a proof.
        /// </summary>
        public string DetectedSourceCrs { get; set; } = string.Empty;

        /// <summary>True when the import translates the data to <see cref="TargetCrs"/>.</summary>
        public bool TranslatesCrs => !string.IsNullOrWhiteSpace(TargetCrs);

        /// <summary>
        /// The source CRS that replaces the CRS of the file, or null when the file states it.
        /// </summary>
        public string? SourceCrsOverride => OverrideFor(SourceCrs);

        /// <summary>The override that a source CRS text gives. See <see cref="SourceCrsOverride"/>.</summary>
        public string? OverrideFor(string? sourceCrs)
        {
            var text = sourceCrs?.Trim() ?? string.Empty;
            var isTheDetectedCode = string.Equals(text, DetectedSourceCrs.Trim(), System.StringComparison.OrdinalIgnoreCase);
            return text.Length == 0 || isTheDetectedCode ? null : text;
        }

        /// <summary>
        /// The unit of the source coordinates when the model space receives the data.
        /// The import scales the coordinates from this unit to the model unit of the document.
        /// </summary>
        public UnitSystem ModelUnits { get; set; } = UnitChoice.SameAsDocument;

        /// <summary>
        /// The unit of the source coordinates when a layout receives the data.
        /// The command imports into the model space only, so this unit scales nothing yet.
        /// </summary>
        public UnitSystem LayoutUnits { get; set; } = UnitChoice.SameAsDocument;

        /// <summary>True to move the data by <see cref="Offset"/>.</summary>
        public bool ApplyOffset { get; set; }

        /// <summary>The translation added to every coordinate.</summary>
        public Vector3d Offset { get; set; } = Vector3d.Zero;

        /// <summary>The layer that receives the imported objects.</summary>
        public string LayerName { get; set; } = string.Empty;

        /// <summary>True to group the parts of one multi part record.</summary>
        public bool GroupParts { get; set; } = true;

        /// <summary>The factor from <see cref="ModelUnits"/> to the model unit of the document.</summary>
        public double ModelScale(RhinoDoc doc) =>
            UnitChoice.ScaleTo(ModelUnits, doc?.ModelUnitSystem ?? UnitChoice.SameAsDocument);

        /// <summary>The factor from <see cref="LayoutUnits"/> to the layout unit of the document.</summary>
        public double LayoutScale(RhinoDoc doc) =>
            UnitChoice.ScaleTo(LayoutUnits, doc?.PageUnitSystem ?? UnitChoice.SameAsDocument);
    }
}
