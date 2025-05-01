using RawDiskManager;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VssSample;

namespace RawDiskManagerDemo9;

internal class Program
{
    private static string _indentation = "";
    private static string _completeLog = "";

    private class Snapshot
    {
        public string Path { get; internal set; }
        public VssBackup Vss { get; set; }
        public string SnapshotPath { get; set; }
    }

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
        var vssSnapshots = new Snapshot[26];
        try
        {
            Console.WriteLine("RawDiskManager Demo 9: backup a physical disk completely (image backup using VSS and on-the-fly zip compression)");

            //
            // I'm combining the VSS subsystem (through AlphaVSS, Demo8) with my Demo6.
            //
            // Steps:
            // 1. Analyze the disc.
            // 2. Read the metadata of the physical disk (MBR, GPT1, GPT2)
            // 3. Find all volumes with a drive letter
            // 4. create a snapshot for volume
            // 5. read all volumes, read from the snapshot where one exists, the remaining volumes are read directly
            // 6. on-the-fly zip compression 
            // 7. close the snapshots
            //



            var driveLetter = "H";  // <---------------- CHANGE THIS TO YOUR DRIVE !



            Log($"\n------------------ Inventory ------------------");
            var elapsedTime = Stopwatch.StartNew();
            string destinationDirectory = GetTempDirectory();
            var manager = new PhysicalDiskManager();
            manager.BufferSize = 100 * 1024 * 1024; // 100 MB buffer size
            var physicalDiscs = manager.GetPhysicalDiscs();

            var disc = manager.GetDiscByDriveLetter(physicalDiscs, driveLetter);
            if (disc is null)
                throw new Exception($"Device with drive Letter {driveLetter} not found");


            Log($"Partitions:");
            foreach (var partition in disc.Partitions)
                Log($"{partition.PartitionNumber,2} size {SizeFormatter.Format(partition.Size),10}   DriveLetter {partition.Volume.DriveLetter,2}   type {partition.GptTypeDesc}");


            Log($"\n------------------ Creating an empty image backup file ------------------");
            var destinationFilename = Path.Combine(destinationDirectory, $"ImageBackup_Disc{disc.DeviceId}_Drive{driveLetter}_Full_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.zip");
            Log($"Saving data to {destinationFilename}");
            if (File.Exists(destinationFilename))
                File.Delete(destinationFilename);


            
            Log($"\n------------------ looping over all volumes ------------------");
            int i = 0;
            foreach (var partition in disc.Partitions)
            {
                Log($"Partition {partition.PartitionNumber,2} size {SizeFormatter.Format(partition.Size),10} DriveLetter {partition.Volume.DriveLetter,2} type {partition.GptTypeDesc}");

                if (partition.Volume.HasDriveLetter)
                {
                    Console.Write($"    Creating a shadow copy for volume {driveLetter}...");
                    var ss = new Snapshot();
                    ss.Vss = new VssBackup();
                    ss.Path = @$"{driveLetter}:\";
                    ss.Vss.Setup(Alphaleonis.Win32.Filesystem.Path.GetPathRoot(ss.Path));
                    ss.SnapshotPath = ss.Vss.GetSnapshotPath(ss.Path).TrimEnd('\\');
                    Log($"created: {ss.SnapshotPath}");
                    vssSnapshots[i++] = ss;
                }
            }


            Log($"\n------------------ Creating a ZIP file and backing up all volumes ------------------");
            using (var outStream = new FileStream(destinationFilename, FileMode.CreateNew, FileAccess.Write, FileShare.None, (int)manager.BufferSize))
            {
                using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
                {
                    // create a data structure to save metadata about the discs backed up
                    var metadata = new Metadata();
                    metadata.StartTime = DateTime.Now;
                    metadata.PhysicalDisks.Add(disc);

                    (var mbr, metadata.MbrDecoded) = manager.ReadMBR(disc.DeviceId);

                    (var gpt1, var gpt2, var gpt1Decoded, var gpt2Decoded) = manager.ReadGPTs(disc.DeviceId, disc.LogicalSectorSize, disc.Size);
                    metadata.Gpt1Decoded = gpt1Decoded;
                    metadata.Gpt2Decoded = gpt2Decoded;

                    AddEntry(archive, mbr , "mbr");
                    AddEntry(archive, gpt1, "gpt1");
                    AddEntry(archive, gpt2, "gpt2");

                    var sectorBySectorCopy = true;
                    if (sectorBySectorCopy)
                    {
                        Log($"Backing up the complete disk sector by sector");
                        var zipStream = AddEntry(archive, $"wholedisk{disc.DeviceId}");
                        manager.ReadFromDiskToStream(disc.Path, disc.Size, zipStream, MyProgressHandler);
                    }
                    else
                    {
                        i = 0;
                        foreach (var p in disc.Partitions)
                        {
                            Log($"Partition {p.PartitionNumber}");
                            _indentation = "    ";
                            Log(SomePartitionInfo(p));

                            p.BackupFilename = $"partition{p.PartitionNumber}_volume{(p.Volume.HasDriveLetter ? p.Volume.DriveLetter : "-")}";
                            var zipStream = AddEntry(archive, p.BackupFilename);

                            var sourcePath = "";
                            if (p.Volume.HasDriveLetter)
                            {
                                AddSomeDebugInfo(disc, p, metadata.Gpt1Decoded, raw:false);
                                sourcePath = GetSourcePathForVssOfVolume(p, vssSnapshots, i);
                                Log($"Saving {sourcePath}...");
                                manager.ReadFromDiskToStream(sourcePath, p.Volume.Size, zipStream, MyProgressHandler);
                                i++;
                            }
                            else
                            {
                                if (p.Volume.UniqueId == null || p.Volume.Size == 0)
                                {
                                    AddSomeDebugInfo(disc, p, metadata.Gpt1Decoded, raw:true);
                                    sourcePath = GetSourcePathForPartition(p, disc);
                                    Log($"Saving {sourcePath}...");
                                    manager.ReadFromDiskToStream(sourcePath, p.Size, zipStream, MyProgressHandler);
                                }
                                else
                                {
                                    AddSomeDebugInfo(disc, p, metadata.Gpt1Decoded, raw:false);
                                    sourcePath = GetSourcePathForVolume(p, manager, driveLetter);
                                    Log($"Saving {sourcePath}...");
                                    manager.ReadFromDiskToStream(sourcePath, p.Volume.Size, zipStream, MyProgressHandler);
                                }
                            }

                            Log($"Finished");
                            Log($"");
                            _indentation = "";
                        }
                    }


                    Log($"Backup successful, now closing the ZIP archive");
                    metadata.Log = _completeLog;
                    metadata.EndTime = DateTime.Now;
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(metadata, jsonOptions);
                    AddEntry(archive, json, "metadata");

                    _indentation = "";
                }
            }

            Log($"Backup successful");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            Log("If you get an access denied exception, you need to run the program as administrator.");
            Log($"Backup ended with errors.");
        }
        finally
        {
            Log($"releasing all snapshots");
            foreach(var snapshot in vssSnapshots.Where(s => s != null && s.Vss != null))
                snapshot.Vss.Dispose();
        }
    }

    private static string GetSourcePathForVssOfVolume(Partition p, Snapshot[] vssSnapshots, int i)
    {
        string sourcePath = vssSnapshots[i].SnapshotPath;
        if (string.IsNullOrEmpty(sourcePath))
            throw new Exception($"Volume GUID is invalid for partition no.{p.PartitionNumber} GUID {p.Guid} ");
        return sourcePath;
    }

    private static string GetSourcePathForPartition(Partition p, PhysicalDisk disc)
    {
        return $@"\\.\GLOBALROOT\Device\Harddisk{disc.DeviceId}\Partition{p.PartitionNumber}";
    }

    private static string GetSourcePathForVolume(Partition p, PhysicalDiskManager manager, string driveLetter)
    {
        string sourcePath;
        var volumeGuid = p.Volume.UniqueId ?? null;
        if (string.IsNullOrEmpty(volumeGuid))
            throw new Exception($"Volume GUID is invalid for drive letter {driveLetter} ");

        sourcePath = manager.GetDosDevicenameForVolumeUniqueId(volumeGuid!);
        if (string.IsNullOrEmpty(sourcePath))
            throw new Exception($"Volume GUID is invalid for partition no.{p.PartitionNumber} GUID {p.Guid} ");

        return sourcePath;
    }

    private static string SomePartitionInfo(Partition? p)
    {
        return $"Size {SizeFormatter.Format(p.Size)} " +
                                    $"DriveLetter {(p.Volume.HasDriveLetter ? p.Volume.DriveLetter : "-")}  " +
                                    $"FileSystem {p?.Volume?.FileSystem}  " +
                                    $"Type {p.GptTypeDesc}";
    }

    private static void AddSomeDebugInfo(PhysicalDisk disc, Partition p, GptContents gptDecoded, bool raw)
    {
        int index = (int)p.PartitionNumber - 1;

        var firstLba        = gptDecoded.PartitionTable[index].FirstLBA;
        var lastLba         = gptDecoded.PartitionTable[index].LastLBA;
        var firstLbaOffset  = firstLba * disc.LogicalSectorSize;
        var lastLbaOffset   = lastLba  * disc.LogicalSectorSize;
        Log($"GPT says from LBA {firstLba,10} to LBA {lastLba,10} (from {firstLbaOffset,10} to {lastLbaOffset,10})");

        if (raw)
        {
            var partStart       = p.Offset;
            var partEnd         = p.Offset + p.Size;
            Log($"Saving raw sectors for this partition,          from {partStart,10} to {partEnd,10}...");
        }
    }

    private static ZipArchiveEntry AddEntry(ZipArchive archive, string text, string filenameInZip)
    {
        var data = Encoding.UTF8.GetBytes(text);
        return AddEntry(archive, data, filenameInZip);
    }

    private static ZipArchiveEntry AddEntry(ZipArchive archive, byte[] chunk, string filenameInZip)
    {
        var entry = archive.CreateEntry(filenameInZip, CompressionLevel.SmallestSize);
        using (var destinationStream = entry.Open())
        using (var fileToCompressStream = new MemoryStream(chunk))
            fileToCompressStream.CopyTo(destinationStream);
        return entry;
    }

    private static Stream AddEntry(ZipArchive archive, string filenameInZip)
    {
        var entry = archive.CreateEntry(filenameInZip, CompressionLevel.SmallestSize);
        Stream zipStream = null;
        zipStream = entry.Open();
        return zipStream;
    }

    private static void MyProgressHandler(PhysicalDiskManager.ProgressData data)
    {
        Console.Write($"{_indentation}Progress: {data.Percentage:N1} %       \r");
    }

    private static string GetTempDirectory()
    {
        var destinationDirectory = @$"C:\Temp";
        if (!Directory.Exists(destinationDirectory))
            destinationDirectory = Path.GetTempPath();
        return destinationDirectory;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"{_indentation}{message}");
        _completeLog += $"{_indentation}{message}";
    }
}