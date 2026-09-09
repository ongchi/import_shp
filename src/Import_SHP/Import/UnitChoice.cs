using System;
using System.Collections.Generic;
using Rhino;

namespace Import_SHP.Import
{
    /// <summary>One entry of a unit dropdown: the text the user reads and the unit it selects.</summary>
    public readonly struct UnitChoiceItem
    {
        public UnitChoiceItem(string label, UnitSystem unit)
        {
            Label = label;
            Unit = unit;
        }

        public string Label { get; }

        public UnitSystem Unit { get; }
    }

    /// <summary>
    /// The units that the import options offer, and the scale between two of them.
    /// A shapefile holds no unit, so the user states the unit of the source coordinates here.
    /// </summary>
    public static class UnitChoice
    {
        /// <summary>The entry that leaves the coordinates as they are. Its scale is always 1.</summary>
        public const UnitSystem SameAsDocument = UnitSystem.None;

        private static readonly UnitChoiceItem[] ItemArray =
        {
            new("Same as the document", SameAsDocument),
            new("Millimeters", UnitSystem.Millimeters),
            new("Centimeters", UnitSystem.Centimeters),
            new("Meters", UnitSystem.Meters),
            new("Kilometers", UnitSystem.Kilometers),
            new("Inches", UnitSystem.Inches),
            new("Feet", UnitSystem.Feet),
            new("Yards", UnitSystem.Yards),
            new("Miles", UnitSystem.Miles),
        };

        public static IReadOnlyList<UnitChoiceItem> Items => ItemArray;

        /// <summary>The labels in the order of <see cref="Items"/>, for the command line option list.</summary>
        public static string[] Labels { get; } = Array.ConvertAll(ItemArray, item => item.Label);

        /// <summary>The index of a unit in <see cref="Items"/>. An unknown unit gives 0.</summary>
        public static int IndexOf(UnitSystem unit)
        {
            var index = Array.FindIndex(ItemArray, item => item.Unit == unit);
            return index < 0 ? 0 : index;
        }

        /// <summary>The unit at an index of <see cref="Items"/>. An index outside the list gives the first unit.</summary>
        public static UnitSystem At(int index)
        {
            return index >= 0 && index < ItemArray.Length ? ItemArray[index].Unit : SameAsDocument;
        }

        /// <summary>
        /// The factor that turns a length in <paramref name="source"/> into a length in
        /// <paramref name="target"/>. A unit that states nothing gives 1.
        /// </summary>
        public static double ScaleTo(UnitSystem source, UnitSystem target)
        {
            if (!IsKnown(source) || !IsKnown(target) || source == target)
                return 1.0;

            var scale = RhinoMath.UnitScale(source, target);
            return scale > 0.0 ? scale : 1.0;
        }

        private static bool IsKnown(UnitSystem unit) => unit != UnitSystem.None && unit != UnitSystem.Unset;
    }
}
