using System;
using System.IO;
using Import_SHP.Formats;
using Import_SHP.Import;
using Import_SHP.UI;
using Rhino;
using Rhino.Commands;
using Rhino.Input;

namespace Import_SHP
{
    /// <summary>Imports one shapefile. The command works in interactive mode and in script mode.</summary>
    public sealed class ImportShapefileCommand : Command
    {
        public override string EnglishName => "ImportShapefile";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var interactive = mode == RunMode.Interactive;

            var shapePath = GetShapePath(interactive);
            if (shapePath is null)
                return Result.Cancel;

            if (!File.Exists(shapePath))
            {
                RhinoApp.WriteLine($"The file \"{shapePath}\" does not exist.");
                return Result.Failure;
            }

            try
            {
                var summary = ShapefileSummary.Read(shapePath);
                var options = ImportOptionsResolver.CreateDefaults(doc, summary);

                var accepted = interactive
                    ? ImportOptionsDialog.Show(summary, options)
                    : ImportOptionsPrompt.TryPrompt(summary, options);
                if (!accepted)
                    return Result.Cancel;

                var importer = new ShapeImporter(doc.ModelAbsoluteTolerance);
                var report = importer.Import(doc, shapePath, options);
                ReportWriter.Write(report, shapePath);

                doc.Views.Redraw();
                return Result.Success;
            }
            catch (Exception exception) when (exception is ShapefileFormatException or IOException or UnauthorizedAccessException)
            {
                RhinoApp.WriteLine($"Shapefile import failed: {exception.Message}");
                return Result.Failure;
            }
        }

        private static string? GetShapePath(bool interactive)
        {
            if (!interactive)
            {
                var scriptPath = string.Empty;
                return RhinoGet.GetString("Shapefile path", false, ref scriptPath) == Result.Success
                    ? scriptPath.Trim().Trim('"')
                    : null;
            }

            var dialog = new Rhino.UI.OpenFileDialog
            {
                Title = "Import Shapefile",
                Filter = "ESRI Shapefile (*.shp)|*.shp",
                MultiSelect = false,
            };

            return dialog.ShowOpenDialog() ? dialog.FileName : null;
        }
    }
}
