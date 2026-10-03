using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Import_SHP.Import
{
    /// <summary>Counts and messages collected during one import operation.</summary>
    public sealed class ImportReport
    {
        private readonly List<string> _warnings = new();

        public int PointCount { get; set; }

        public int CurveCount { get; set; }

        public int RecordCount { get; set; }

        public int SkippedNullCount { get; set; }

        public int SkippedUnsupportedCount { get; set; }

        public int SkippedDeletedCount { get; set; }

        /// <summary>The records with a vertex that GDAL could not translate to the target CRS.</summary>
        public int SkippedUntranslatedCount { get; set; }

        /// <summary>The CRS that the import translated from. Empty when no translation ran.</summary>
        public string SourceCrs { get; set; } = string.Empty;

        /// <summary>The CRS that the import translated to. Empty when no translation ran.</summary>
        public string TargetCrs { get; set; } = string.Empty;

        /// <summary>The factor from the source unit to the model unit of the document.</summary>
        public double ModelScale { get; set; } = 1.0;

        /// <summary>The factor from the source unit to the layout unit of the document.</summary>
        public double LayoutScale { get; set; } = 1.0;

        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>Adds a warning. Each message text is kept one time only.</summary>
        public void AddWarning(string message)
        {
            if (!_warnings.Contains(message))
                _warnings.Add(message);
        }

        public string ToSummary(string fileName)
        {
            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"{fileName}: {RecordCount} records, ");
            text.Append(CultureInfo.InvariantCulture, $"{PointCount} points, {CurveCount} curves.");

            if (SkippedNullCount > 0)
                text.Append(CultureInfo.InvariantCulture, $" {SkippedNullCount} null records skipped.");
            if (SkippedUnsupportedCount > 0)
                text.Append(CultureInfo.InvariantCulture, $" {SkippedUnsupportedCount} unsupported records skipped.");
            if (SkippedDeletedCount > 0)
                text.Append(CultureInfo.InvariantCulture, $" {SkippedDeletedCount} deleted records skipped.");
            if (SkippedUntranslatedCount > 0)
                text.Append(CultureInfo.InvariantCulture, $" {SkippedUntranslatedCount} records outside the target CRS skipped.");

            return text.ToString();
        }

        /// <summary>The CRS line, or null when no translation ran.</summary>
        public string? ToCrsText()
        {
            return TargetCrs.Length == 0
                ? null
                : $"CRS: the import translated the coordinates from {SourceCrs} to {TargetCrs}.";
        }

        /// <summary>
        /// The unit line, or null when both units leave the coordinates as they are.
        /// </summary>
        public string? ToUnitText()
        {
            if (ModelScale == 1.0 && LayoutScale == 1.0)
                return null;

            var text = new StringBuilder();
            text.Append(CultureInfo.InvariantCulture, $"Model units: the import scaled the coordinates by {ModelScale:R}.");

            if (LayoutScale != 1.0)
                text.Append(CultureInfo.InvariantCulture, $" Layout units: the layout factor is {LayoutScale:R}, and no layout received data.");

            return text.ToString();
        }
    }
}
