using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Import_SHP.Gdal
{
    /// <summary>One horizontal position.</summary>
    public readonly struct Coordinate
    {
        public Coordinate(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    /// <summary>Runs "gdaltransform" to translate coordinates from one CRS to another.</summary>
    public static class GdalTransform
    {
        /// <summary>The line that gdaltransform writes for a coordinate that it cannot translate.</summary>
        private const string FailureLine = "transformation failed.";

        /// <summary>
        /// Translates the coordinates. The result has one entry for each input coordinate, in the
        /// same order. An entry is null when GDAL cannot translate that coordinate.
        /// </summary>
        /// <exception cref="GdalFailureException">GDAL refused a CRS, or its output is not readable.</exception>
        public static IReadOnlyList<Coordinate?> Translate(
            GdalTools tools,
            string sourceCrs,
            string targetCrs,
            IReadOnlyList<Coordinate> coordinates)
        {
            if (coordinates.Count == 0)
                return new Coordinate?[0];

            var output = GdalProcess.Run(
                tools.GdalTransformPath,
                BuildArguments(sourceCrs, targetCrs),
                FormatInput(coordinates));

            return ParseOutput(output, coordinates.Count);
        }

        public static IReadOnlyList<string> BuildArguments(string sourceCrs, string targetCrs)
        {
            return new[] { "-s_srs", sourceCrs.Trim(), "-t_srs", targetCrs.Trim(), "-output_xy" };
        }

        /// <summary>Writes one "x y" line for each coordinate, with every digit of the value.</summary>
        public static string FormatInput(IReadOnlyList<Coordinate> coordinates)
        {
            var text = new StringBuilder();
            foreach (var coordinate in coordinates)
            {
                text.Append(coordinate.X.ToString("R", CultureInfo.InvariantCulture));
                text.Append(' ');
                text.Append(coordinate.Y.ToString("R", CultureInfo.InvariantCulture));
                text.Append('\n');
            }

            return text.ToString();
        }

        /// <summary>Reads the "x y" lines that gdaltransform writes.</summary>
        /// <exception cref="GdalFailureException">The line count is wrong, or a line holds no two numbers.</exception>
        public static IReadOnlyList<Coordinate?> ParseOutput(string output, int expectedCount)
        {
            var coordinates = new List<Coordinate?>(expectedCount);

            foreach (var line in output.Split('\n'))
            {
                var text = line.Trim();
                if (text.Length == 0)
                    continue;

                coordinates.Add(text == FailureLine ? null : ParseLine(text));
            }

            if (coordinates.Count != expectedCount)
            {
                throw new GdalFailureException(
                    $"gdaltransform returned {coordinates.Count} coordinates for {expectedCount} coordinates.");
            }

            return coordinates;
        }

        /// <summary>Reads one line. A value that is not finite counts as a failed translation.</summary>
        private static Coordinate? ParseLine(string text)
        {
            var parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new GdalFailureException($"gdaltransform returned the line \"{text}\", which holds no coordinate.");

            // The C library writes "inf" and "nan", which the .NET number parser does not read.
            if (IsNotFiniteText(parts[0]) || IsNotFiniteText(parts[1]))
                return null;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                throw new GdalFailureException($"gdaltransform returned the line \"{text}\", which holds no coordinate.");

            return double.IsFinite(x) && double.IsFinite(y) ? new Coordinate(x, y) : null;
        }

        private static bool IsNotFiniteText(string text)
        {
            var value = text.TrimStart('+', '-');
            return value.StartsWith("inf", System.StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("nan", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
