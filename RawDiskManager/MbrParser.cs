using System;

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
    /// Parses the Master Boot Record (MBR) of a disk.
    /// On a classical generic (non UEFI) system, the MBR contains up to 4 partitions.
    /// On a GPT partioned (UEFI) system, the MBR contains 1 partition covering the complete disk,
    /// just to ensure non-UEFI bioses will get plausible information.
    /// Please refer to https://en.wikipedia.org/wiki/Master_boot_record for more information.
    /// </summary>
    public class MbrParser
    {
        public MbrContents Parse(byte[] mbrData)
        {
            if (mbrData == null || mbrData.Length < 512)
                throw new ArgumentException("Invalid MBR data. Length must be at least 512 bytes (the smallest logical sector size)");

            var mbr = new MbrContents();
            mbr.BootstrapCodeArea1    = ByteLib.ExtractBytes  (mbrData, 0x0000, 0x00DA);

            mbr.DiscTimestamp         = ByteLib.ExtractWord   (mbrData, 0x00DA);
            mbr.OriginalPhysicalDrive = ByteLib.ExtractByte   (mbrData, 0x00DC);
            mbr.Seconds               = ByteLib.ExtractByte   (mbrData, 0x00DD);
            mbr.Minutes               = ByteLib.ExtractByte   (mbrData, 0x00DE);
            mbr.Hours                 = ByteLib.ExtractByte   (mbrData, 0x00DF);
            mbr.DiscTimestampLong     = ByteLib.ExtractBytes  (mbrData, 0x00DA, 6);

            mbr.BootstrapCodeArea2    = ByteLib.ExtractBytes  (mbrData, 0x00E0, 216);

            mbr.DiscSignature32       = ByteLib.ExtractDword  (mbrData, 0x01B8);
            mbr.CopyProtectFlag       = ByteLib.ExtractWord   (mbrData, 0x01BC);
            mbr.DiscSignature48       = ByteLib.ExtractBytes  (mbrData, 0x01B8, 6);

            mbr.PartitionEntries[0]   = ExtractEntryAt        (mbrData, 0x01BE);
            mbr.PartitionEntries[1]   = ExtractEntryAt        (mbrData, 0x01CE);
            mbr.PartitionEntries[2]   = ExtractEntryAt        (mbrData, 0x01DE);
            mbr.PartitionEntries[3]   = ExtractEntryAt        (mbrData, 0x01EE);

            mbr.BootSignature         = ByteLib.ExtractWord   (mbrData, 0x01FE);
            return mbr;
        }

        public void Validate(MbrContents mbr)
        {
            if (mbr.BootSignature != 0xAA55)
                throw new ArgumentException("Invalid MBR signature. Expected 0xAA55 at the end of the MBR");
        }

        public void CreateNewSignature(byte[] mbrData)
        {
            var buffer = new byte[6];
            new Random((int)DateTime.Now.Ticks).NextBytes(buffer);
            ByteLib.WriteBytes(mbrData, 0x01B8, 6, buffer);
        }

        private MbrPartitionEntry ExtractEntryAt(byte[] data, int offset)
        {
            var entry = new MbrPartitionEntry();
            entry.Status                = ByteLib.ExtractByte (data, offset + 0x00);
            entry.FirstAbsoluteSector.H = ByteLib.ExtractByte (data, offset + 0x01);
            entry.FirstAbsoluteSector.S = ByteLib.ExtractByte (data, offset + 0x02);
            entry.FirstAbsoluteSector.C = ByteLib.ExtractByte (data, offset + 0x03);
            entry.PartitionType         = ByteLib.ExtractByte (data, offset + 0x04);
            entry.LastAbsoluteSector.H  = ByteLib.ExtractByte (data, offset + 0x05);
            entry.LastAbsoluteSector.S  = ByteLib.ExtractByte (data, offset + 0x06);
            entry.LastAbsoluteSector.C  = ByteLib.ExtractByte (data, offset + 0x07);
            entry.LBA                   = ByteLib.ExtractDword(data, offset + 0x08);
            entry.NumberOfSectors       = ByteLib.ExtractDword(data, offset + 0x0C);
            return entry;
        }
    }

    public class MbrContents
    {
        public byte[]               BootstrapCodeArea1       { get; internal set; } = new byte[218];
        public ushort               DiscTimestamp            { get; internal set; }
        public MbrPartitionEntry[]  PartitionEntries         { get; internal set; } = new MbrPartitionEntry[4];
        public ushort               OriginalPhysicalDrive    { get; internal set; }
        public byte                 Seconds                  { get; internal set; }
        public byte                 Minutes                  { get; internal set; }
        public byte                 Hours                    { get; internal set; }
        public byte[]               DiscTimestampLong        { get; internal set; }
        public byte[]               BootstrapCodeArea2       { get; internal set; }
        public ulong                DiscSignature32          { get; internal set; }
        public ushort               CopyProtectFlag          { get; internal set; }
        public byte[]               DiscSignature48          { get; internal set; }
        public ushort               BootSignature            { get; internal set; }
    }

    public class MbrPartitionEntry
    {
        public byte  Status               { get; internal set; }
        public CHS   FirstAbsoluteSector  { get; internal set; } = new CHS();
        public CHS   LastAbsoluteSector   { get; internal set; } = new CHS();
        public byte  PartitionType        { get; internal set; }
        public ulong LBA                  { get; internal set; }
        public ulong NumberOfSectors      { get; internal set; }

        public override string ToString()
        {
            var newLine = "\n                        ";
            return 
                $"Status             : {Status.ToString("X2")} "        + newLine + 
                $"FirstAbsoluteSector: {FirstAbsoluteSector} "          + newLine + 
                $"PartitionType      : {PartitionType.ToString("X2")} " + newLine + 
                $"LastAbsoluteSector : {LastAbsoluteSector} "           + newLine + 
                $"LBA                : {LBA.ToString("X8")} "           + newLine + 
                $"NumberOfSectors    : {NumberOfSectors.ToString("X8").Substring(0,8)}";
        }
    }
}
