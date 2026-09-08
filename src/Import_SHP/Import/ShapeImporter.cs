using System;
using System.Collections.Generic;
using System.IO;
using Import_SHP.Formats;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Import_SHP.Import
{
    /// <summary>Reads a shapefile and adds the geometry and the attributes to a Rhino document.</summary>
    public sealed class ShapeImporter
    {
        /// <summary>The layer user text key that holds the coordinate system text of the .prj file.</summary>
        public const string ProjectionUserTextKey = "Import_SHP.Projection";

        private readonly double _tolerance;

        public ShapeImporter(double tolerance)
        {
            _tolerance = tolerance > 0.0 ? tolerance : 0.001;
        }

        public ImportReport Import(RhinoDoc doc, string shapePath, ImportOptions options)
        {
            if (doc is null)
                throw new ArgumentNullException(nameof(doc));
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var report = new ImportReport();
            var sidecars = SidecarFiles.Find(shapePath);

            using var shapeReader = ShapefileReader.Open(shapePath);
            using var table = sidecars.AttributePath is null
                ? null
                : DbfTable.Open(sidecars.AttributePath, sidecars.ReadCodePage());

            if (table is null)
                report.AddWarning("The shapefile has no .dbf file. The objects import without attributes.");

            var layerIndex = FindOrCreateLayer(doc, options.LayerName, sidecars.ReadProjectionText());
            using var rows = table?.ReadRecords().GetEnumerator();

            foreach (var record in shapeReader.ReadRecords())
            {
                report.RecordCount++;

                DbfRecord? row = null;
                if (rows is not null && rows.MoveNext())
                    row = rows.Current;

                if (row is not null && row.IsDeleted)
                {
                    report.SkippedDeletedCount++;
                    continue;
                }

                ImportRecord(doc, record, row, table?.Fields, options, layerIndex, report);
            }

            if (options.ApplyOffset && !options.Offset.IsZero)
                OriginOffset.WriteToDocument(doc, options.Offset);

            return report;
        }

        private void ImportRecord(
            RhinoDoc doc,
            ShapeRecord record,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            int layerIndex,
            ImportReport report)
        {
            switch (record.Family)
            {
                case ShapeFamily.Null:
                    report.SkippedNullCount++;
                    return;
                case ShapeFamily.Unsupported:
                    report.SkippedUnsupportedCount++;
                    report.AddWarning($"Shape type {record.ShapeType} is not supported. Those records are skipped.");
                    return;
                case ShapeFamily.Point:
                case ShapeFamily.MultiPoint:
                    ImportPoints(doc, record, row, fields, options, layerIndex, report);
                    return;
                case ShapeFamily.PolyLine:
                case ShapeFamily.Polygon:
                    ImportCurves(doc, record, row, fields, options, layerIndex, report);
                    return;
            }
        }

        private void ImportPoints(
            RhinoDoc doc,
            ShapeRecord record,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            int layerIndex,
            ImportReport report)
        {
            if (record.Vertices.Count == 0)
            {
                report.SkippedNullCount++;
                return;
            }

            var groupIndex = record.Vertices.Count > 1 && options.GroupParts ? doc.Groups.Add() : -1;

            foreach (var vertex in record.Vertices)
            {
                var attributes = CreateAttributes(doc, layerIndex, row, fields, options, groupIndex);
                if (doc.Objects.AddPoint(ToPoint3d(vertex, row, options, report), attributes) != Guid.Empty)
                    report.PointCount++;
                else
                    report.AddWarning($"Record {record.RecordNumber} holds a point that Rhino did not accept.");
            }
        }

        private void ImportCurves(
            RhinoDoc doc,
            ShapeRecord record,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            int layerIndex,
            ImportReport report)
        {
            var closeParts = record.Family == ShapeFamily.Polygon;
            var groupIndex = record.PartCount > 1 && options.GroupParts ? doc.Groups.Add() : -1;

            for (var partIndex = 0; partIndex < record.PartCount; partIndex++)
            {
                var part = record.GetPart(partIndex);
                var polyline = new Polyline(part.Count);
                foreach (var vertex in part)
                    polyline.Add(ToPoint3d(vertex, row, options, report));

                if (closeParts && polyline.Count > 2 && !polyline.IsClosed)
                    polyline.Add(polyline[0]);

                polyline.DeleteShortSegments(_tolerance);

                if (polyline.Count < 2)
                {
                    report.AddWarning($"Record {record.RecordNumber} holds a part with fewer than two different vertices. That part is skipped.");
                    continue;
                }

                var attributes = CreateAttributes(doc, layerIndex, row, fields, options, groupIndex);
                if (doc.Objects.AddCurve(polyline.ToPolylineCurve(), attributes) != Guid.Empty)
                    report.CurveCount++;
                else
                    report.AddWarning($"Record {record.RecordNumber} holds a curve that Rhino did not accept.");
            }
        }

        private Point3d ToPoint3d(ShapeVertex vertex, DbfRecord? row, ImportOptions options, ImportReport report)
        {
            var point = new Point3d(vertex.X, vertex.Y, ResolveZ(vertex, row, options, report));
            return options.ApplyOffset ? point + options.Offset : point;
        }

        private static double ResolveZ(ShapeVertex vertex, DbfRecord? row, ImportOptions options, ImportReport report)
        {
            switch (options.ZSource)
            {
                case ZSource.Constant:
                    return options.ConstantZ;

                case ZSource.AttributeField:
                    if (row is not null && row.TryGetDouble(options.ZFieldName, out var fieldValue))
                        return fieldValue;

                    report.AddWarning(
                        $"The field \"{options.ZFieldName}\" holds no number in some records. Those objects import at elevation 0.");
                    return 0.0;

                default:
                    return vertex.HasZ ? vertex.Z : 0.0;
            }
        }

        private static ObjectAttributes CreateAttributes(
            RhinoDoc doc,
            int layerIndex,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            int groupIndex)
        {
            var attributes = doc.CreateDefaultAttributes();
            attributes.LayerIndex = layerIndex;

            if (row is not null && fields is not null)
            {
                for (var i = 0; i < fields.Count && i < row.Values.Count; i++)
                    attributes.SetUserString(fields[i].Name, row.Values[i]);

                if (!string.IsNullOrEmpty(options.NameFieldName))
                {
                    var name = row.GetValue(options.NameFieldName);
                    if (!string.IsNullOrEmpty(name))
                        attributes.Name = name;
                }
            }

            if (groupIndex >= 0)
                attributes.AddToGroup(groupIndex);

            return attributes;
        }

        /// <summary>Returns the index of the import layer. The layer is created when it does not exist.</summary>
        private static int FindOrCreateLayer(RhinoDoc doc, string layerName, string? projectionText)
        {
            var name = string.IsNullOrWhiteSpace(layerName) ? "Shapefile" : layerName.Trim();
            var layer = doc.Layers.FindName(name);

            if (layer is null)
            {
                var index = doc.Layers.Add(name, System.Drawing.Color.Black);
                if (index < 0)
                    return doc.Layers.CurrentLayerIndex;
                layer = doc.Layers[index];
            }

            if (!string.IsNullOrEmpty(projectionText))
                layer.SetUserString(ProjectionUserTextKey, projectionText);

            return layer.Index;
        }

        /// <summary>Builds the default layer name of a shapefile path.</summary>
        public static string DefaultLayerName(string shapePath)
        {
            var name = Path.GetFileNameWithoutExtension(shapePath);
            return string.IsNullOrWhiteSpace(name) ? "Shapefile" : name;
        }
    }
}
