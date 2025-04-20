using RawDiskManager;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace RawDiskManagerDemoA;

internal class Program
{
    private static string _indentation = "";

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
        Console.WriteLine("RawDiskManager Demo A: restore a physical disk completely / from zip file created by Demo9");



        var deviceID = 3;  // <---------------- CHANGE THIS TO YOUR DRIVE !



        string destinationDirectory = GetTempDirectory();
        var filename = @"C:\Temp\ImageBackup_Disc1_DriveI_Full_2025-04-19_16-34-28.zip";







        try
        {
            Log($"------------------ looking up disc ------------------");
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();
            var destinationDisc = manager.GetDiscByDeviceID(physicalDiscs, deviceID);
            if (destinationDisc is null)
                throw new Exception($"WARNING: Device with ID {deviceID} not found");
            var destinationPath = @$"\\.\PHYSICALDRIVE{deviceID}";


            using (FileStream zipFileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (ZipArchive archive = new ZipArchive(zipFileStream, ZipArchiveMode.Read))
            {
                Log($"------------------ checking backup data integrity------------------");

                var metadataJson = GetTextChunkFromArchive(archive, "metadata");
                if (string.IsNullOrEmpty(metadataJson))
                    throw new Exception("Metadata not present in zip archive.");
                var metadata = JsonSerializer.Deserialize<Metadata>(metadataJson);
                if (metadata == null)
                    throw new Exception("Metadata cannot be deserialized.");
                Log($"Metadata is OK");


                var mbr = GetBinaryChunkFromArchive(archive, "mbr");
                if (mbr.GetLength(0) != 512)
                    throw new Exception($"MBR file is not 512 bytes long");
                Log($"MBR is OK");


                var gpt1 = GetBinaryChunkFromArchive(archive, "gpt1");
                Log($"Read GPT1 ({gpt1.GetLength(0)} Bytes)");
                if (gpt1.GetLength(0) < 512)
                    throw new Exception($"GPT file is not 512 bytes long");
                var gptParser = new GptParser(destinationDisc.LogicalSectorSize);
                var gpt1Decoded = gptParser.Parse(gpt1);
                Log($"GPT1 is OK");


                var gpt2 = GetBinaryChunkFromArchive(archive, "gpt2");
                Log($"Read GPT2 ({gpt2.GetLength(0)} Bytes)");
                if (gpt2.GetLength(0) < 512)
                    throw new Exception($"GPT file is not 512 bytes long");
                var gpt2Decoded = gptParser.Parse(gpt2);
                Log($"GPT2 is OK");


                //var fileInfo = new FileInfo(filename4);
                //if ((ulong)fileInfo.Length > disc.Size)
                //    throw new Exception($"Destination disk is too small for restore. " +
                //        $"Backup data is {SizeFormatter.Format((ulong)fileInfo.Length)}, " + 
                //        $"but the target disc only has a capacity of {SizeFormatter.Format(disc.Size)}");
                //else if ((ulong)fileInfo.Length < disc.Size)
                //    Log($"Destination disk is larger than necessary. " + 
                //        $"Backup data is {SizeFormatter.Format((ulong)fileInfo.Length)}, " + 
                //        $"but the target disc has a capacity of {SizeFormatter.Format(disc.Size)}");
                //
                //destinationPath = disc.Path;
                //if (destinationPath is null)
                //    throw new Exception($"sourcePath is invalid for disc {disc.DeviceId}");
                //Log($"Partition image is OK"); 




                Log($"ATTENTION!!!!! YOU ARE ABOUT TO OVERWRITE DISC {destinationPath} COMPLETELY !!!!  press y to continue");
                if (Console.ReadKey().KeyChar != 'y') return;
                Log($"ATTENTION!!!!! ALL DATA ON THE DISC WITH ID {deviceID} SIZE {SizeFormatter.Format(destinationDisc.Size)} WILL BE LOST  press y to continue\");");
                if (Console.ReadKey().KeyChar != 'y') return;
                Log($"ATTENTION!!!!! ALL DATA ON THE DISC WITH ID {deviceID} SIZE {SizeFormatter.Format(destinationDisc.Size)} WILL BE LOST  press y to continue\");");
                if (Console.ReadKey().KeyChar != 'y') return;
                Log($"");




                ZipArchiveEntry? entry = archive.GetEntry("wholedisk1");
                if (entry != null)
                {
                    var zipStream = GetStreamFromArchive(archive, "wholedisk1");
                    ulong sourceOffset = 0;
                    ulong destinationOffset = 0;
                    manager.WriteFromStreamToDisk(zipStream, (ulong)entry.Length, destinationPath, sourceOffset, destinationOffset, MyProgressHandler);
                }
                else
                {
                    Log($"------------------ restoring MBR ------------------");
                    manager.WritePhysicalSectors(destinationPath, mbr, 0, 512);
                    Log($"Wrote MBR to disc");



                    Log($"------------------ restoring GPTs ------------------");
                    var totalLBACount      = destinationDisc.Size / destinationDisc.LogicalSectorSize;
                    ulong currentLBA       = 1;
                    ulong startingLBA      = 2;
                    ulong firstUsableLBA   = 2 + 32;
                    ulong backupLBA        = totalLBACount - 1;
                    ulong gpt2ArrayLBA     = totalLBACount - 33;
                    ulong lastUsableLBA    = totalLBACount - 34;
                    ulong gpt1HeaderOffset = currentLBA   * destinationDisc.LogicalSectorSize;
                    ulong gpt1ArrayOffset  = startingLBA  * destinationDisc.LogicalSectorSize;
                    ulong gpt2HeaderOffset = backupLBA    * destinationDisc.LogicalSectorSize;
                    ulong gpt2ArrayOffset  = gpt2ArrayLBA * destinationDisc.LogicalSectorSize;

                    // we need to patch the GPTs before writing
                    gpt1Decoded.CurrentLBA     = currentLBA;
                    gpt1Decoded.StartingLBA    = startingLBA;
                    gpt1Decoded.FirstUsableLBA = firstUsableLBA;
                    gpt1Decoded.BackupLBA      = backupLBA;
                    gpt1Decoded.LastUsableLBA  = lastUsableLBA;
                    gptParser.Reconstruct(gpt1, gpt1Decoded);

                    gpt2Decoded.CurrentLBA     = currentLBA;
                    gpt2Decoded.StartingLBA    = startingLBA;
                    gpt2Decoded.FirstUsableLBA = firstUsableLBA;
                    gpt2Decoded.BackupLBA      = backupLBA;
                    gpt2Decoded.LastUsableLBA  = lastUsableLBA;
                    gptParser.Reconstruct(gpt2, gpt2Decoded);

                    // writing GPT1
                    (int header1Length, int array1Length, byte[] gpt1Header, byte[] gpt1Array) = SplitGPT(gpt1);
                    manager.WritePhysicalSectors(destinationPath, gpt1Header, gpt1HeaderOffset, (ulong)header1Length);
                    manager.WritePhysicalSectors(destinationPath, gpt1Array , gpt1ArrayOffset , (ulong)array1Length);
                    Log($"Wrote {gpt1.GetLength(0)} Bytes to disc");

                    // ATTENTION NEED TO CHECK OF SPACE ON DISK IS SUFFICIENT FOR THE GPT2 !!

                    // writing GPT2
                    (int header2Length, int array2Length, byte[] gpt2Header, byte[] gpt2Array) = SplitGPT(gpt2);
                    manager.WritePhysicalSectors(destinationPath, gpt2Header, gpt2HeaderOffset, (ulong)header2Length);
                    manager.WritePhysicalSectors(destinationPath, gpt2Array , gpt2ArrayOffset , (ulong)array2Length);
                    Log($"Wrote GPTs to disc");


                    Log($"------------------ restoring partitions ------------------");
                    //foreach (var p in metadata.PhysicalDisks[0].Partitions)
                    //{
                    //    Log($"Partition {p.PartitionNumber}");
                    //    _indentation = "    ";
                    //    AddSomeDebugInfo(destinationDisc, p, gpt1Decoded, raw: false);
                    //
                    //    var zipStream = GetStreamFromArchive(archive, p.BackupFilename!);
                    //
                    //    ulong sourceOffset = 0;
                    //    ulong destinationOffset = p.Offset;
                    //    manager.WriteFromStreamToDisk(zipStream, p.Size, destinationPath, sourceOffset, destinationOffset, MyProgressHandler);
                    //
                    //    Log($"Finished 100 %                                       ");
                    //    Log($"");
                    //}

                }


            }
            Log($"Restore successful.");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            Log("If you get an access denied exception, you need to run the program as administrator.");
        }   
    }

