using System;
using System.Collections.Generic;
using System.Linq;
using Import_SHP.Gdal;
using Import_SHP.Import;
using Rhino;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Import_SHP.UI
{
    /// <summary>Collects the import options on the command line. Scripts can set every option.</summary>
    public static class ImportOptionsPrompt
    {
        private const string NoFieldItem = "None";

        /// <summary>The command line text of an empty CRS.</summary>
        private const string NoCrs = "None";

        private static readonly string[] ZSourceNames = { "ShapeZ", "AttributeField", "Constant" };

        /// <summary>Asks for the options. Returns false when the user cancels.</summary>
        public static bool TryPrompt(RhinoDoc doc, ShapefileSummary summary, ImportOptions options)
        {
            var offsetComesFromDocument = OriginOffset.TryReadFromDocument(doc, out _);
            var numericFields = summary.NumericFieldNames;
            var nameFields = new[] { NoFieldItem }.Concat(summary.FieldNames).ToArray();

            var constantZ = new OptionDouble(options.ConstantZ);
            var offsetX = new OptionDouble(options.Offset.X);
            var offsetY = new OptionDouble(options.Offset.Y);
            var applyOffset = new OptionToggle(options.ApplyOffset, "No", "Yes");
            var groupParts = new OptionToggle(options.GroupParts, "No", "Yes");

            var zFieldIndex = Math.Max(0, IndexOf(numericFields, options.ZFieldName));
            var nameFieldIndex = Math.Max(0, IndexOf(nameFields, options.NameFieldName));
            var modelUnitsIndex = UnitChoice.IndexOf(options.ModelUnits);
            var layoutUnitsIndex = UnitChoice.IndexOf(options.LayoutUnits);

            // The center of the data in the target CRS. The offset proposal starts from it.
            var bounds = summary.Header.Bounds;
            Coordinate? center = bounds.IsEmpty ? null : new Coordinate(bounds.CenterX, bounds.CenterY);

            var getOption = new GetOption();
            getOption.AcceptNothing(true);

            while (true)
            {
                getOption.SetCommandPrompt($"Shapefile import options. Layer \"{options.LayerName}\". Press Enter to import");
                getOption.ClearCommandOptions();

                var zSourceOption = getOption.AddOptionList("Elevation", ZSourceNames, (int)options.ZSource);
                var zFieldOption = options.ZSource == ZSource.AttributeField && numericFields.Count > 0
                    ? getOption.AddOptionList("ElevationField", numericFields, zFieldIndex)
                    : -1;
                var constantOption = options.ZSource == ZSource.Constant
                    ? getOption.AddOptionDouble("ConstantElevation", ref constantZ)
                    : -1;
                var nameFieldOption = nameFields.Length > 1
                    ? getOption.AddOptionList("NameField", nameFields, nameFieldIndex)
                    : -1;
                var layerOption = getOption.AddOption("Layer");
                var sourceCrsOption = getOption.AddOption("SourceCRS", CrsOptionValue(options.SourceCrs));
                var targetCrsOption = getOption.AddOption("TargetCRS", CrsOptionValue(options.TargetCrs));
                var modelUnitsOption = getOption.AddOptionList("ModelUnits", UnitChoice.Labels, modelUnitsIndex);
                var layoutUnitsOption = getOption.AddOptionList("LayoutUnits", UnitChoice.Labels, layoutUnitsIndex);
                var offsetOption = getOption.AddOptionToggle("MoveToOrigin", ref applyOffset);
                var offsetXOption = applyOffset.CurrentValue ? getOption.AddOptionDouble("OffsetX", ref offsetX) : -1;
                var offsetYOption = applyOffset.CurrentValue ? getOption.AddOptionDouble("OffsetY", ref offsetY) : -1;
                var groupOption = getOption.AddOptionToggle("GroupParts", ref groupParts);

                var result = getOption.Get();

                if (result == GetResult.Nothing)
                    break;

                if (result != GetResult.Option)
                    return false;

                var chosen = getOption.OptionIndex();

                if (chosen == zSourceOption)
                {
                    options.ZSource = (ZSource)getOption.Option().CurrentListOptionIndex;
                    if (options.ZSource == ZSource.AttributeField && numericFields.Count == 0)
                    {
                        Rhino.RhinoApp.WriteLine("The shapefile has no numeric field. Select another elevation source.");
                        options.ZSource = ZSource.ShapeZ;
                    }
                }
                else if (chosen == zFieldOption)
                {
                    zFieldIndex = getOption.Option().CurrentListOptionIndex;
                }
                else if (chosen == nameFieldOption)
                {
                    nameFieldIndex = getOption.Option().CurrentListOptionIndex;
                }
                else if (chosen == modelUnitsOption)
                {
                    modelUnitsIndex = getOption.Option().CurrentListOptionIndex;

                    // The offset is in document units, so the proposal follows the model unit.
                    if (!offsetComesFromDocument)
                    {
                        var suggestion = SuggestOffset(doc, center, UnitChoice.At(modelUnitsIndex));
                        offsetX = new OptionDouble(suggestion.X);
                        offsetY = new OptionDouble(suggestion.Y);
                    }
                }
                else if (chosen == sourceCrsOption || chosen == targetCrsOption)
                {
                    var isSource = chosen == sourceCrsOption;
                    if (!TryGetCrs(isSource ? "Source CRS" : "Target CRS", isSource ? options.SourceCrs : options.TargetCrs, out var crs))
                        continue;

                    var sourceCrs = isSource ? crs : options.SourceCrs;
                    var targetCrs = isSource ? options.TargetCrs : crs;
                    if (!TryGetCenter(summary, options, sourceCrs, targetCrs, out var translatedCenter))
                        continue;

                    options.SourceCrs = sourceCrs;
                    options.TargetCrs = targetCrs;
                    center = translatedCenter;

                    // The translated data has another center.
                    if (!offsetComesFromDocument)
                    {
                        var suggestion = SuggestOffset(doc, center, UnitChoice.At(modelUnitsIndex));
                        offsetX = new OptionDouble(suggestion.X);
                        offsetY = new OptionDouble(suggestion.Y);
                    }
                }
                else if (chosen == layoutUnitsOption)
                {
                    layoutUnitsIndex = getOption.Option().CurrentListOptionIndex;
                }
                else if (chosen == layerOption)
                {
                    var layerName = options.LayerName;
                    if (RhinoGet.GetString("Layer name", true, ref layerName) != Rhino.Commands.Result.Success)
                        continue;
                    if (!string.IsNullOrWhiteSpace(layerName))
                        options.LayerName = layerName.Trim();
                }
                else if (chosen == offsetOption || chosen == offsetXOption || chosen == offsetYOption
                         || chosen == constantOption || chosen == groupOption)
                {
                    // The option objects already hold the new value.
                }
            }

            options.ConstantZ = constantZ.CurrentValue;
            options.ModelUnits = UnitChoice.At(modelUnitsIndex);
            options.LayoutUnits = UnitChoice.At(layoutUnitsIndex);
            options.GroupParts = groupParts.CurrentValue;
            options.ApplyOffset = applyOffset.CurrentValue;
            options.Offset = applyOffset.CurrentValue ? new Vector3d(offsetX.CurrentValue, offsetY.CurrentValue, 0.0) : Vector3d.Zero;
            options.ZFieldName = numericFields.Count > 0 ? numericFields[Math.Min(zFieldIndex, numericFields.Count - 1)] : string.Empty;

            var chosenNameField = nameFields[Math.Min(nameFieldIndex, nameFields.Length - 1)];
            options.NameFieldName = chosenNameField == NoFieldItem ? string.Empty : chosenNameField;

            return true;
        }

        /// <summary>The text that the command line shows for a CRS option.</summary>
        private static string CrsOptionValue(string crs) => string.IsNullOrWhiteSpace(crs) ? NoCrs : crs.Trim();

        /// <summary>Asks for a CRS text. The text "None" clears the value.</summary>
        private static bool TryGetCrs(string prompt, string current, out string crs)
        {
            crs = CrsOptionValue(current);
            if (RhinoGet.GetString($"{prompt}, or {NoCrs}", true, ref crs) != Rhino.Commands.Result.Success)
                return false;

            crs = crs.Trim().Trim('"');
            if (crs.Length == 0 || string.Equals(crs, NoCrs, StringComparison.OrdinalIgnoreCase))
                crs = string.Empty;

            return true;
        }

        /// <summary>
        /// Translates the center of the data. A CRS that GDAL refuses gives a message and no change.
        /// </summary>
        private static bool TryGetCenter(
            ShapefileSummary summary,
            ImportOptions options,
            string sourceCrs,
            string targetCrs,
            out Coordinate? center)
        {
            try
            {
                center = ImportOptionsResolver.CenterInTargetCrs(summary, options, sourceCrs, targetCrs);
                return true;
            }
            catch (Exception exception) when (exception is GdalFailureException or GdalNotFoundException)
            {
                RhinoApp.WriteLine(exception.Message);
                center = null;
                return false;
            }
        }

        /// <summary>The proposed offset for a model unit, in document units.</summary>
        private static Vector3d SuggestOffset(RhinoDoc doc, Coordinate? center, UnitSystem modelUnits)
        {
            if (center is null)
                return Vector3d.Zero;

            var scale = UnitChoice.ScaleTo(modelUnits, doc.ModelUnitSystem);
            return OriginOffset.Suggest(center.Value.X * scale, center.Value.Y * scale);
        }

        private static int IndexOf(IReadOnlyList<string> names, string value)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}
