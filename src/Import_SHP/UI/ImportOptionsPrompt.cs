using System;
using System.Collections.Generic;
using System.Linq;
using Import_SHP.Import;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Import_SHP.UI
{
    /// <summary>Collects the import options on the command line. Scripts can set every option.</summary>
    public static class ImportOptionsPrompt
    {
        private const string NoFieldItem = "None";

        private static readonly string[] ZSourceNames = { "ShapeZ", "AttributeField", "Constant" };

        /// <summary>Asks for the options. Returns false when the user cancels.</summary>
        public static bool TryPrompt(ShapefileSummary summary, ImportOptions options)
        {
            var numericFields = summary.NumericFieldNames;
            var nameFields = new[] { NoFieldItem }.Concat(summary.FieldNames).ToArray();

            var constantZ = new OptionDouble(options.ConstantZ);
            var offsetX = new OptionDouble(options.Offset.X);
            var offsetY = new OptionDouble(options.Offset.Y);
            var applyOffset = new OptionToggle(options.ApplyOffset, "No", "Yes");
            var groupParts = new OptionToggle(options.GroupParts, "No", "Yes");

            var zFieldIndex = Math.Max(0, IndexOf(numericFields, options.ZFieldName));
            var nameFieldIndex = Math.Max(0, IndexOf(nameFields, options.NameFieldName));

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
            options.GroupParts = groupParts.CurrentValue;
            options.ApplyOffset = applyOffset.CurrentValue;
            options.Offset = applyOffset.CurrentValue ? new Vector3d(offsetX.CurrentValue, offsetY.CurrentValue, 0.0) : Vector3d.Zero;
            options.ZFieldName = numericFields.Count > 0 ? numericFields[Math.Min(zFieldIndex, numericFields.Count - 1)] : string.Empty;

            var chosenNameField = nameFields[Math.Min(nameFieldIndex, nameFields.Length - 1)];
            options.NameFieldName = chosenNameField == NoFieldItem ? string.Empty : chosenNameField;

            return true;
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
