using System;

namespace RawDiskManager
{
    public static class CRC32
    {
        private static readonly uint[] Crc32Table;

        static CRC32()
        {
            // Init the CRC32 table
            Crc32Table = new uint[256];
            const uint polynomial = 0xEDB88320; // Standard CRC32 polynome
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((crc & 1) == 1)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
                }
                Crc32Table[i] = crc;
            }
        }

        /// <summary>
        /// Calculates the CRC32 checksum of a buffer with a specified length.
        /// </summary>
        public static uint Calculate(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset and length are outside the buffer size");

            uint crc = 0xFFFFFFFF; // Initialwert
            for (int i = offset; i < offset + length; i++)
            {
                byte index = (byte)((crc ^ buffer[i]) & 0xFF);
                crc = (crc >> 8) ^ Crc32Table[index];
            }
            return ~crc; // invert
        }
    }
}
