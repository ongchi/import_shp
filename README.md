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
  as user text with the key `Import_SHP.Projection`.

## Coordinates far from the origin

Rhino loses accuracy when geometry sits far from the world origin, and projected
coordinate systems such as UTM give coordinates of millions of units. When the center
of the data is more than 100000 units from the origin, the plugin proposes an offset
that moves the data near the origin. The offset is rounded to the nearest 1000.

The plugin writes the offset it applied into the document user text with the key
`Import_SHP.Offset`. The next import into the same document reuses that offset, so
every file lands in the same place.

## Limits

- The plugin does not change the coordinate system. The `.prj` text is stored only.
- The plugin does not write shapefiles.
- MultiPatch records (type 31) are not supported.
- Polygons import as closed curves, not as surfaces or hatches.
- The shapefile format holds no unit. The plugin uses the coordinate values as they
  are, in the model units of the document.

## Repository layout

```
build.sh                        build and test
src/Import_SHP/
  Import_SHPPlugIn.cs            the import file type
  ImportShapefileCommand.cs     the ImportShapefile command
  Formats/                      the file readers, without any Rhino dependency
  Import/                       the conversion to Rhino objects
  UI/                           the options dialog and the command line options
tests/Import_SHP.Tests/          the reader tests, run without Rhino
tools/install_mac.sh            copies the plugin into the Rhino plugin folder
tools/make_fixtures.py          writes the binary test fixtures
tools/rhino_import_test.py      runs the import inside Rhino and checks the result
```

## Tests

`dotnet test` covers the readers of the `.shp`, `.dbf` and `.cpg` files. Those tests
need no Rhino installation.

The conversion to Rhino objects needs a running Rhino. Rhino runs one instance at a
time, so run the test from inside Rhino:

1. Build in Release. The test loads the built assembly.
2. Open **Tools > Script Editor** in Rhino.
3. Open `tools/rhino_import_test.py` and press **Run**.

The script imports every fixture into the active document and writes
`rhino_test_report.txt` with one line for each check. The script clears the objects
of the active document, so run it in an empty model.
