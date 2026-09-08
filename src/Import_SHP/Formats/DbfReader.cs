using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Import_SHP.Formats
{
    /// <summary>One column of a dBase table.</summary>
    public sealed class DbfField
    {
        public DbfField(string name, char typeCode, int length, int decimalCount)
        {
            Name = name;
            TypeCode = typeCode;
            Length = length;
            DecimalCount = decimalCount;
        }

        public string Name { get; }

        /// <summary>The dBase type character: C, N, F, D, L, M and others.</summary>
        public char TypeCode { get; }

        public int Length { get; }

        public int DecimalCount { get; }

        /// <summary>True for the field types that hold a number.</summary>
        public bool IsNumeric => TypeCode == 'N' || TypeCode == 'F';
    }

    /// <summary>One row of a dBase table. The values keep the field order of the table.</summary>
    public sealed class DbfRecord
    {
        private readonly DbfTable _table;
        private readonly string[] _values;

        internal DbfRecord(DbfTable table, string[] values, bool isDeleted)
        {
            _table = table;
            _values = values;
            IsDeleted = isDeleted;
        }

        public IReadOnlyList<string> Values => _values;

        /// <summary>True when the row carries the dBase deletion marker.</summary>
        public bool IsDeleted { get; }

        public string this[string fieldName] => GetValue(fieldName);

        public string GetValue(string fieldName)
        {
            var index = _table.IndexOfField(fieldName);
            return index < 0 ? string.Empty : _values[index];
        }

        /// <summary>Reads a field as a number. Returns false when the field is missing or empty.</summary>
        public bool TryGetDouble(string fieldName, out double value)
        {
            value = 0.0;
            var text = GetValue(fieldName);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>Reads the attribute table (.dbf) of a shapefile.</summary>
    public sealed class DbfTable : IDisposable
    {
        private const byte FieldTerminator = 0x0D;
        private const byte DeletedRecordMarker = 0x2A;
        private const int HeaderLengthInBytes = 32;
        private const int FieldDescriptorLengthInBytes = 32;

        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly DbfField[] _fields;
        private readonly Dictionary<string, int> _fieldIndexes;
        private readonly Encoding _encoding;
        private readonly int _headerLength;
        private readonly int _recordLength;

        private DbfTable(
            Stream stream,
            bool ownsStream,
            DbfField[] fields,
            Encoding encoding,
            int recordCount,
            int headerLength,
            int recordLength)
        {
            _stream = stream;
            _ownsStream = ownsStream;
            _fields = fields;
            _encoding = encoding;
            _headerLength = headerLength;
            _recordLength = recordLength;
            RecordCount = recordCount;

            _fieldIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < fields.Length; i++)
                _fieldIndexes[fields[i].Name] = i;
        }

        public IReadOnlyList<DbfField> Fields => _fields;

        /// <summary>The record count from the header. Deleted records are part of this count.</summary>
        public int RecordCount { get; }

        public Encoding Encoding => _encoding;

        public static DbfTable Open(string path, Encoding? encoding = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("The table path is empty.", nameof(path));

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                return Open(stream, ownsStream: true, encoding);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public static DbfTable Open(Stream stream, bool ownsStream, Encoding? encoding = null)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            var header = new byte[HeaderLengthInBytes];
            if (!TryReadExactly(stream, header, HeaderLengthInBytes))
                throw new ShapefileFormatException("The attribute table is shorter than its 32 byte header.");

            var recordCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
            var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
            var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2));
            var languageDriverId = header[29];

            if (recordCount < 0)
                throw new ShapefileFormatException("The attribute table declares a negative record count.");
            if (headerLength < HeaderLengthInBytes + 1 || recordLength < 1)
                throw new ShapefileFormatException("The attribute table header lengths are not valid.");

            var fields = ReadFields(stream, headerLength);
            var tableEncoding = encoding ?? EncodingFromLanguageDriverId(languageDriverId);

            return new DbfTable(stream, ownsStream, fields, tableEncoding, recordCount, headerLength, recordLength);
        }

        public int IndexOfField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return -1;
            return _fieldIndexes.TryGetValue(fieldName, out var index) ? index : -1;
        }

        /// <summary>
        /// Reads every record in file order, deleted records included. The order matches the shape record order,
        /// so the caller must keep the position and use <see cref="DbfRecord.IsDeleted"/> to filter.
        /// </summary>
        public IEnumerable<DbfRecord> ReadRecords()
        {
            _stream.Seek(_headerLength, SeekOrigin.Begin);
            var buffer = new byte[_recordLength];

            for (var i = 0; i < RecordCount; i++)
            {
                if (!TryReadExactly(_stream, buffer, _recordLength))
                    yield break;

                yield return ParseRecord(buffer);
            }
        }

        public void Dispose()
        {
            if (_ownsStream)
                _stream.Dispose();
        }

        private DbfRecord ParseRecord(byte[] buffer)
        {
            var values = new string[_fields.Length];
            var offset = 1;

            for (var i = 0; i < _fields.Length; i++)
            {
                var field = _fields[i];
                var length = Math.Min(field.Length, Math.Max(0, buffer.Length - offset));
                var text = _encoding.GetString(buffer, offset, length).Trim();
                values[i] = FormatValue(field, text);
                offset += field.Length;
            }

            return new DbfRecord(this, values, buffer[0] == DeletedRecordMarker);
        }

        private static string FormatValue(DbfField field, string text)
        {
            switch (field.TypeCode)
            {
                case 'D':
                    return FormatDate(text);
                case 'L':
                    return FormatLogical(text);
                default:
                    return text;
            }
        }

        private static string FormatDate(string text)
        {
            if (text.Length != 8)
                return text;

            return DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : text;
        }

        private static string FormatLogical(string text)
        {
            if (text.Length != 1)
                return string.Empty;

            switch (char.ToUpperInvariant(text[0]))
            {
                case 'T':
                case 'Y':
                    return "true";
                case 'F':
                case 'N':
                    return "false";
                default:
                    return string.Empty;
            }
        }

        private static DbfField[] ReadFields(Stream stream, int headerLength)
        {
            var fields = new List<DbfField>();
            var descriptor = new byte[FieldDescriptorLengthInBytes];
            var position = HeaderLengthInBytes;

            while (position + FieldDescriptorLengthInBytes <= headerLength)
            {
                if (!TryReadExactly(stream, descriptor, FieldDescriptorLengthInBytes))
                    break;
                position += FieldDescriptorLengthInBytes;

                if (descriptor[0] == FieldTerminator || descriptor[0] == 0x00)
                    break;

                var name = ReadFieldName(descriptor);
                var typeCode = (char)descriptor[11];
                var length = descriptor[16];
                var decimalCount = descriptor[17];

                if (name.Length == 0)
                    throw new ShapefileFormatException("The attribute table has a field with an empty name.");

                fields.Add(new DbfField(name, typeCode, length, decimalCount));
            }

            if (fields.Count == 0)
                throw new ShapefileFormatException("The attribute table has no fields.");

            return fields.ToArray();
        }

        private static string ReadFieldName(byte[] descriptor)
        {
            var length = 0;
            while (length < 11 && descriptor[length] != 0x00)
                length++;

            return Encoding.ASCII.GetString(descriptor, 0, length).Trim();
        }

        /// <summary>Maps the dBase language driver byte to an encoding. Falls back to Latin-1.</summary>
        private static Encoding EncodingFromLanguageDriverId(byte languageDriverId)
        {
            var codePage = languageDriverId switch
            {
                0x01 => 437,
                0x02 => 850,
                0x03 => 1252,
                0x08 => 865,
                0x09 => 437,
                0x0A => 850,
                0x4D => 936,
                0x4E => 949,
                0x4F => 950,
                0x50 => 874,
                0x57 => 1252,
                0x58 => 1252,
                0x59 => 1252,
                0x64 => 852,
                0x65 => 866,
                0x66 => 865,
                0x87 => 852,
                0xC8 => 1250,
                0xC9 => 1251,
                0xCA => 1254,
                0xCB => 1253,
                _ => 0,
            };

            return TextEncodings.FromCodePage(codePage);
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
