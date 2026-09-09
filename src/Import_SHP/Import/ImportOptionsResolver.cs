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
    }
}
