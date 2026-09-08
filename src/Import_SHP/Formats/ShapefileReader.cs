using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace Import_SHP.Formats
{
    /// <summary>The 100 byte header of a main shapefile.</summary>
    public sealed class ShapefileHeader
    {
        public ShapefileHeader(ShapeType shapeType, ShapeBounds bounds, double zMin, double zMax, long fileLengthInBytes)
        {
            ShapeType = shapeType;
            Bounds = bounds;
            ZMin = zMin;
            ZMax = zMax;
            FileLengthInBytes = fileLengthInBytes;
        }

        /// <summary>The shape type of every non null record in the file.</summary>
        public ShapeType ShapeType { get; }

        public ShapeBounds Bounds { get; }

        public double ZMin { get; }

        public double ZMax { get; }

        public long FileLengthInBytes { get; }
    }

    /// <summary>Reads the geometry records of a main shapefile (.shp).</summary>
    public sealed class ShapefileReader : IDisposable
    {
        private const int FileCode = 9994;
        private const int FileVersion = 1000;
        private const int HeaderLengthInBytes = 100;
        private const int RecordHeaderLengthInBytes = 8;

        private readonly Stream _stream;
        private readonly bool _ownsStream;

        private ShapefileReader(Stream stream, bool ownsStream, ShapefileHeader header)
        {
            _stream = stream;
            _ownsStream = ownsStream;
            Header = header;
        }

        public ShapefileHeader Header { get; }

        public static ShapefileReader Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("The shapefile path is empty.", nameof(path));

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                return Open(stream, ownsStream: true);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public static ShapefileReader Open(Stream stream, bool ownsStream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            var header = ReadHeader(stream);
            return new ShapefileReader(stream, ownsStream, header);
        }

        /// <summary>Reads every record in file order. Records of an unsupported type come back with no vertices.</summary>
        public IEnumerable<ShapeRecord> ReadRecords()
        {
            _stream.Seek(HeaderLengthInBytes, SeekOrigin.Begin);
            var recordHeader = new byte[RecordHeaderLengthInBytes];

            while (_stream.Position + RecordHeaderLengthInBytes <= Header.FileLengthInBytes)
            {
                if (!TryReadExactly(_stream, recordHeader, RecordHeaderLengthInBytes))
                    yield break;

                var recordNumber = BinaryPrimitives.ReadInt32BigEndian(recordHeader.AsSpan(0, 4));
                var contentLengthInWords = BinaryPrimitives.ReadInt32BigEndian(recordHeader.AsSpan(4, 4));
                if (contentLengthInWords < 0)
                    throw new ShapefileFormatException($"Record {recordNumber} declares a negative content length.");

                var contentLength = contentLengthInWords * 2;
                var content = new byte[contentLength];
                if (!TryReadExactly(_stream, content, contentLength))
                    throw new ShapefileFormatException($"Record {recordNumber} is shorter than its declared length.");

                yield return ParseRecord(recordNumber, content);
            }
        }

        public void Dispose()
        {
            if (_ownsStream)
                _stream.Dispose();
        }

        private static ShapefileHeader ReadHeader(Stream stream)
        {
            var header = new byte[HeaderLengthInBytes];
            if (!TryReadExactly(stream, header, HeaderLengthInBytes))
                throw new ShapefileFormatException("The file is shorter than the 100 byte shapefile header.");

            var fileCode = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
            if (fileCode != FileCode)
                throw new ShapefileFormatException($"The file code is {fileCode}. A shapefile must start with {FileCode}.");

            var version = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(28, 4));
            if (version != FileVersion)
                throw new ShapefileFormatException($"The shapefile version is {version}. Only version {FileVersion} is supported.");

            var fileLengthInWords = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(24, 4));
            if (fileLengthInWords < HeaderLengthInBytes / 2)
                throw new ShapefileFormatException("The shapefile header declares a file length that is too small.");

            var shapeType = (ShapeType)BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(32, 4));
            var bounds = new ShapeBounds(
                BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(36, 8)),
                BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(44, 8)),
                BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(52, 8)),
                BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(60, 8)));
            var zMin = BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(68, 8));
            var zMax = BinaryPrimitives.ReadDoubleLittleEndian(header.AsSpan(76, 8));

            return new ShapefileHeader(shapeType, bounds, zMin, zMax, (long)fileLengthInWords * 2);
        }

        private static ShapeRecord ParseRecord(int recordNumber, byte[] content)
        {
            var cursor = new ByteCursor(content, 0, content.Length);
            var shapeType = (ShapeType)cursor.ReadInt32();

            switch (ShapeRecord.GetFamily(shapeType))
            {
                case ShapeFamily.Null:
                    return ShapeRecord.CreateNull(recordNumber, shapeType);
                case ShapeFamily.Point:
                    return ParsePoint(recordNumber, shapeType, ref cursor);
                case ShapeFamily.MultiPoint:
                    return ParseMultiPoint(recordNumber, shapeType, ref cursor);
                case ShapeFamily.PolyLine:
                case ShapeFamily.Polygon:
                    return ParsePolyShape(recordNumber, shapeType, ref cursor);
                default:
                    return ShapeRecord.CreateNull(recordNumber, shapeType);
            }
        }

        private static ShapeRecord ParsePoint(int recordNumber, ShapeType shapeType, ref ByteCursor cursor)
        {
            var x = cursor.ReadDouble();
            var y = cursor.ReadDouble();
            var z = ShapeRecord.TypeHasZ(shapeType) ? cursor.ReadDouble() : double.NaN;
            return ShapeRecord.CreatePoint(recordNumber, shapeType, new ShapeVertex(x, y, z));
        }

        private static ShapeRecord ParseMultiPoint(int recordNumber, ShapeType shapeType, ref ByteCursor cursor)
        {
            var bounds = ReadBounds(ref cursor);
            var pointCount = ReadCount(ref cursor, recordNumber, "point");

            var xy = new double[pointCount * 2];
            for (var i = 0; i < xy.Length; i++)
                xy[i] = cursor.ReadDouble();

            var z = ReadOptionalZ(ref cursor, shapeType, pointCount);
            var vertices = BuildVertices(xy, z);
            var parts = new int[pointCount];
            for (var i = 0; i < pointCount; i++)
                parts[i] = i;

            return new ShapeRecord(recordNumber, shapeType, bounds, vertices, parts);
        }

        private static ShapeRecord ParsePolyShape(int recordNumber, ShapeType shapeType, ref ByteCursor cursor)
        {
            var bounds = ReadBounds(ref cursor);
            var partCount = ReadCount(ref cursor, recordNumber, "part");
            var pointCount = ReadCount(ref cursor, recordNumber, "point");

            var parts = new int[partCount];
            for (var i = 0; i < partCount; i++)
            {
                parts[i] = cursor.ReadInt32();
                if (parts[i] < 0 || parts[i] > pointCount)
                    throw new ShapefileFormatException($"Record {recordNumber} has a part index outside the vertex list.");
                if (i > 0 && parts[i] < parts[i - 1])
                    throw new ShapefileFormatException($"Record {recordNumber} has part indexes that do not increase.");
            }

            var xy = new double[pointCount * 2];
            for (var i = 0; i < xy.Length; i++)
                xy[i] = cursor.ReadDouble();

            var z = ReadOptionalZ(ref cursor, shapeType, pointCount);
            var vertices = BuildVertices(xy, z);

            return new ShapeRecord(recordNumber, shapeType, bounds, vertices, parts);
        }

        private static ShapeBounds ReadBounds(ref ByteCursor cursor)
        {
            var xMin = cursor.ReadDouble();
            var yMin = cursor.ReadDouble();
            var xMax = cursor.ReadDouble();
            var yMax = cursor.ReadDouble();
            return new ShapeBounds(xMin, yMin, xMax, yMax);
        }

        private static int ReadCount(ref ByteCursor cursor, int recordNumber, string itemName)
        {
            var count = cursor.ReadInt32();
            if (count < 0)
                throw new ShapefileFormatException($"Record {recordNumber} declares a negative {itemName} count.");
            return count;
        }

        /// <summary>Reads the Z range and the Z array of a Z shape type. Returns null for the other types.</summary>
        private static double[]? ReadOptionalZ(ref ByteCursor cursor, ShapeType shapeType, int pointCount)
        {
            if (!ShapeRecord.TypeHasZ(shapeType))
                return null;

            // Z range (2 doubles) then one Z value for each point.
            var neededBytes = (2 + pointCount) * sizeof(double);
            if (!cursor.CanRead(neededBytes))
                return null;

            cursor.Skip(2 * sizeof(double));
            var z = new double[pointCount];
            for (var i = 0; i < pointCount; i++)
                z[i] = cursor.ReadDouble();
            return z;
        }

        private static ShapeVertex[] BuildVertices(double[] xy, double[]? z)
        {
            var count = xy.Length / 2;
            var vertices = new ShapeVertex[count];
            for (var i = 0; i < count; i++)
                vertices[i] = new ShapeVertex(xy[i * 2], xy[(i * 2) + 1], z is null ? double.NaN : z[i]);
            return vertices;
        }

        private static bool TryReadExactly(Stream stream, byte[] buffer, int count)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    return false;
                offset += read;
            }

            return true;
        }
    }
}
