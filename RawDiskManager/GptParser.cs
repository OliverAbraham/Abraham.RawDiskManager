using System;
using System.Collections.Generic;

/// <summary>
/// Read,write data from/to physical disks, partitions, volumes. 
/// Read/write/analyze MBR and GPT, disc structures.
/// Create discs, partitions, volumes.
/// 
/// Author:
/// Oliver Abraham, mail@oliver-abraham.de, https://www.oliver-abraham.de
/// 
/// Source code hosted at: 
/// https://github.com/OliverAbraham/Abraham.RawDiscManager
/// 
/// Nuget Package hosted at: 
/// https://www.nuget.org/packages/Abraham.RawDiscManager
/// </summary>
namespace RawDiskManager
{
    /// <summary>
    /// Parses the GUID Partition Table of a disk.
    /// On a GPT partioned (UEFI) system, the disc contains two copies of the GPT.
    /// Please refer to https://en.wikipedia.org/wiki/GUID_Partition_Table for more information.
    /// 
    /// Like MBR, GPT uses logical block addressing (LBA) in place of the historical cylinder-head-sector (CHS) addressing. 
    /// The protective MBR is stored at LBA 0, and the GPT header is in LBA 1. 
    /// The GPT header has a pointer to the partition table (Partition Entry Array), which is typically at LBA 2. 
    /// Each entry in the partition table has the same size, which is 128 or 256 or 512, etc., bytes; typically this size is 128 bytes. 
    
    /// The UEFI specification stipulates that a minimum of 16,384 bytes, regardless of sector size, are allocated for the Partition Entry Array. 
    /// Thus, on a disk with 512-byte sectors, at least 32 sectors are used for the Partition Entry Array, 
    /// and the first usable block is at LBA 34 or higher, while on a 4,096-byte sector disk, 
    /// at least 4 sectors are used for the Partition Entry Array, and the first usable block is at LBA 6 or higher. 
    /// 
    /// In addition to the primary GPT header and Partition Entry Array, stored at the beginning of the disk, 
    /// there is a backup GPT header and Partition Entry Array, stored at the end of the disk. 
    /// The backup GPT header must be at the last block on the disk (LBA -1) and the backup Partition Entry Array is placed 
    /// between the end of the last partition and the last block.[2]: pp. 115-120, §5.3 
    /// </summary>
    public class GptParser
    {
        private ulong _logicalSectorSize;

        public GptParser(ulong logicalSectorSize)
        {
            _logicalSectorSize = logicalSectorSize;
        }

        /// <summary>
        /// Accepts the first Sector of a GPT disk and returns the start and length of the partition table array.
        /// Then the complete GPT must be read from disk, indicated by "TotalLengthOfGptInBytes".
        /// Then, the Parse method can be called, passing the complete GPT data.
        /// </summary>
        /// <returns>Starting LBA and length of the partition table array</returns>
        public (ulong,ulong) ParseArrayPositionAndSize(byte[] gptHeader)
        {
            if (gptHeader == null || gptHeader.Length < 512)
                throw new ArgumentException("Invalid GPT data. Length must be between 512 and 16384 bytes (32*512). 512 bytes is the smallest logical sector size.");

            var gpt = new GptContents();
            gpt.StartingLBA              = ByteLib.ExtractQword(gptHeader, 0x0048);               // 72 (0x48) 	8 bytes 	Starting LBA of array of partition entries (usually 2 for compatibility)
            gpt.NumberOfPartitionEntries = ByteLib.ExtractDword(gptHeader, 0x0050);     // 80 (0x50) 	4 bytes 	Number of partition entries in array
            gpt.SizeOfPartitionEntry     = ByteLib.ExtractDword(gptHeader, 0x0054);     // 84 (0x54) 	4 bytes 	Size of a single partition entry (usually 80h or 128)

            gpt.GptTotalLength = _logicalSectorSize + (gpt.NumberOfPartitionEntries * gpt.SizeOfPartitionEntry);
            
            var pos = gpt.StartingLBA * _logicalSectorSize;
            var length = gpt.NumberOfPartitionEntries * gpt.SizeOfPartitionEntry;
            return (pos,length);
        }

        public GptContents Parse(byte[] gpt)
        {
            if (gpt == null || gpt.Length < 512)
                throw new ArgumentException("Invalid GPT data. Length must be between 512 and 16384 bytes (32*512). 512 bytes is the smallest logical sector size.");

            var gptHeader = ByteLib.ExtractBytes(gpt, 0, _logicalSectorSize);
            var gptArray  = ByteLib.ExtractBytes(gpt, _logicalSectorSize, (ulong)(gpt.GetLength(0) - gptHeader.GetLength(0)));
            return Parse(gptHeader, gptArray);
        }

