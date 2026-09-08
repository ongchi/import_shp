using System;

namespace Import_SHP.Formats
{
    /// <summary>Thrown when a shapefile part does not follow the file format.</summary>
    public sealed class ShapefileFormatException : Exception
    {
        public ShapefileFormatException(string message)
            : base(message)
        {
        }

        public ShapefileFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
