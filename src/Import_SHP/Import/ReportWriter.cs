using System.IO;
using Rhino;

namespace Import_SHP.Import
{
    /// <summary>Writes the result of an import to the Rhino command line.</summary>
    public static class ReportWriter
    {
        public static void Write(ImportReport report, string shapePath)
        {
            RhinoApp.WriteLine(report.ToSummary(Path.GetFileName(shapePath)));

            var crsText = report.ToCrsText();
            if (crsText is not null)
                RhinoApp.WriteLine($"  {crsText}");

            var unitText = report.ToUnitText();
            if (unitText is not null)
                RhinoApp.WriteLine($"  {unitText}");

            foreach (var warning in report.Warnings)
                RhinoApp.WriteLine($"  Warning: {warning}");
        }
    }
}