        public GptContents Parse(byte[] gptHeader, byte[] gptArray)
        {
            if (gptHeader == null || gptHeader.Length < 512)
                throw new ArgumentException("Invalid GPT data. Length must be between 512 and 16384 bytes (32*512). 512 bytes is the smallest logical sector size.");

            var gpt = new GptContents();
            gpt.Signature                = ByteLib.ExtractQword(gptHeader, 0x0000);               // 0 (0x00) 	8 bytes 	Signature ("EFI PART", 45h 46h 49h 20h 50h 41h 52h 54h or 0x5452415020494645ULL[a] on little-endian machines)
            gpt.Revision                 = ByteLib.ExtractDword(gptHeader, 0x0008);               // 8 (0x08) 	4 bytes 	Revision number of header - 1.0 (00h 00h 01h 00h) for UEFI 2.10
            gpt.HeaderSize               = ByteLib.ExtractDword(gptHeader, 0x000C);               // 12 (0x0C) 	4 bytes 	Header size in little endian (in bytes, usually 5Ch 00h 00h 00h or 92 bytes)
            gpt.CRC32                    = ByteLib.ExtractDword(gptHeader, 0x0010);               // 16 (0x10) 	4 bytes 	CRC32 of header (offset +0 to +0x5B) in little endian, with this field zeroed during calculation
                                                                                                // 20 (0x14) 	4 bytes 	Reserved; must be zero
            gpt.CurrentLBA               = ByteLib.ExtractQword(gptHeader, 0x0018);               // 24 (0x18) 	8 bytes 	Current LBA (location of this header copy)
            gpt.BackupLBA                = ByteLib.ExtractQword(gptHeader, 0x0020);               // 32 (0x20) 	8 bytes 	Backup LBA (location of the other header copy)
            gpt.FirstUsableLBA           = ByteLib.ExtractQword(gptHeader, 0x0028);               // 40 (0x28) 	8 bytes 	First usable LBA for partitions (primary partition table last LBA + 1)
            gpt.LastUsableLBA            = ByteLib.ExtractQword(gptHeader, 0x0030);               // 48 (0x30) 	8 bytes 	Last usable LBA (secondary partition table first LBA − 1)
            gpt.DiskGUID                 = new Guid(ByteLib.ExtractBytes(gptHeader, 0x0038, 16)); // 56 (0x38) 	16 bytes 	Disk GUID in little endian[b]
            gpt.StartingLBA              = ByteLib.ExtractQword(gptHeader, 0x0048);               // 72 (0x48) 	8 bytes 	Starting LBA of array of partition entries (usually 2 for compatibility)
            gpt.NumberOfPartitionEntries = ByteLib.ExtractDword(gptHeader, 0x0050);               // 80 (0x50) 	4 bytes 	Number of partition entries in array
            gpt.SizeOfPartitionEntry     = ByteLib.ExtractDword(gptHeader, 0x0054);               // 84 (0x54) 	4 bytes 	Size of a single partition entry (usually 80h or 128)
            gpt.CRC32PartitionEntries    = ByteLib.ExtractDword(gptHeader, 0x0058);               // 88 (0x58) 	4 bytes 	CRC32 of partition entries array in little endian

            gpt.GptHeaderLength          = _logicalSectorSize;
            gpt.GptArrayLength           = gpt.NumberOfPartitionEntries * gpt.SizeOfPartitionEntry;
            gpt.GptTotalLength           = gpt.GptHeaderLength + gpt.GptArrayLength;

            for (ulong i = 0; i < gpt.NumberOfPartitionEntries; i++)
            {
                var entry = ExtractEntryAt(gptArray, i * gpt.SizeOfPartitionEntry);
                gpt.PartitionTable.Add(entry);
            }
            return gpt;
        }

