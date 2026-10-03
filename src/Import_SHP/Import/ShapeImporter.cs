using System;
using System.Collections.Generic;
using System.IO;
using Import_SHP.Formats;
using Import_SHP.Gdal;
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

        /// <summary>The vertex count that one GDAL run translates. It limits the memory of a large file.</summary>
        private const int TranslationBatchVertexCount = 50000;

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

            var transform = PointTransform.For(doc, options);
            var report = new ImportReport
            {
                ModelScale = transform.Scale,
                LayoutScale = options.LayoutScale(doc),
            };
            var sidecars = SidecarFiles.Find(shapePath);

            var projectionText = sidecars.ReadProjectionText();
            var translator = CrsTranslator.Create(options.SourceCrsOverride, options.TargetCrs, sidecars.ProjectionPath);
            if (translator is not null)
                projectionText = ReportTheTranslation(translator, options, projectionText, report);

            using var shapeReader = ShapefileReader.Open(shapePath);
            using var table = sidecars.AttributePath is null
                ? null
                : DbfTable.Open(sidecars.AttributePath, sidecars.ReadCodePage());

            if (table is null)
                report.AddWarning("The shapefile has no .dbf file. The objects import without attributes.");

            var layerIndex = FindOrCreateLayer(doc, options.LayerName, projectionText);
            using var rows = table?.ReadRecords().GetEnumerator();

            // The records that wait for the translation. One GDAL run translates a whole batch.
            var batch = new List<(ShapeRecord Record, DbfRecord? Row)>();
            var batchVertexCount = 0;

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

                if (translator is null)
                {
                    ImportRecord(doc, record, row, table?.Fields, options, transform, layerIndex, report);
                    continue;
                }

                batch.Add((record, row));
                batchVertexCount += record.Vertices.Count;
                if (batchVertexCount < TranslationBatchVertexCount)
                    continue;

                ImportTranslatedRecords(doc, batch, translator, table?.Fields, options, transform, layerIndex, report);
                batch.Clear();
                batchVertexCount = 0;
            }

            if (translator is not null)
                ImportTranslatedRecords(doc, batch, translator, table?.Fields, options, transform, layerIndex, report);

            if (options.ApplyOffset && !options.Offset.IsZero)
                OriginOffset.WriteToDocument(doc, options.Offset);

            return report;
        }

        /// <summary>
        /// Writes the two CRS texts into the report. Returns the text that the import layer keeps:
        /// the WKT of the target CRS, or the target text when GDAL gives no WKT.
        /// </summary>
        private static string ReportTheTranslation(
            CrsTranslator translator,
            ImportOptions options,
            string? projectionText,
            ImportReport report)
        {
            report.SourceCrs = options.SourceCrsOverride
                               ?? (options.DetectedSourceCrs.Length > 0 ? options.DetectedSourceCrs : null)
                               ?? DetectedCrs.ParseName(projectionText)
                               ?? "the .prj file";
            report.TargetCrs = translator.TargetCrs;

            var targetWkt = translator.ReadTargetWkt();
            if (DetectedCrs.IsGeographic(targetWkt))
            {
                report.AddWarning(
                    "The target CRS is geographic, so X and Y are in degrees. The elevation keeps its own unit.");
            }

            return targetWkt ?? translator.TargetCrs;
        }

        /// <summary>Translates the vertices of the records with one GDAL run, then imports the records.</summary>
        private void ImportTranslatedRecords(
            RhinoDoc doc,
            IReadOnlyList<(ShapeRecord Record, DbfRecord? Row)> batch,
            CrsTranslator translator,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            PointTransform transform,
            int layerIndex,
            ImportReport report)
        {
            var coordinates = new List<Coordinate>();
            foreach (var (record, _) in batch)
            {
                foreach (var vertex in record.Vertices)
                    coordinates.Add(new Coordinate(vertex.X, vertex.Y));
            }

            var translated = translator.Translate(coordinates);
            var firstIndex = 0;

            foreach (var (record, row) in batch)
            {
                var translatedRecord = TranslateRecord(record, translated, firstIndex);
                firstIndex += record.Vertices.Count;

                if (translatedRecord is null)
                {
                    report.SkippedUntranslatedCount++;
                    report.AddWarning(
                        "Some records hold a vertex that the target CRS cannot represent. Those records are skipped.");
                    continue;
                }

                ImportRecord(doc, translatedRecord, row, fields, options, transform, layerIndex, report);
            }
        }

        /// <summary>
        /// Builds the record with the translated X and Y. The Z values stay. Returns null when a
        /// vertex has no translation.
        /// </summary>
        private static ShapeRecord? TranslateRecord(ShapeRecord record, IReadOnlyList<Coordinate?> translated, int firstIndex)
        {
            var vertices = new ShapeVertex[record.Vertices.Count];

            for (var i = 0; i < vertices.Length; i++)
            {
                var coordinate = translated[firstIndex + i];
                if (coordinate is null)
                    return null;

                vertices[i] = new ShapeVertex(coordinate.Value.X, coordinate.Value.Y, record.Vertices[i].Z);
            }

            return new ShapeRecord(record.RecordNumber, record.ShapeType, record.Bounds, vertices, record.PartStartIndexes);
        }

        private void ImportRecord(
            RhinoDoc doc,
            ShapeRecord record,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            PointTransform transform,
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
                    ImportPoints(doc, record, row, fields, options, transform, layerIndex, report);
                    return;
                case ShapeFamily.PolyLine:
                case ShapeFamily.Polygon:
                    ImportCurves(doc, record, row, fields, options, transform, layerIndex, report);
                    return;
            }
        }

        private void ImportPoints(
            RhinoDoc doc,
            ShapeRecord record,
            DbfRecord? row,
            IReadOnlyList<DbfField>? fields,
            ImportOptions options,
            PointTransform transform,
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
                if (doc.Objects.AddPoint(ToPoint3d(vertex, row, options, transform, report), attributes) != Guid.Empty)
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
            PointTransform transform,
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
                    polyline.Add(ToPoint3d(vertex, row, options, transform, report));

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

        private static Point3d ToPoint3d(
            ShapeVertex vertex,
            DbfRecord? row,
            ImportOptions options,
            PointTransform transform,
            ImportReport report)
        {
            return transform.Apply(vertex.X, vertex.Y, ResolveZ(vertex, row, options, report));
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
