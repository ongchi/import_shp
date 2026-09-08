using System;
using System.Globalization;
using System.Text;

namespace Import_SHP.Formats
{
    /// <summary>Finds the text encoding of an attribute table. Falls back to Latin-1, which never fails on a byte value.</summary>
    public static class TextEncodings
    {
        public static Encoding Default => Encoding.Latin1;

        public static Encoding FromCodePage(int codePage)
        {
            if (codePage <= 0)
                return Default;

            try
            {
                return Encoding.GetEncoding(codePage);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                return Default;
            }
        }

        /// <summary>Reads an encoding from the text of a .cpg file, for example "UTF-8", "ISO-8859-1" or "1252".</summary>
        public static Encoding FromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Default;

            var text = name.Trim();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var codePage))
                return FromCodePage(codePage);

            // Common ArcGIS spellings that the .NET encoding names do not accept.
            var normalized = text.Replace(" ", string.Empty).ToUpperInvariant();
            switch (normalized)
            {
                case "UTF8":
                case "UTF-8":
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                case "LATIN1":
                case "ISO88591":
                case "ISO-8859-1":
                    return Default;
            }

            if (normalized.StartsWith("CP", StringComparison.Ordinal)
                && int.TryParse(normalized.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cpNumber))
                return FromCodePage(cpNumber);

            try
            {
                return Encoding.GetEncoding(text);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                return Default;
            }
        }
    }
}
