using Import_SHP.Gdal;
using Import_SHP.UI;
using Rhino;

namespace Import_SHP.Import
{
    /// <summary>Builds the import options: first the defaults, then the user changes.</summary>
    public static class ImportOptionsResolver
    {
        /// <summary>
        /// Returns the options for one import, or null when the user cancels.
        /// The interactive mode shows the options dialog. The other modes use the defaults.
        /// </summary>
        public static ImportOptions? Resolve(RhinoDoc doc, string shapePath, bool interactive)
        {
            var summary = ShapefileSummary.Read(shapePath);
            var options = CreateDefaults(doc, summary);

            if (!interactive)
                return options;

            return ImportOptionsDialog.Show(doc, summary, options) ? options : null;
        }

        /// <summary>
        /// Builds the default options. The offset of an earlier import in the same document wins,
        /// so that every file lands in the same place.
        /// </summary>
        public static ImportOptions CreateDefaults(RhinoDoc doc, ShapefileSummary summary)
        {
            var options = new ImportOptions
            {
                LayerName = ShapeImporter.DefaultLayerName(summary.ShapePath),
                ZSource = ZSource.ShapeZ,
                SourceCrs = summary.DetectedCrs.EpsgCode ?? string.Empty,
                DetectedSourceCrs = summary.DetectedCrs.EpsgCode ?? string.Empty,
            };

            if (OriginOffset.TryReadFromDocument(doc, out var documentOffset))
            {
                options.ApplyOffset = true;
                options.Offset = documentOffset;
            }
            else if (OriginOffset.IsFarFromOrigin(summary.Header.Bounds))
            {
                options.ApplyOffset = true;
                options.Offset = OriginOffset.Suggest(summary.Header.Bounds);
            }

            return options;
        }

        /// <summary>
        /// The center of the data in the target CRS, or in the source CRS when the target is
        /// empty. The offset proposal starts from this center. Returns null for a file with no
        /// shape.
        /// </summary>
        /// <exception cref="GdalFailureException">GDAL refused a CRS, or cannot translate the center.</exception>
        /// <exception cref="GdalNotFoundException">The machine has no gdaltransform.</exception>
        public static Coordinate? CenterInTargetCrs(
            ShapefileSummary summary,
            ImportOptions options,
            string sourceCrs,
            string targetCrs)
        {
            var bounds = summary.Header.Bounds;
            if (bounds.IsEmpty)
                return null;

            var translator = CrsTranslator.Create(options.OverrideFor(sourceCrs), targetCrs, summary.ProjectionPath);
            if (translator is null)
                return new Coordinate(bounds.CenterX, bounds.CenterY);

            return translator.TranslateCenter(bounds)
                   ?? throw new GdalFailureException("The target CRS cannot represent the center of the data.");
        }
    }
}
