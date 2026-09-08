using System;
using System.Buffers.Binary;

namespace Import_SHP.Formats
{
    /// <summary>Reads little endian values from a byte buffer and checks the buffer limits.</summary>
    internal struct ByteCursor
    {
        private readonly byte[] _buffer;
        private readonly int _start;
        private readonly int _length;
        private int _position;

        public ByteCursor(byte[] buffer, int start, int length)
        {
            _buffer = buffer;
            _start = start;
            _length = length;
            _position = 0;
        }

        public int Remaining => _length - _position;

        public bool CanRead(int byteCount) => Remaining >= byteCount;

        public int ReadInt32()
        {
            Require(sizeof(int));
            var value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_start + _position, sizeof(int)));
            _position += sizeof(int);
            return value;
        }

        public double ReadDouble()
        {
            Require(sizeof(double));
            var value = BinaryPrimitives.ReadDoubleLittleEndian(_buffer.AsSpan(_start + _position, sizeof(double)));
            _position += sizeof(double);
            return value;
        }

        public void Skip(int byteCount)
        {
            Require(byteCount);
            _position += byteCount;
        }

        private void Require(int byteCount)
        {
            if (byteCount < 0 || Remaining < byteCount)
                throw new ShapefileFormatException(
                    $"The shape record ends too early. The record needs {byteCount} more bytes but only {Remaining} bytes remain.");
        }
    }
}
