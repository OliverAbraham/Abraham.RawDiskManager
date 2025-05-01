using System;
using System.Text;

namespace RawDiskManager
{
    public class ByteLib
    {

        public static byte ExtractByte(byte[] buffer, int offset)
        {
            return buffer[offset];
        }

        public static ushort ExtractWord(byte[] buffer, ushort offset)
        {
            var lo = ((ushort)buffer[offset]);
            var hi = ((ushort)buffer[offset + 1] << 8);
            return (ushort)(lo | hi);
        }

        public static ulong ExtractDword(byte[] buffer, int offset)
        {
            var a = ((ulong)buffer[offset]);
            var b = ((ulong)buffer[offset + 1] << 8);
            var c = ((ulong)buffer[offset + 2] << 16);
            var d = ((ulong)buffer[offset + 3] << 24);
            return a | b | c | d;
        }

        public static UInt64 ExtractQword(byte[] buffer, ulong offset)
        {
            var a = ((UInt64)buffer[offset]);
            var b = ((UInt64)buffer[offset + 1] <<  8);
            var c = ((UInt64)buffer[offset + 2] << 16);
            var d = ((UInt64)buffer[offset + 3] << 24);
            var e = ((UInt64)buffer[offset + 4] << 32);
            var f = ((UInt64)buffer[offset + 5] << 40);
            var g = ((UInt64)buffer[offset + 6] << 48);
            var h = ((UInt64)buffer[offset + 7] << 56);
            return  a | b | c | d | e | f | g | h;
        }

        public static byte[] ExtractBytes(byte[] buffer, ulong offset, ulong count)
        {
            var result = new byte[count];
            Array.Copy(buffer, (long)offset, result, 0, (long)count);
            return result;
        }

        public static string ExtractNameAsUtf16LE(byte[] buffer, ulong offset, ulong count)
        {
            var bytes = ExtractBytes(buffer, offset, count);
            
            var result = "";
            for (ulong i = 0; i < count / 2; i++)
            {
                var block = new byte[4] { buffer[offset + i * 2], buffer[offset + i * 2 + 1], 0, 0 };
                var character = Encoding.UTF32.GetString(block);
                result += character;
            }
            return result.TrimEnd('\0');
        }

        public static void WriteBytes(byte[] destination, int offset, int count, byte[] source)
        {
            Array.Copy(source, 0, destination, offset, (long)count);
        }

        public static void WriteWord(byte[] destination, int offset, ulong value)
        {
            destination[offset + 0] = (byte)((value      ) & 0xFF);
            destination[offset + 1] = (byte)((value >>  8) & 0xFF);
        }

        public static void WriteDword(byte[] destination, int offset, ulong value)
        {
            destination[offset + 0] = (byte)((value      ) & 0xFF);
            destination[offset + 1] = (byte)((value >>  8) & 0xFF);
            destination[offset + 2] = (byte)((value >> 16) & 0xFF);
            destination[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static void WriteQword(byte[] destination, int offset, ulong value)
        {
            destination[offset + 0] = (byte)((value      ) & 0xFF);
            destination[offset + 1] = (byte)((value >>  8) & 0xFF);
            destination[offset + 2] = (byte)((value >> 16) & 0xFF);
            destination[offset + 3] = (byte)((value >> 24) & 0xFF);
            destination[offset + 4] = (byte)((value >> 32) & 0xFF);
            destination[offset + 5] = (byte)((value >> 40) & 0xFF);
            destination[offset + 6] = (byte)((value >> 48) & 0xFF);
            destination[offset + 7] = (byte)((value >> 56) & 0xFF);
        }

        public static void WriteNameAsUtf16LE(byte[] destination, int offset, int maxLength, string name)
        {
            var nameAsBytes = Encoding.UTF32.GetBytes(name);
            var length = Math.Min(maxLength-1, name.Length);
            Array.Clear(destination, offset, maxLength);

            for (int i = 0; i < name.Length; i++)
            {
                destination[offset + i*2    ] = nameAsBytes[i*4    ];
                destination[offset + i*2 + 1] = nameAsBytes[i*4 + 1];
            }
        }
    }
}
