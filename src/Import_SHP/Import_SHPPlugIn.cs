using System;
using Import_SHP.Import;
using Rhino;
using Rhino.FileIO;
using Rhino.PlugIns;

namespace Import_SHP
{
    /// <summary>Adds the ESRI shapefile format to the Rhino import dialog.</summary>
    public sealed class Import_SHPPlugIn : FileImportPlugIn
    {
        public Import_SHPPlugIn()
        {
            Instance = this;
        }

        public static Import_SHPPlugIn? Instance { get; private set; }

        protected override FileTypeList AddFileTypes(FileReadOptions options)
        {
            var fileTypes = new FileTypeList();
            fileTypes.AddFileType("ESRI Shapefile (*.shp)", "shp");
            return fileTypes;
        }

        protected override bool ReadFile(string filename, int index, RhinoDoc doc, FileReadOptions options)
        {
            try
            {
                var importOptions = ImportOptionsResolver.Resolve(doc, filename, interactive: !options.BatchMode);
                if (importOptions is null)
                    return false;

                var importer = new ShapeImporter(doc.ModelAbsoluteTolerance);
                var report = importer.Import(doc, filename, importOptions);
                ReportWriter.Write(report, filename);
                doc.Views.Redraw();
                return true;
            }
            catch (Exception exception) when (exception is Formats.ShapefileFormatException or System.IO.IOException or UnauthorizedAccessException
                                                  or Gdal.GdalNotFoundException or Gdal.GdalFailureException)
            {
                RhinoApp.WriteLine($"Shapefile import failed: {exception.Message}");
                return false;
            }
        }
    }
}
