using Import_SHP.Gdal;
using Xunit;

namespace Import_SHP.Tests
{
    public class DetectedCrsTests
    {
        [Fact]
        public void Epsg_parser_reads_the_code_between_blank_lines()
        {
            Assert.Equal("EPSG:32633", GdalSrsInfo.ParseEpsgCode("\n\nEPSG:32633\n\n"));
        }

        [Fact]
        public void Epsg_parser_reads_the_first_of_two_codes()
        {
            Assert.Equal("EPSG:4326", GdalSrsInfo.ParseEpsgCode("EPSG:4326\nEPSG:4979\n"));
        }

        [Fact]
        public void Epsg_parser_gives_no_code_when_gdal_finds_no_match()
        {
            // GDAL writes the code -1 for a CRS that matches no EPSG entry.
            Assert.Null(GdalSrsInfo.ParseEpsgCode("\n\nEPSG:-1\n\n"));
        }

        [Fact]
        public void Epsg_parser_gives_no_code_for_an_empty_output()
        {
            Assert.Null(GdalSrsInfo.ParseEpsgCode(string.Empty));
        }

        [Theory]
        [InlineData("PROJCS[\"WGS_1984_UTM_Zone_33N\",GEOGCS[\"GCS_WGS_1984\"]]", "WGS_1984_UTM_Zone_33N")]
        [InlineData("GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\"]]", "GCS_WGS_1984")]
        [InlineData("PROJCRS[\"WGS 84 / UTM zone 33N\",\n    ID[\"EPSG\",32633]]", "WGS 84 / UTM zone 33N")]
        public void Name_parser_reads_the_first_quoted_text(string wkt, string expectedName)
        {
            Assert.Equal(expectedName, DetectedCrs.ParseName(wkt));
        }

        [Fact]
        public void Name_parser_gives_no_name_for_a_text_without_quotes()
        {
            Assert.Null(DetectedCrs.ParseName("+proj=utm +zone=33"));
        }

        [Fact]
        public void Display_text_prefers_the_code_to_the_name()
        {
            var detected = new DetectedCrs("EPSG:32633", "PROJCRS[\"WGS 84 / UTM zone 33N\"]");

            Assert.Equal("EPSG:32633", detected.DisplayText);
        }

        [Fact]
        public void Display_text_falls_back_to_the_name()
        {
            var detected = new DetectedCrs(null, "PROJCRS[\"Local grid\"]");

            Assert.Equal("Local grid", detected.DisplayText);
        }

        [Fact]
        public void File_without_a_crs_is_not_known()
        {
            Assert.False(DetectedCrs.None.IsKnown);
            Assert.Equal(string.Empty, DetectedCrs.None.DisplayText);
        }

        [Theory]
        [InlineData("GEOGCRS[\"WGS 84\"]", true)]
        [InlineData("GEOGCS[\"GCS_WGS_1984\"]", true)]
        [InlineData("PROJCRS[\"WGS 84 / UTM zone 33N\"]", false)]
        [InlineData(null, false)]
        public void Geographic_crs_is_recognized_by_its_wkt_keyword(string? wkt, bool expected)
        {
            Assert.Equal(expected, DetectedCrs.IsGeographic(wkt));
        }
    }
}
