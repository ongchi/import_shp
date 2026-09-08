using System;
using System.Collections.Generic;

namespace Import_SHP.Formats
{
    /// <summary>Shape type codes defined by the ESRI shapefile specification.</summary>
    public enum ShapeType
    {
        Null = 0,
        Point = 1,
        PolyLine = 3,
        Polygon = 5,
        MultiPoint = 8,
        PointZ = 11,
        PolyLineZ = 13,
        PolygonZ = 15,
        MultiPointZ = 18,
        PointM = 21,
        PolyLineM = 23,
        PolygonM = 25,
        MultiPointM = 28,
        MultiPatch = 31,
    }

    /// <summary>The geometry family that a shape type belongs to.</summary>
    public enum ShapeFamily
    {
        Null,
        Point,
        MultiPoint,
        PolyLine,
        Polygon,
        Unsupported,
    }

    /// <summary>One shapefile vertex. <see cref="Z"/> is <see cref="double.NaN"/> when the file has no Z values.</summary>
    public readonly struct ShapeVertex
    {
        public ShapeVertex(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public bool HasZ => !double.IsNaN(Z);
    }

    /// <summary>An axis aligned bounding box in shapefile coordinates.</summary>
    public readonly struct ShapeBounds
    {
        public ShapeBounds(double xMin, double yMin, double xMax, double yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public double XMin { get; }
        public double YMin { get; }
        public double XMax { get; }
        public double YMax { get; }

        public double CenterX => (XMin + XMax) / 2.0;
        public double CenterY => (YMin + YMax) / 2.0;

        public bool IsEmpty => XMin > XMax || YMin > YMax;
    }

    /// <summary>One record of the main shapefile. Point records hold a single vertex and a single part.</summary>
    public sealed class ShapeRecord
    {
        private static readonly int[] SinglePart = { 0 };

        public ShapeRecord(
            int recordNumber,
            ShapeType shapeType,
            ShapeBounds bounds,
            IReadOnlyList<ShapeVertex> vertices,
            IReadOnlyList<int> partStartIndexes)
        {
            RecordNumber = recordNumber;
            ShapeType = shapeType;
            Bounds = bounds;
            Vertices = vertices;
            PartStartIndexes = partStartIndexes;
        }

        /// <summary>The one based record number written in the record header.</summary>
        public int RecordNumber { get; }

        public ShapeType ShapeType { get; }

        public ShapeBounds Bounds { get; }

        public IReadOnlyList<ShapeVertex> Vertices { get; }

        /// <summary>Index into <see cref="Vertices"/> of the first vertex of each part.</summary>
        public IReadOnlyList<int> PartStartIndexes { get; }

        public ShapeFamily Family => GetFamily(ShapeType);

        public int PartCount => PartStartIndexes.Count;

        /// <summary>Returns the vertices of one part.</summary>
        public IReadOnlyList<ShapeVertex> GetPart(int partIndex)
        {
            if (partIndex < 0 || partIndex >= PartStartIndexes.Count)
                throw new ArgumentOutOfRangeException(nameof(partIndex));

            var start = PartStartIndexes[partIndex];
            var end = partIndex + 1 < PartStartIndexes.Count ? PartStartIndexes[partIndex + 1] : Vertices.Count;
            var part = new ShapeVertex[Math.Max(0, end - start)];
            for (var i = 0; i < part.Length; i++)
                part[i] = Vertices[start + i];
            return part;
        }

        public static ShapeRecord CreateNull(int recordNumber, ShapeType shapeType)
        {
            return new ShapeRecord(
                recordNumber,
                shapeType,
                new ShapeBounds(1, 1, -1, -1),
                Array.Empty<ShapeVertex>(),
                Array.Empty<int>());
        }

        public static ShapeRecord CreatePoint(int recordNumber, ShapeType shapeType, ShapeVertex vertex)
        {
            var bounds = new ShapeBounds(vertex.X, vertex.Y, vertex.X, vertex.Y);
            return new ShapeRecord(recordNumber, shapeType, bounds, new[] { vertex }, SinglePart);
        }

        public static ShapeFamily GetFamily(ShapeType shapeType)
        {
            switch (shapeType)
            {
                case ShapeType.Null:
                    return ShapeFamily.Null;
                case ShapeType.Point:
                case ShapeType.PointZ:
                case ShapeType.PointM:
                    return ShapeFamily.Point;
                case ShapeType.MultiPoint:
                case ShapeType.MultiPointZ:
                case ShapeType.MultiPointM:
                    return ShapeFamily.MultiPoint;
                case ShapeType.PolyLine:
                case ShapeType.PolyLineZ:
                case ShapeType.PolyLineM:
                    return ShapeFamily.PolyLine;
                case ShapeType.Polygon:
                case ShapeType.PolygonZ:
                case ShapeType.PolygonM:
                    return ShapeFamily.Polygon;
                default:
                    return ShapeFamily.Unsupported;
            }
        }

        /// <summary>True when the shape type stores Z values.</summary>
        public static bool TypeHasZ(ShapeType shapeType)
        {
            return shapeType == ShapeType.PointZ
                || shapeType == ShapeType.MultiPointZ
                || shapeType == ShapeType.PolyLineZ
                || shapeType == ShapeType.PolygonZ;
        }
    }
}
