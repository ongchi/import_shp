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
