using Rhino;
using Rhino.Geometry;

namespace Import_SHP.Import
{
    /// <summary>
    /// Turns a source coordinate into a document coordinate: first the unit scale, then the offset.
    /// The offset comes last because the document keeps it in document units, so that a later
    /// import of another file lands in the same place.
    /// </summary>
    public readonly struct PointTransform
    {
        public PointTransform(double scale, Vector3d offset)
        {
            Scale = scale;
            Offset = offset;
        }

        /// <summary>The factor from the source unit to the document unit.</summary>
        public double Scale { get; }

        /// <summary>The translation added after the scale, in document units.</summary>
        public Vector3d Offset { get; }

        /// <summary>Builds the transform of one import.</summary>
        public static PointTransform For(RhinoDoc doc, ImportOptions options)
        {
            return new PointTransform(
                options.ModelScale(doc),
                options.ApplyOffset ? options.Offset : Vector3d.Zero);
        }

        public Point3d Apply(double x, double y, double z)
        {
            return new Point3d(x * Scale + Offset.X, y * Scale + Offset.Y, z * Scale + Offset.Z);
        }
    }
}
