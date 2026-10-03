using System;

namespace Import_SHP.Gdal
{
    /// <summary>Thrown when the machine has no GDAL command line tools.</summary>
    public sealed class GdalNotFoundException : Exception
    {
        public GdalNotFoundException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Thrown when a GDAL tool stops with an error.</summary>
    public sealed class GdalFailureException : Exception
    {
        public GdalFailureException(string message)
            : base(message)
        {
        }
    }
}
