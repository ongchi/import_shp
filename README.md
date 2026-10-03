# Import_SHP — ESRI shapefile import for Rhino 8

A Rhino 8 plugin that reads ESRI shapefiles. The plugin adds the shapefile format to
**File > Import** and gives the command `ImportShapefile` to scripts.

## Build

The build needs the .NET SDK 8. The plugin targets `net7.0`, the plugin framework of Rhino 8.

```bash
./build.sh            # build in Release and run the unit tests
./build.sh Debug      # build in Debug
```

The build writes `src/Import_SHP/bin/Release/net7.0/Import_SHP.rhp`.

Set the `DOTNET` variable when the SDK is not on the PATH:

```bash
DOTNET=/run/current-system/sw/bin/dotnet ./build.sh
```

## Install

```bash
./tools/install_mac.sh
```

The script copies `Import_SHP.rhp` into the plugin folder of the current user:

```
~/Library/Application Support/McNeel/Rhinoceros/MacPlugIns/
```

Restart Rhino 8 after the copy. Rhino reads that folder at startup.

The plugin is one file. It needs no companion file, because RhinoCommon is part of
the Rhino process.

## Use

**From the import dialog:** select **File > Import**, then select a `.shp` file.
The options dialog opens before the import.

**From the command line:** run `ImportShapefile`. The dash form `-ImportShapefile`
runs without the dialog and asks for the path and the options on the command line,
which lets a script set every option.

### Options

