#!/usr/bin/env python3
"""Write the binary shapefile fixtures used by the unit tests.

The generator writes plain ESRI shapefile bytes with the standard library only.
Run it from the repository root:

    python3 tools/make_fixtures.py
"""

from __future__ import annotations

import struct
from pathlib import Path

FIXTURE_DIR = Path(__file__).resolve().parent.parent / "tests" / "Import_SHP.Tests" / "fixtures"

SHAPE_TYPE_POINT = 1
SHAPE_TYPE_POLYLINE = 3
SHAPE_TYPE_POLYGON = 5
SHAPE_TYPE_POLYLINE_Z = 13
SHAPE_TYPE_NULL = 0

NO_DATA = -1.0e38


def build_header(file_length_bytes: int, shape_type: int, bounds: tuple[float, float, float, float],
                 z_range: tuple[float, float] = (0.0, 0.0)) -> bytes:
    """Builds the 100 byte main file header."""
    x_min, y_min, x_max, y_max = bounds
    header = struct.pack(">i", 9994)
    header += b"\x00" * 20
    header += struct.pack(">i", file_length_bytes // 2)
    header += struct.pack("<ii", 1000, shape_type)
    header += struct.pack("<4d", x_min, y_min, x_max, y_max)
    header += struct.pack("<2d", *z_range)
    header += struct.pack("<2d", 0.0, 0.0)
    assert len(header) == 100
    return header


def record(number: int, content: bytes) -> bytes:
    """Wraps shape content in a record header."""
    return struct.pack(">ii", number, len(content) // 2) + content


def point_content(x: float, y: float) -> bytes:
    return struct.pack("<i2d", SHAPE_TYPE_POINT, x, y)


def null_content() -> bytes:
    return struct.pack("<i", SHAPE_TYPE_NULL)


def poly_content(shape_type: int, parts: list[list[tuple[float, float]]]) -> bytes:
    points = [point for part in parts for point in part]
    x_values = [p[0] for p in points]
    y_values = [p[1] for p in points]

    part_starts: list[int] = []
    start = 0
    for part in parts:
        part_starts.append(start)
        start += len(part)

    content = struct.pack("<i", shape_type)
    content += struct.pack("<4d", min(x_values), min(y_values), max(x_values), max(y_values))
    content += struct.pack("<ii", len(parts), len(points))
    content += struct.pack(f"<{len(part_starts)}i", *part_starts)
    for x, y in points:
        content += struct.pack("<2d", x, y)
    return content


def poly_z_content(parts: list[list[tuple[float, float, float]]]) -> bytes:
    points = [point for part in parts for point in part]
    x_values = [p[0] for p in points]
    y_values = [p[1] for p in points]
    z_values = [p[2] for p in points]

    part_starts: list[int] = []
    start = 0
    for part in parts:
        part_starts.append(start)
        start += len(part)

    content = struct.pack("<i", SHAPE_TYPE_POLYLINE_Z)
    content += struct.pack("<4d", min(x_values), min(y_values), max(x_values), max(y_values))
    content += struct.pack("<ii", len(parts), len(points))
    content += struct.pack(f"<{len(part_starts)}i", *part_starts)
    for x, y, _ in points:
        content += struct.pack("<2d", x, y)
    content += struct.pack("<2d", min(z_values), max(z_values))
    content += struct.pack(f"<{len(z_values)}d", *z_values)
    content += struct.pack("<2d", NO_DATA, NO_DATA)
    content += struct.pack(f"<{len(points)}d", *([NO_DATA] * len(points)))
    return content


def write_shapefile(name: str, shape_type: int, contents: list[bytes],
                    bounds: tuple[float, float, float, float],
                    z_range: tuple[float, float] = (0.0, 0.0)) -> None:
    body = b"".join(record(i + 1, content) for i, content in enumerate(contents))
    header = build_header(100 + len(body), shape_type, bounds, z_range)
    (FIXTURE_DIR / f"{name}.shp").write_bytes(header + body)


def write_dbf(name: str, fields: list[tuple[str, str, int, int]],
              rows: list[list[str]], deleted_rows: set[int] | None = None,
              language_driver_id: int = 0x57) -> None:
    """Writes a dBase III table. Each field is (name, type code, length, decimal count)."""
    deleted_rows = deleted_rows or set()
    header_length = 32 + (32 * len(fields)) + 1
    record_length = 1 + sum(field[2] for field in fields)

    header = struct.pack("<4B", 0x03, 124, 1, 1)
    header += struct.pack("<i", len(rows))
    header += struct.pack("<hh", header_length, record_length)
    header += b"\x00" * 17
    header += struct.pack("<B", language_driver_id)
    header += b"\x00" * 2

    for field_name, type_code, length, decimals in fields:
        header += field_name.encode("ascii").ljust(11, b"\x00")[:11]
        header += type_code.encode("ascii")
        header += b"\x00" * 4
        header += struct.pack("<BB", length, decimals)
        header += b"\x00" * 14
    header += b"\x0d"

    body = b""
    for index, row in enumerate(rows):
        body += b"*" if index in deleted_rows else b" "
        for value, (_, type_code, length, _decimals) in zip(row, fields):
            encoded = value.encode("latin-1")[:length]
            if type_code in ("N", "F"):
                body += encoded.rjust(length, b" ")
            else:
                body += encoded.ljust(length, b" ")
    body += b"\x1a"

    (FIXTURE_DIR / f"{name}.dbf").write_bytes(header + body)


def make_points() -> None:
    """Three points, one null record, and a matching attribute table."""
    contents = [
        point_content(10.0, 20.0),
        point_content(-5.5, 7.25),
        null_content(),
        point_content(1000.0, 2000.0),
    ]
    write_shapefile("points", SHAPE_TYPE_POINT, contents, (-5.5, 7.25, 1000.0, 2000.0))
    write_dbf(
        "points",
        fields=[("NAME", "C", 10, 0), ("ELEV", "N", 10, 2), ("OPEN", "L", 1, 0), ("SURVEYED", "D", 8, 0)],
        rows=[
            ["alpha", "12.50", "T", "20240115"],
            ["beta", "-3.75", "F", "20240116"],
            ["null one", "0.00", "?", "20240117"],
            ["delta", "104.00", "Y", "20240118"],
        ],
    )
    (FIXTURE_DIR / "points.cpg").write_text("UTF-8\n", encoding="ascii")
    (FIXTURE_DIR / "points.prj").write_text(
        'GEOGCS["GCS_WGS_1984",DATUM["D_WGS_1984",SPHEROID["WGS_1984",6378137.0,298.257223563]],'
        'PRIMEM["Greenwich",0.0],UNIT["Degree",0.0174532925199433]]',
        encoding="ascii",
    )


def make_lines() -> None:
    """One single part line and one two part line."""
    single = [[(0.0, 0.0), (10.0, 0.0), (10.0, 10.0)]]
    multi = [[(0.0, 50.0), (5.0, 50.0)], [(20.0, 50.0), (25.0, 55.0), (30.0, 50.0)]]
    contents = [poly_content(SHAPE_TYPE_POLYLINE, single), poly_content(SHAPE_TYPE_POLYLINE, multi)]
    write_shapefile("lines", SHAPE_TYPE_POLYLINE, contents, (0.0, 0.0, 30.0, 55.0))
    write_dbf(
        "lines",
        fields=[("ROADNAME", "C", 12, 0), ("LANES", "N", 3, 0)],
        rows=[["main", "2"], ["ring", "4"]],
    )


def make_polygons() -> None:
    """One polygon with an outer ring and an inner ring."""
    outer = [(0.0, 0.0), (100.0, 0.0), (100.0, 100.0), (0.0, 100.0), (0.0, 0.0)]
    inner = [(40.0, 40.0), (40.0, 60.0), (60.0, 60.0), (60.0, 40.0), (40.0, 40.0)]
    contents = [poly_content(SHAPE_TYPE_POLYGON, [outer, inner])]
    write_shapefile("polygons", SHAPE_TYPE_POLYGON, contents, (0.0, 0.0, 100.0, 100.0))
    write_dbf("polygons", fields=[("BUILDING", "C", 12, 0)], rows=[["depot"]])


def make_lines_z() -> None:
    """One polyline with Z values."""
    part = [(0.0, 0.0, 5.0), (10.0, 0.0, 7.5), (20.0, 0.0, 2.25)]
    contents = [poly_z_content([part])]
    write_shapefile("linesz", SHAPE_TYPE_POLYLINE_Z, contents, (0.0, 0.0, 20.0, 0.0), (2.25, 7.5))
    write_dbf("linesz", fields=[("PIPEID", "C", 8, 0)], rows=[["p-1"]])


def make_far_from_origin() -> None:
    """Points in a projected coordinate system far away from the world origin."""
    contents = [point_content(500000.0, 4600000.0), point_content(500100.0, 4600200.0)]
    write_shapefile("utm_points", SHAPE_TYPE_POINT, contents, (500000.0, 4600000.0, 500100.0, 4600200.0))
    write_dbf("utm_points", fields=[("SITE", "C", 8, 0)], rows=[["a"], ["b"]])


def make_deleted_rows() -> None:
    """An attribute table with one deleted row."""
    write_dbf(
        "deleted",
        fields=[("LABEL", "C", 6, 0)],
        rows=[["keep"], ["gone"], ["last"]],
        deleted_rows={1},
    )


def main() -> None:
    FIXTURE_DIR.mkdir(parents=True, exist_ok=True)
    make_points()
    make_lines()
    make_polygons()
    make_lines_z()
    make_far_from_origin()
    make_deleted_rows()
    print(f"Fixtures written to {FIXTURE_DIR}")


if __name__ == "__main__":
    main()
