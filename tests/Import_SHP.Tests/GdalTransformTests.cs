using System.Linq;
using Import_SHP.Gdal;
using Xunit;

namespace Import_SHP.Tests
{
    public class GdalTransformTests
    {
        [Fact]
        public void Arguments_name_the_two_crs_and_ask_for_x_and_y_only()
        {
            var arguments = GdalTransform.BuildArguments(" EPSG:4326 ", "EPSG:25832");

            Assert.Equal(new[] { "-s_srs", "EPSG:4326", "-t_srs", "EPSG:25832", "-output_xy" }, arguments.ToArray());
        }

        [Fact]
        public void Input_holds_one_line_for_each_coordinate_with_every_digit()
        {
            var input = GdalTransform.FormatInput(new[] { new Coordinate(10.5, 50.25), new Coordinate(0.1, -0.2) });

            Assert.Equal("10.5 50.25\n0.1 -0.2\n", input);
        }

        [Fact]
        public void Output_lines_become_coordinates_in_the_same_order()
        {
            var coordinates = GdalTransform.ParseOutput("571666.447504128 5539109.81517567\n500000 0\n", 2);

            Assert.Equal(571666.447504128, coordinates[0]!.Value.X, 9);
            Assert.Equal(5539109.81517567, coordinates[0]!.Value.Y, 8);
            Assert.Equal(500000.0, coordinates[1]!.Value.X);
            Assert.Equal(0.0, coordinates[1]!.Value.Y);
        }

        [Fact]
        public void Failed_translation_gives_no_coordinate_and_keeps_the_order()
        {
            var coordinates = GdalTransform.ParseOutput("1 2\ntransformation failed.\n3 4\n", 3);

            Assert.NotNull(coordinates[0]);
            Assert.Null(coordinates[1]);
            Assert.Equal(3.0, coordinates[2]!.Value.X);
        }

        [Fact]
        public void Infinite_value_counts_as_a_failed_translation()
        {
            var coordinates = GdalTransform.ParseOutput("inf inf\n", 1);

            Assert.Null(coordinates[0]);
        }

        [Fact]
        public void Wrong_line_count_is_refused()
        {
            var exception = Assert.Throws<GdalFailureException>(() => GdalTransform.ParseOutput("1 2\n", 2));

            Assert.Contains("1 coordinates for 2 coordinates", exception.Message);
        }

        [Fact]
        public void Line_without_two_numbers_is_refused()
        {
            Assert.Throws<GdalFailureException>(() => GdalTransform.ParseOutput("ERROR 1: something\n", 1));
        }
    }
}