        public void Reconstruct(byte[] gpt, GptContents gptDecoded)
        {
            ByteLib.WriteQword(gpt, 0x0018,  gptDecoded.CurrentLBA    );               // 24 (0x18) 	8 bytes 	Current LBA (location of this header copy)
            ByteLib.WriteQword(gpt, 0x0020,  gptDecoded.BackupLBA     );               // 32 (0x20) 	8 bytes 	Backup LBA (location of the other header copy)
            ByteLib.WriteQword(gpt, 0x0028,  gptDecoded.FirstUsableLBA);               // 40 (0x28) 	8 bytes 	First usable LBA for partitions (primary partition table last LBA + 1)
            ByteLib.WriteQword(gpt, 0x0030,  gptDecoded.LastUsableLBA );               // 48 (0x30) 	8 bytes 	Last usable LBA (secondary partition table first LBA − 1)
            ByteLib.WriteQword(gpt, 0x0048,  gptDecoded.StartingLBA   );               // 72 (0x48) 	8 bytes 	Starting LBA of array of partition entries (usually 2 for compatibility)

            // re-calculate the checksum
            ByteLib.WriteQword(gpt, 0x0010, 0);
            var crc32 = CRC32.Calculate(gpt, 0, 0x5C);
            ByteLib.WriteQword(gpt, 0x0010, crc32);
        }


        public void Validate(GptContents gpt)
        {
            //if (gpt.BootSignature != 0xAA55)
            //    throw new ArgumentException("Invalid MBR signature. Expected 0xAA55 at the end of the MBR");
        }

        private GptPartitionEntry ExtractEntryAt(byte[] gptData, ulong offset)
        {
            var entry = new GptPartitionEntry();
            entry.LogicalSectorSize   = _logicalSectorSize;
            entry.PartitionTypeGUID   = new Guid(ByteLib.ExtractBytes(gptData, offset + 0x00, 16));             // 16 bytes
            entry.UniquePartitionGUID = new Guid(ByteLib.ExtractBytes(gptData, offset + 0x10, 16));             // 16 bytes
            entry.FirstLBA            = ByteLib.ExtractQword(gptData, offset + 0x20);                           // 8 bytes
            entry.LastLBA             = ByteLib.ExtractQword(gptData, offset + 0x28);                           // 8 bytes
            entry.AttributeFlags      = ByteLib.ExtractQword(gptData, offset + 0x30);                           // 8 bytes
            entry.PartitionName       = ByteLib.ExtractName (gptData, offset + 0x38, 72);                       // 72 bytes max
            entry.PartitionTypeDesc   = PartitionTypeGuid.GetTypeByGuid(entry.PartitionTypeGUID.ToString());
            return entry;
        }
    }

    public class GptContents
    {
        public ulong  Signature                { get; set; }
        public ulong  Revision                 { get; set; }
        public ulong  HeaderSize               { get; set; }
        public ulong  CRC32                    { get; set; }
        public ulong  CurrentLBA               { get; set; }
        public ulong  BackupLBA                { get; set; }
        public ulong  FirstUsableLBA           { get; set; }
        public ulong  LastUsableLBA            { get; set; }
        public Guid   DiskGUID                 { get; set; }
        public ulong  StartingLBA              { get; set; }
        public ulong  NumberOfPartitionEntries { get; set; }
        public ulong  SizeOfPartitionEntry     { get; set; }
        public ulong  CRC32PartitionEntries    { get; set; }
        public ulong  GptHeaderLength          { get; set; }
        public ulong  GptArrayLength           { get; set; }
        public ulong  GptTotalLength           { get; set; } // this is a calculated value, not in the GPT header

        public List<GptPartitionEntry> PartitionTable { get; internal set; } = new List<GptPartitionEntry>();
    }

    public class GptPartitionEntry
    {
        public Guid   PartitionTypeGUID   { get; internal set; } = new Guid();
        public Guid   UniquePartitionGUID { get; internal set; } = new Guid();
        public UInt64 FirstLBA            { get; internal set; }                 // 8 bytes
        public UInt64 LastLBA             { get; internal set; }                 // 8 bytes
        public UInt64 AttributeFlags      { get; internal set; }                 // 8 bytes
        public string PartitionName       { get; internal set; }                 // 72 bytes max
        public string PartitionTypeDesc   { get; internal set; }
        public ulong  LogicalSectorSize   { get; internal set; }
        public bool   HasData             => PartitionTypeGUID.Equals(Guid.Empty) == false; // not 0x00000000-0000-0000-0000-000000000000
        public ulong  TotalSectors        => HasData ? (LastLBA - FirstLBA + 1) : 0;
        public ulong  TotalBytes          => HasData ? (TotalSectors * LogicalSectorSize) : 0;
    }

    public class CHS
    {
        public byte C { get; set; }
        public byte H { get; set; }
        public byte S { get; set; }

        public override string ToString()
        {
            return $"C:{C} H:{H} S:{S}";
        }
    }
}
