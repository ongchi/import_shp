using System.Collections.Generic;
using Import_SHP.Formats;
using Import_SHP.Gdal;

namespace Import_SHP.Import
{
    /// <summary>
    /// Translates the horizontal coordinates of one shapefile from its source CRS to a target CRS.
    /// The elevation keeps its value.
    /// </summary>
    public sealed class CrsTranslator
    {
        private readonly GdalTools _tools;
        private readonly string _sourceCrs;

        private CrsTranslator(GdalTools tools, string sourceCrs, string targetCrs)
        {
            _tools = tools;
            _sourceCrs = sourceCrs;
            TargetCrs = targetCrs;
        }

        /// <summary>The target CRS text, as the user gave it.</summary>
        public string TargetCrs { get; }

        /// <summary>
        /// Builds the translator, or returns null when the target CRS is empty, which means no
        /// translation.
        /// </summary>
        /// <param name="sourceCrsOverride">The CRS that replaces the .prj file, or null to use the file.</param>
        /// <param name="targetCrs">The CRS of the result.</param>
        /// <param name="projectionPath">The path of the .prj file, or null when the shapefile has none.</param>
        /// <exception cref="GdalNotFoundException">The machine has no gdaltransform.</exception>
        /// <exception cref="GdalFailureException">The source CRS is unknown.</exception>
        public static CrsTranslator? Create(string? sourceCrsOverride, string? targetCrs, string? projectionPath)
        {
            if (string.IsNullOrWhiteSpace(targetCrs))
                return null;

            // GDAL reads the CRS from a .prj path, so the file needs no conversion to an EPSG code.
            var sourceCrs = string.IsNullOrWhiteSpace(sourceCrsOverride) ? projectionPath : sourceCrsOverride.Trim();
            if (sourceCrs is null)
                throw new GdalFailureException("The shapefile has no .prj file. Enter the source CRS.");

            return new CrsTranslator(GdalTools.Find(), sourceCrs, targetCrs.Trim());
        }

        /// <summary>
        /// Translates the coordinates. An entry of the result is null when GDAL cannot translate
        /// that coordinate.
        /// </summary>
        public IReadOnlyList<Coordinate?> Translate(IReadOnlyList<Coordinate> coordinates)
        {
            return GdalTransform.Translate(_tools, _sourceCrs, TargetCrs, coordinates);
        }

        /// <summary>
        /// Translates the center of the bounds. Returns null for empty bounds and for a center
        /// that GDAL cannot translate.
        /// </summary>
        public Coordinate? TranslateCenter(ShapeBounds bounds)
        {
            if (bounds.IsEmpty)
                return null;

            return Translate(new[] { new Coordinate(bounds.CenterX, bounds.CenterY) })[0];
        }

        /// <summary>The WKT of the target CRS, or null when GDAL does not give it.</summary>
        public string? ReadTargetWkt() => GdalSrsInfo.ReadWkt(_tools, TargetCrs);
    }
}
