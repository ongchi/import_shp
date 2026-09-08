using System.Linq;
using System.Text;
using Import_SHP.Formats;
using Xunit;

namespace Import_SHP.Tests
{
    public class DbfReaderTests
    {
        [Fact]
        public void Table_reports_its_fields_in_file_order()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));

            Assert.Equal(new[] { "NAME", "ELEV", "OPEN", "SURVEYED" }, table.Fields.Select(field => field.Name));
            Assert.Equal('C', table.Fields[0].TypeCode);
            Assert.Equal(10, table.Fields[1].Length);
            Assert.Equal(2, table.Fields[1].DecimalCount);
            Assert.True(table.Fields[1].IsNumeric);
        }

        [Fact]
        public void Records_hold_trimmed_field_text()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var records = table.ReadRecords().ToList();

            Assert.Equal(4, records.Count);
            Assert.Equal("alpha", records[0]["NAME"]);
            Assert.Equal("12.50", records[0]["ELEV"]);
            Assert.Equal("delta", records[3]["NAME"]);
        }

        [Fact]
        public void Field_lookup_ignores_case()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var record = table.ReadRecords().First();

            Assert.Equal("alpha", record["name"]);
        }

        [Fact]
        public void Missing_field_reads_as_empty_text()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var record = table.ReadRecords().First();

            Assert.Equal(string.Empty, record["NO_SUCH_FIELD"]);
        }

        [Fact]
        public void Logical_field_becomes_true_or_false()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var records = table.ReadRecords().ToList();

            Assert.Equal("true", records[0]["OPEN"]);
            Assert.Equal("false", records[1]["OPEN"]);
            Assert.Equal(string.Empty, records[2]["OPEN"]);
            Assert.Equal("true", records[3]["OPEN"]);
        }

        [Fact]
        public void Date_field_becomes_an_iso_date()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var record = table.ReadRecords().First();

            Assert.Equal("2024-01-15", record["SURVEYED"]);
        }

        [Fact]
        public void Numeric_field_reads_as_a_number()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var records = table.ReadRecords().ToList();

            Assert.True(records[1].TryGetDouble("ELEV", out var value));
            Assert.Equal(-3.75, value, 9);
        }

        [Fact]
        public void Text_field_does_not_read_as_a_number()
        {
            using var table = DbfTable.Open(Fixtures.Path("points.dbf"));
            var record = table.ReadRecords().First();

            Assert.False(record.TryGetDouble("NAME", out _));
        }

        [Fact]
        public void Deleted_records_keep_their_position_and_carry_the_flag()
        {
            using var table = DbfTable.Open(Fixtures.Path("deleted.dbf"));
            var records = table.ReadRecords().ToList();

            Assert.Equal(3, records.Count);
            Assert.False(records[0].IsDeleted);
            Assert.True(records[1].IsDeleted);
            Assert.False(records[2].IsDeleted);
            Assert.Equal("last", records[2]["LABEL"]);
        }

        [Fact]
        public void Code_page_file_selects_the_encoding()
        {
            var sidecars = SidecarFiles.Find(Fixtures.Path("points.shp"));

            Assert.NotNull(sidecars.CodePagePath);
            Assert.Equal(Encoding.UTF8.CodePage, sidecars.ReadCodePage()!.CodePage);
        }

        [Fact]
        public void Missing_code_page_file_reads_as_null()
        {
            var sidecars = SidecarFiles.Find(Fixtures.Path("lines.shp"));

            Assert.Null(sidecars.CodePagePath);
            Assert.Null(sidecars.ReadCodePage());
        }

        [Fact]
        public void Sidecar_search_finds_the_attribute_and_projection_files()
        {
            var sidecars = SidecarFiles.Find(Fixtures.Path("points.shp"));

            Assert.NotNull(sidecars.AttributePath);
            Assert.NotNull(sidecars.ProjectionPath);
            Assert.Contains("GCS_WGS_1984", sidecars.ReadProjectionText());
        }
    }
}
