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

            return text.ToString();
        }
    }
}
