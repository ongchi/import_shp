using System;
using System.Globalization;
using Import_SHP.Formats;
using Rhino;
using Rhino.Geometry;

namespace Import_SHP.Import
{
    /// <summary>
    /// Handles the translation that moves projected coordinates near the world origin.
    /// Rhino loses accuracy when geometry sits far away from the origin, and coordinates such as
    /// UTM eastings are millions of units large.
    /// </summary>
    public static class OriginOffset
    {
        /// <summary>The document user text key that holds the offset of the first import.</summary>
        public const string DocumentUserTextKey = "Import_SHP.Offset";

        /// <summary>The distance from the origin above which the plugin proposes an offset.</summary>
        public const double FarFromOriginDistance = 100000.0;

        private const double RoundingStep = 1000.0;

        /// <summary>Reads the offset that an earlier import wrote into the document.</summary>
        public static bool TryReadFromDocument(RhinoDoc doc, out Vector3d offset)
        {
            offset = Vector3d.Zero;
            if (doc is null)
                return false;

            var text = doc.Strings.GetValue(DocumentUserTextKey);
            return TryParse(text, out offset);
        }

        public static void WriteToDocument(RhinoDoc doc, Vector3d offset)
        {
            if (doc is null)
                return;

            var text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}",
                offset.X,
                offset.Y,
                offset.Z);
            doc.Strings.SetString(DocumentUserTextKey, text);
        }

        public static bool TryParse(string? text, out Vector3d offset)
        {
            offset = Vector3d.Zero;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var parts = text.Split(',');
            if (parts.Length != 3)
                return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;

            offset = new Vector3d(x, y, z);
            return true;
        }

        /// <summary>True when the center of the data lies further from the origin than the threshold.</summary>
        public static bool IsFarFromOrigin(ShapeBounds bounds)
        {
            if (bounds.IsEmpty)
                return false;

            return Math.Abs(bounds.CenterX) > FarFromOriginDistance
                || Math.Abs(bounds.CenterY) > FarFromOriginDistance;
        }

        /// <summary>Proposes the translation that moves the center of the data near the origin.</summary>
        public static Vector3d Suggest(ShapeBounds bounds)
        {
            if (bounds.IsEmpty)
                return Vector3d.Zero;

            return new Vector3d(
                -RoundToStep(bounds.CenterX),
                -RoundToStep(bounds.CenterY),
                0.0);
        }

        private static double RoundToStep(double value)
        {
            return Math.Round(value / RoundingStep, MidpointRounding.AwayFromZero) * RoundingStep;
        }
    }
}
