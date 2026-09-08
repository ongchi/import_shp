#! python 3
"""Runs the import pipeline inside Rhino and writes the result to a report file.

Open this file in the Rhino Script Editor (Tools > Script Editor) and press Run.
Build the plugin in Release first, because the script loads the built assembly.

The script imports every fixture into the active document and checks the object counts,
the attributes, the elevations and the offset. It deletes the objects of the active
document before each case, so run it in an empty model.
"""

import os
import shutil
import sys
import traceback

ROOT_DIR = "/Users/chii/Documents/Projects/Rhino3D/import_shp"
PLUGIN_PATH = os.path.join(ROOT_DIR, "src", "Import_SHP", "bin", "Release", "net7.0", "Import_SHP.rhp")
FIXTURE_DIR = os.path.join(ROOT_DIR, "tests", "Import_SHP.Tests", "fixtures")
REPORT_PATH = os.path.join(ROOT_DIR, "rhino_test_report.txt")

RESULTS = []


def write_report(lines):
    with open(REPORT_PATH, "w", encoding="utf-8") as report_file:
        report_file.write("\n".join(lines))


try:
    import clr

    # pythonnet resolves assemblies by the .dll extension, so load a copy of the plugin.
    _assembly_copy = os.path.join(ROOT_DIR, "src", "Import_SHP", "bin", "Release", "net7.0", "Import_SHP.dll")
    shutil.copyfile(PLUGIN_PATH, _assembly_copy)
    clr.AddReference(_assembly_copy)

    import Rhino
    from Import_SHP.Import import ImportOptions, ImportOptionsResolver, ShapeImporter, ShapefileSummary, ZSource
except Exception:
    write_report(["FAIL  load the plugin assembly", traceback.format_exc()])
    raise


def check(name, condition, detail=""):
    RESULTS.append(("PASS" if condition else "FAIL", name, detail))


def fixture(name):
    return os.path.join(FIXTURE_DIR, name)


def new_document():
    """Deletes every object and layer of the active document so each case starts clean."""
    doc = Rhino.RhinoDoc.ActiveDoc
    doc.Objects.Clear()
    doc.Strings.Delete("Import_SHP.Offset")
    return doc


def import_file(doc, file_name, options):
    importer = ShapeImporter(doc.ModelAbsoluteTolerance)
    return importer.Import(doc, fixture(file_name), options)


def default_options(doc, file_name):
    summary = ShapefileSummary.Read(fixture(file_name))
    return summary, ImportOptionsResolver.CreateDefaults(doc, summary)


def test_points():
    doc = new_document()
    summary, options = default_options(doc, "points.shp")
    options.NameFieldName = "NAME"
    report = import_file(doc, "points.shp", options)

    check("points: three point objects", report.PointCount == 3, f"count={report.PointCount}")
    check("points: null record skipped", report.SkippedNullCount == 1, f"count={report.SkippedNullCount}")
    check("points: four records read", report.RecordCount == 4, f"count={report.RecordCount}")

    objects = list(doc.Objects)
    check("points: objects added to the document", len(objects) == 3, f"count={len(objects)}")

    first = objects[0]
    check("points: object name from the field", first.Attributes.Name == "alpha", f"name={first.Attributes.Name}")
    check("points: attribute user text", first.Attributes.GetUserString("ELEV") == "12.50",
          f"elev={first.Attributes.GetUserString('ELEV')}")
    check("points: date field as iso text", first.Attributes.GetUserString("SURVEYED") == "2024-01-15",
          f"date={first.Attributes.GetUserString('SURVEYED')}")
    check("points: logical field as text", first.Attributes.GetUserString("OPEN") == "true",
          f"open={first.Attributes.GetUserString('OPEN')}")

    location = first.Geometry.Location
    check("points: coordinates kept", abs(location.X - 10.0) < 1e-9 and abs(location.Y - 20.0) < 1e-9,
          f"point={location}")
    check("points: flat file lands on z zero", abs(location.Z) < 1e-9, f"z={location.Z}")

    layer = doc.Layers.FindName("points", 0)
    check("points: layer named after the file", layer is not None)
    if layer is not None:
        projection = layer.GetUserString("Import_SHP.Projection")
        check("points: projection text stored on the layer", projection is not None and "GCS_WGS_1984" in projection,
              f"prj={projection}")