    private static (int, int, byte[], byte[]) SplitGPT(byte[] gpt2)
    {
        int headerLength;
        int arrayLength;
        byte[] gpt2Header;
        byte[] gpt2Array;

        // Split gpt2 into header and array
        headerLength = 512;
        arrayLength  = gpt2.GetLength(0) - headerLength;
        gpt2Header   = new byte[headerLength];
        gpt2Array   = new byte[gpt2.GetLength(0) - gpt2Header.GetLength(0)];

        Array.Copy(gpt2, 0, gpt2Header, 0, gpt2Header.GetLength(0));
        Array.Copy(gpt2, gpt2Header.GetLength(0), gpt2Array, 0, gpt2.GetLength(0) - headerLength);

        return (headerLength, arrayLength, gpt2Header, gpt2Array);
    }

    private static string GetTextChunkFromArchive(ZipArchive archive, string fileToExtract)
    {
        var data = GetBinaryChunkFromArchive(archive, fileToExtract);
        return Encoding.UTF8.GetString(data);
    }

    private static byte[] GetBinaryChunkFromArchive(ZipArchive archive, string fileToExtract)
    {
        ZipArchiveEntry? entry = archive.GetEntry(fileToExtract);
        if (entry == null)
            throw new FileNotFoundException($"Die Datei '{fileToExtract}' wurde im ZIP-Archiv nicht gefunden.");

        using (Stream entryStream = entry.Open())
        using (MemoryStream outputStream = new MemoryStream())
        {
            entryStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }

    private static bool StreamExists(ZipArchive archive, string fileToExtract)
    {
        return archive.GetEntry(fileToExtract) != null;
    }

    private static Stream GetStreamFromArchive(ZipArchive archive, string fileToExtract)
    {
        ZipArchiveEntry? entry = archive.GetEntry(fileToExtract);
        if (entry == null)
            throw new FileNotFoundException($"Die Datei '{fileToExtract}' wurde im ZIP-Archiv nicht gefunden.");

        Stream entryStream = entry.Open();
        return entryStream;
    }

    private static void MyProgressHandler(PhysicalDiskManager.ProgressData data)
    {
        Console.Write($"Progress: {data.Percentage:N1} %       \r");
    }

    private static string GetTempDirectory()
    {
        var destinationDirectory = @$"C:\Temp";
        if (!Directory.Exists(destinationDirectory))
            destinationDirectory = Path.GetTempPath();
        return destinationDirectory;
    }

    private static void AddSomeDebugInfo(PhysicalDisk disc, Partition p, GptContents gptDecoded, bool raw)
    {
        int index = (int)p.PartitionNumber - 1;

        Log($"Size {SizeFormatter.Format(p.Size)} " +
            $"DriveLetter {(p.Volume.HasDriveLetter ? p.Volume.DriveLetter : "-")}  " +
            $"FileSystem {p?.Volume?.FileSystem}  " +
            $"Type {p.GptTypeDesc}");

        var firstLba        = gptDecoded.PartitionTable[index].FirstLBA;
        var lastLba         = gptDecoded.PartitionTable[index].LastLBA;
        var firstLbaOffset  = firstLba * disc.LogicalSectorSize;
        var lastLbaOffset   = lastLba  * disc.LogicalSectorSize;
        Log($"GPT says from LBA {firstLba,14} to LBA {lastLba,14} (from {firstLbaOffset,14} to {lastLbaOffset,14})");
        Log($"Partition says                                  from {p.Offset,14} to {p.Offset + p.Size,14})");
        Log($"Differences:                                         {firstLbaOffset-p.Offset,14}    {(p.Offset + p.Size)-lastLbaOffset,14})");

        if (raw)
        {
            var partStart       = p.Offset;
            var partEnd         = p.Offset + p.Size;
            Log($"Saving raw sectors for this partition,          from {partStart,14} to {partEnd,14}...");
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine($"{_indentation}{message}");
    }
}
