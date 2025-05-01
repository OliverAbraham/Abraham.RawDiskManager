using RawDiskManager;
using System.Text;

namespace RawDiskManagerDemo2;

internal class Program
{
    /// <summary>
    /// Demo for RawDiskManager nuget package.
    /// 
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
    static void Main(string[] args)
    {
        Console.WriteLine("RawDiskManager Demo 2: read and analyze MBR and both GPTs");


        
        var deviceID = 3;  // <---------------- CHANGE THIS TO YOUR DRIVE ! // choose a small disk, i.e. a USB drive!
                           // <---------------- look up this number in disc management



        try
        {
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();
            var disc = manager.GetDiscByDeviceID(physicalDiscs, deviceID);
            if (disc is null)
                throw new Exception($"Device with ID {deviceID} not found");


            Console.WriteLine($"------------------ MBR contents ---------------");
            (var rawMbr, var decodedMbr) = manager.ReadMBR(deviceID);
            Display(decodedMbr);


            Console.WriteLine();
            Console.WriteLine($"------------------ first GUID Partition Table (GPT) ---------------");
            (var gpt1, var gpt2, var gpt1Decoded, var gpt2Decoded) = manager.ReadGPTs(deviceID, disc.LogicalSectorSize, disc.Size);
            var gptParser = new GptParser(disc.LogicalSectorSize);
            Display(gpt1Decoded);


            Console.WriteLine();
            Console.WriteLine($"------------------ second GUID Partition Table ---------------");
            Display(gpt2Decoded);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   
    }

        private static void Display(MbrContents mbr)
        {
            Console.WriteLine($"BootstrapCodeArea1    : {Hex(mbr.BootstrapCodeArea1)}");
            Console.WriteLine($"DiscTimestamp         : {Hex(mbr.DiscTimestamp)}");
            Console.WriteLine($"OriginalPhysicalDrive : {Hex(mbr.OriginalPhysicalDrive)}");
            Console.WriteLine($"Seconds               : {Hex(mbr.Seconds)}");
            Console.WriteLine($"Minutes               : {Hex(mbr.Minutes)}");
            Console.WriteLine($"Hours                 : {Hex(mbr.Hours)}");
            Console.WriteLine($"DiscTimestampLong     : {Hex(mbr.DiscTimestampLong)}");
            Console.WriteLine($"BootstrapCodeArea2    : {Hex(mbr.BootstrapCodeArea2)}");
            Console.WriteLine($"DiscSignature32       : {Hex(mbr.DiscSignature32)}");
            Console.WriteLine($"CopyProtectFlag       : {Hex(mbr.CopyProtectFlag)}");
            Console.WriteLine($"DiscSignature48       : {Hex(mbr.DiscSignature48)}");
            Console.WriteLine($"Partition entry 1     : {mbr.PartitionEntries[0]}");
            Console.WriteLine($"Partition entry 2     : {mbr.PartitionEntries[1]}");
            Console.WriteLine($"Partition entry 3     : {mbr.PartitionEntries[2]}");
            Console.WriteLine($"Partition entry 4     : {mbr.PartitionEntries[3]}");
            Console.WriteLine($"BootSignature         : {Hex(mbr.BootSignature)}");
        }

        private static void Display(GptContents gpt)
        {
            Console.WriteLine($"Signature                : {Hex(gpt.Signature)}");
            Console.WriteLine($"Revision                 : {Hex(gpt.Revision)}");
            Console.WriteLine($"HeaderSize               : {gpt.HeaderSize}");
            Console.WriteLine($"CRC32                    : {Hex(gpt.CRC32)}");
            Console.WriteLine($"CurrentLBA               : {gpt.CurrentLBA}");
            Console.WriteLine($"BackupLBA                : {gpt.BackupLBA}");
            Console.WriteLine($"FirstUsableLBA           : {gpt.FirstUsableLBA}");
            Console.WriteLine($"LastUsableLBA            : {gpt.LastUsableLBA}");
            Console.WriteLine($"DiskGUID                 : {gpt.DiskGUID}");
            Console.WriteLine($"StartingLBA              : {gpt.StartingLBA}");
            Console.WriteLine($"NumberOfPartitionEntries : {Hex(gpt.NumberOfPartitionEntries)}");
            Console.WriteLine($"SizeOfPartitionEntry     : {Hex(gpt.SizeOfPartitionEntry)}");
            Console.WriteLine($"CRC32PartitionEntries    : {Hex(gpt.CRC32PartitionEntries)}");
            Console.WriteLine($"TotalLengthOfGptInBytes  : {Hex(gpt.GptTotalLength)}");
            Console.WriteLine($"PartitionEntries         : {gpt.PartitionTable.Count} ({gpt.PartitionTable.Where(e => e.HasData).Count()} filled)");

            foreach (var entry in gpt.PartitionTable.Where(e => e.HasData))
            {
                Console.WriteLine();
                Console.WriteLine($"    PartitionTypeGUID    : {entry.PartitionTypeGUID} ({PartitionTypeGuid.GetTypeByGuid(entry.PartitionTypeGUID.ToString())})");
                Console.WriteLine($"    UniquePartitionGUID  : {entry.UniquePartitionGUID}");
                Console.WriteLine($"    FirstLBA             : {entry.FirstLBA}");
                Console.WriteLine($"    LastLBA              : {entry.LastLBA}");
                Console.WriteLine($"    AttributeFlags       : {entry.AttributeFlags:X}");
                Console.WriteLine($"    PartitionName        : {entry.PartitionName}");
                Console.WriteLine($"    TotalSectors         : {entry.TotalSectors}");
                Console.WriteLine($"    TotalBytes           : {entry.TotalBytes} ({SizeFormatter.Format(entry.TotalBytes)})");
            }
        }

        private static string Hex(ushort value)
        {
            return value.ToString("X2");
        }

        private static string Hex(ulong value)
        {
            return value.ToString("X4");
        }

        private static string Hex(byte[] values)
        {
            var hexValues = values.Select(v => v.ToString("X2"));

            // insert a newline every 32 bytes
            var sb = new StringBuilder();
            for (int i = 0; i < hexValues.Count(); i++)
            {
                sb.Append(hexValues.ElementAt(i));
                if ((i + 1) % 32 == 0)
                    sb.Append("\n                        ");
                else
                    sb.Append(" ");
            }

            return sb.ToString();
        }
}