| Option | Function |
| --- | --- |
| Layer | The layer that receives the objects. The default is the file name. |
| Elevation | `ShapeZ` uses the Z values of the shapes, `AttributeField` uses a numeric field, `Constant` uses one elevation. |
| ElevationField | The numeric field that holds the elevation. |
| ConstantElevation | The elevation for the `Constant` source. |
| NameField | The field that gives each object its name. |
| SourceCRS | The coordinate reference system of the file. The default is the CRS that the plugin detects in the `.prj` file. See [Coordinate reference system](#coordinate-reference-system). |
| TargetCRS | The coordinate reference system of the result. The default is empty, which translates nothing. |
| ModelUnits | The unit of the source coordinates. The import scales X, Y and Z to the model unit of the document. |
| LayoutUnits | The unit of the source coordinates for a layout. See [Units](#units). |
| MoveToOrigin | Moves the data near the world origin. |
| OffsetX, OffsetY | The translation added to every coordinate. |
| GroupParts | Groups the parts of one multi part record. |

## What the plugin imports

| Shapefile record | Rhino object |
| --- | --- |
| Point, PointZ, PointM | Point object |
| MultiPoint | One point for each vertex, one group for each record |
| PolyLine, PolyLineZ, PolyLineM | One curve for each part, one group for each multi part record |
| Polygon, PolygonZ, PolygonM | One closed curve for each ring, one group for each record |

Null records are skipped and counted. MultiPatch records are skipped with a warning.

The plugin reads these files of the shapefile set:

- `.shp` — the geometry
- `.dbf` — the attributes. Every field becomes object user text: the key is the field
  name, the value is the field text. Dates become `YYYY-MM-DD`, logical fields become
  `true` or `false`.
- `.cpg` — the text encoding of the attributes. Without this file the plugin uses the
  code page of the `.dbf` header, and Latin-1 when that code page is unknown.
- `.prj` — the coordinate system text. The plugin stores the text on the import layer
  as user text with the key `Import_SHP.Projection`. When a CRS translation ran, the
  layer holds the text of the target CRS.

## Coordinate reference system

`SourceCRS` (from) and `TargetCRS` (to) translate the coordinates from one coordinate
reference system to another. Both options accept each text that GDAL accepts: an EPSG
code such as `EPSG:25833`, a WKT, or a PROJ string.

**This function needs GDAL**, for example from `brew install gdal`. The plugin searches
the `PATH` and the usual install folders. An import with an empty `TargetCRS` needs no
GDAL.

- **SourceCRS** — the plugin reads the `.prj` file and runs `gdalsrsinfo` to find its EPSG
  code. The code is the default value. When no code matches, or when the machine has no
  GDAL, the field is empty and shows the CRS name of the `.prj` file, and the import uses
  the `.prj` file. A text that you type replaces the `.prj` file.
- **TargetCRS** — empty by default. An empty target translates nothing, and `SourceCRS`
  then has no effect. On the command line, the text `None` clears a CRS.

The EPSG code is a match, not a proof. While `SourceCRS` holds the detected code, the
import therefore reads the CRS from the `.prj` file itself.

`gdaltransform` translates the vertices. The plugin keeps its own readers, so the
attributes stay as they are. The dialog has an **Apply CRS** button, which examines the
two texts and shows the new offset before the import runs.

The order of the transforms is:

```
document point = translate(source point) x model unit scale + offset
```

- The translation is horizontal only. The elevation keeps its value.
- `ModelUnits` states the unit of the **target** CRS when a translation runs.
- A record with a vertex that the target CRS cannot represent is skipped and counted.
- A geographic target CRS gives X and Y in degrees. The import permits it and writes a
  warning.
- A shapefile with no `.prj` file needs a `SourceCRS` text.

## Units

The shapefile format holds no unit. `ModelUnits` states the unit of the source
coordinates, and the import scales X, Y and Z from that unit to the model unit of the
document. The default is "Same as the document", which keeps the coordinate values as
they are. The elevation takes the same scale, whatever its source: the Z values of the
shapes, an attribute field or the constant elevation.

The order of the transforms is the scale first, then the offset:

```
document point = source point x model unit scale + offset
```

The offset comes last because the document keeps it in document units. The dialog and the
command line therefore move the proposed offset when the model unit changes, unless an
earlier import already fixed the offset.

`LayoutUnits` states the unit for a layout, and its factor goes to the layout unit of the
document. **The command imports into the model space only, so this option scales no
geometry today.** The report prints its factor. The option becomes live when the plugin
can import into a layout.

## Coordinates far from the origin

Rhino loses accuracy when geometry sits far from the world origin, and projected
coordinate systems such as UTM give coordinates of millions of units. When the center
of the data is more than 100000 units from the origin, the plugin proposes an offset
that moves the data near the origin. The offset is rounded to the nearest 1000.

The plugin writes the offset it applied into the document user text with the key
`Import_SHP.Offset`. The next import into the same document reuses that offset, so
every file lands in the same place.

## Limits

- The CRS translation needs GDAL on the machine. It is horizontal only, so it does not
  translate the elevation between vertical datums.
- The document does not remember the target CRS. Each import states it.
- The plugin does not write shapefiles.
- MultiPatch records (type 31) are not supported.
- Polygons import as closed curves, not as surfaces or hatches.
- The shapefile format holds no unit. The `ModelUnits` option states it.
- The plugin imports into the model space only, so `LayoutUnits` scales no geometry
  today.

## Repository layout

```
build.sh                        build and test
src/Import_SHP/
  Import_SHPPlugIn.cs            the import file type
  ImportShapefileCommand.cs     the ImportShapefile command
  Formats/                      the file readers, without any Rhino dependency
  Gdal/                         the GDAL tools for the CRS, without any Rhino dependency
  Import/                       the conversion to Rhino objects
  UI/                           the options dialog and the command line options
tests/Import_SHP.Tests/          the reader tests, run without Rhino
tools/install_mac.sh            copies the plugin into the Rhino plugin folder
tools/make_fixtures.py          writes the binary test fixtures
tools/rhino_import_test.py      runs the import inside Rhino and checks the result
```

## Tests

`dotnet test` covers the readers of the `.shp`, `.dbf` and `.cpg` files, the
`gdaltransform` input and output, and the CRS detection. Those tests need no Rhino
installation and no GDAL.

The conversion to Rhino objects needs a running Rhino. Rhino runs one instance at a
time, so run the test from inside Rhino:

1. Build in Release. The test loads the built assembly.
2. Open **Tools > Script Editor** in Rhino.
3. Open `tools/rhino_import_test.py` and press **Run**.

The script imports every fixture into the active document and writes
`rhino_test_report.txt` with one line for each check. The script clears the objects
of the active document, so run it in an empty model.
