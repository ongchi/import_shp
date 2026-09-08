using System;
using System.IO;
using System.Linq;
using Import_SHP.Formats;
using Xunit;

namespace Import_SHP.Tests
{
    public class ShapefileReaderTests
    {
        [Fact]
        public void Header_reports_shape_type_and_bounds()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("points.shp"));

            Assert.Equal(ShapeType.Point, reader.Header.ShapeType);
            Assert.Equal(-5.5, reader.Header.Bounds.XMin, 9);
            Assert.Equal(7.25, reader.Header.Bounds.YMin, 9);
            Assert.Equal(1000.0, reader.Header.Bounds.XMax, 9);
            Assert.Equal(2000.0, reader.Header.Bounds.YMax, 9);
        }

        [Fact]
        public void Point_records_keep_file_order_and_coordinates()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("points.shp"));
            var records = reader.ReadRecords().ToList();

            Assert.Equal(4, records.Count);
            Assert.Equal(new[] { 1, 2, 3, 4 }, records.Select(record => record.RecordNumber));
            Assert.Equal(10.0, records[0].Vertices[0].X, 9);
            Assert.Equal(20.0, records[0].Vertices[0].Y, 9);
            Assert.Equal(-5.5, records[1].Vertices[0].X, 9);
            Assert.Equal(2000.0, records[3].Vertices[0].Y, 9);
        }

        [Fact]
        public void Null_record_has_no_vertices()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("points.shp"));
            var nullRecord = reader.ReadRecords().ElementAt(2);

            Assert.Equal(ShapeFamily.Null, nullRecord.Family);
            Assert.Empty(nullRecord.Vertices);
        }

        [Fact]
        public void Point_record_has_no_z_when_the_type_has_no_z()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("points.shp"));
            var record = reader.ReadRecords().First();

            Assert.False(record.Vertices[0].HasZ);
        }

        [Fact]
        public void Multi_part_polyline_reports_each_part()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("lines.shp"));
            var records = reader.ReadRecords().ToList();

            Assert.Equal(2, records.Count);
            Assert.Equal(1, records[0].PartCount);
            Assert.Equal(2, records[1].PartCount);
            Assert.Equal(3, records[0].GetPart(0).Count);
            Assert.Equal(2, records[1].GetPart(0).Count);
            Assert.Equal(3, records[1].GetPart(1).Count);
        }

        [Fact]
        public void Polyline_part_holds_the_written_coordinates()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("lines.shp"));
            var secondPart = reader.ReadRecords().ElementAt(1).GetPart(1);

            Assert.Equal(20.0, secondPart[0].X, 9);
            Assert.Equal(25.0, secondPart[1].X, 9);
            Assert.Equal(55.0, secondPart[1].Y, 9);
            Assert.Equal(30.0, secondPart[2].X, 9);
        }

        [Fact]
        public void Polygon_rings_are_closed_and_separate()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("polygons.shp"));
            var record = reader.ReadRecords().Single();

            Assert.Equal(ShapeFamily.Polygon, record.Family);
            Assert.Equal(2, record.PartCount);

            var outerRing = record.GetPart(0);
            Assert.Equal(5, outerRing.Count);
            Assert.Equal(outerRing[0].X, outerRing[^1].X, 9);
            Assert.Equal(outerRing[0].Y, outerRing[^1].Y, 9);
            Assert.Equal(40.0, record.GetPart(1)[0].X, 9);
        }

        [Fact]
        public void Polyline_z_record_carries_z_values()
        {
            using var reader = ShapefileReader.Open(Fixtures.Path("linesz.shp"));
            var record = reader.ReadRecords().Single();

            Assert.Equal(ShapeType.PolyLineZ, record.ShapeType);
            Assert.All(record.Vertices, vertex => Assert.True(vertex.HasZ));
            Assert.Equal(5.0, record.Vertices[0].Z, 9);
            Assert.Equal(7.5, record.Vertices[1].Z, 9);
            Assert.Equal(2.25, record.Vertices[2].Z, 9);
        }

        [Fact]
        public void Reader_rejects_a_file_with_a_wrong_file_code()
        {
            var bytes = File.ReadAllBytes(Fixtures.Path("points.shp"));
            bytes[3] = 0x00;
            using var stream = new MemoryStream(bytes);

            var exception = Assert.Throws<ShapefileFormatException>(() => ShapefileReader.Open(stream, ownsStream: false));
            Assert.Contains("file code", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Reader_rejects_a_truncated_record()
        {
            var bytes = File.ReadAllBytes(Fixtures.Path("lines.shp"));
            using var stream = new MemoryStream(bytes.AsSpan(0, bytes.Length - 16).ToArray());
            using var reader = ShapefileReader.Open(stream, ownsStream: false);

            Assert.Throws<ShapefileFormatException>(() => reader.ReadRecords().ToList());
        }
    }
}
