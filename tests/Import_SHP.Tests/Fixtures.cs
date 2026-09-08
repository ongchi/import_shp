using System.IO;

namespace Import_SHP.Tests
{
    /// <summary>Finds the binary fixtures that tools/make_fixtures.py writes.</summary>
    internal static class Fixtures
    {
        public static string Path(string fileName)
        {
            return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "fixtures", fileName);
        }
    }
}