def test_lines():
    doc = new_document()
    _, options = default_options(doc, "lines.shp")
    report = import_file(doc, "lines.shp", options)

    check("lines: three curves for three parts", report.CurveCount == 3, f"count={report.CurveCount}")

    objects = list(doc.Objects)
    groups = {o.Attributes.GetGroupList()[0] for o in objects if o.Attributes.GroupCount > 0}
    check("lines: the two part record is grouped", len(groups) == 1, f"groups={groups}")

    grouped = [o for o in objects if o.Attributes.GroupCount > 0]
    check("lines: both parts of the record are in the group", len(grouped) == 2, f"count={len(grouped)}")
    check("lines: attributes on every curve",
          all(o.Attributes.GetUserString("ROADNAME") in ("main", "ring") for o in objects))


def test_polygons():
    doc = new_document()
    _, options = default_options(doc, "polygons.shp")
    report = import_file(doc, "polygons.shp", options)

    check("polygons: one curve for each ring", report.CurveCount == 2, f"count={report.CurveCount}")
    closed = [o for o in doc.Objects if o.Geometry.IsClosed]
    check("polygons: both rings are closed", len(closed) == 2, f"count={len(closed)}")


def test_polyline_z():
    doc = new_document()
    _, options = default_options(doc, "linesz.shp")
    report = import_file(doc, "linesz.shp", options)

    check("linesz: one curve", report.CurveCount == 1, f"count={report.CurveCount}")
    curve = list(doc.Objects)[0].Geometry
    polyline = curve.ToPolyline()
    check("linesz: z values from the shape",
          abs(polyline[0].Z - 5.0) < 1e-9 and abs(polyline[1].Z - 7.5) < 1e-9 and abs(polyline[2].Z - 2.25) < 1e-9,
          f"z={[p.Z for p in polyline]}")


def test_z_from_attribute_field():
    doc = new_document()
    _, options = default_options(doc, "points.shp")
    options.ZSource = ZSource.AttributeField
    options.ZFieldName = "ELEV"
    import_file(doc, "points.shp", options)

    elevations = sorted(round(o.Geometry.Location.Z, 3) for o in doc.Objects)
    check("z field: elevation from the ELEV field", elevations == [-3.75, 12.5, 104.0], f"z={elevations}")


def test_constant_z():
    doc = new_document()
    _, options = default_options(doc, "points.shp")
    options.ZSource = ZSource.Constant
    options.ConstantZ = 42.0
    import_file(doc, "points.shp", options)

    check("constant z: every object at the constant elevation",
          all(abs(o.Geometry.Location.Z - 42.0) < 1e-9 for o in doc.Objects))


def test_offset():
    doc = new_document()
    summary, options = default_options(doc, "utm_points.shp")

    check("offset: proposed for far coordinates", options.ApplyOffset, f"apply={options.ApplyOffset}")
    check("offset: rounded to the nearest thousand",
          abs(options.Offset.X + 500000.0) < 1e-9 and abs(options.Offset.Y + 4600000.0) < 1e-9,
          f"offset={options.Offset}")

    import_file(doc, "utm_points.shp", options)
    locations = [o.Geometry.Location for o in doc.Objects]
    check("offset: geometry moved near the origin",
          all(abs(p.X) < 1000 and abs(p.Y) < 1000 for p in locations), f"points={locations}")

    stored = doc.Strings.GetValue("Import_SHP.Offset")
    check("offset: stored in the document user text", stored is not None and stored.startswith("-500000"),
          f"stored={stored}")

    # A second file must reuse the offset of the document.
    _, second_options = default_options(doc, "points.shp")
    check("offset: reused by the next import",
          second_options.ApplyOffset and abs(second_options.Offset.X + 500000.0) < 1e-9,
          f"offset={second_options.Offset}")


def test_missing_file():
    doc = new_document()
    try:
        options = ImportOptions()
        options.LayerName = "missing"
        import_file(doc, "does_not_exist.shp", options)
        check("missing file: raises an error", False, "no exception")
    except Exception as error:  # noqa: BLE001 - the test only needs the failure
        check("missing file: raises an error", True, type(error).__name__)


def main():
    for test in (test_points, test_lines, test_polygons, test_polyline_z, test_z_from_attribute_field,
                 test_constant_z, test_offset, test_missing_file):
        try:
            test()
        except Exception as error:  # noqa: BLE001 - report the failure and continue
            RESULTS.append(("FAIL", test.__name__, f"{type(error).__name__}: {error}"))

    failures = [row for row in RESULTS if row[0] == "FAIL"]
    lines = [f"{status}  {name}  {detail}" for status, name, detail in RESULTS]
    lines.append("")
    lines.append(f"{len(RESULTS) - len(failures)} passed, {len(failures)} failed")

    write_report(lines)
    print("\n".join(lines))
    return 0 if not failures else 1


try:
    main()
except Exception:
    write_report(["FAIL  run the tests", traceback.format_exc()])
    raise
