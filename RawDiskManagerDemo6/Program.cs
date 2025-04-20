using RawDiskManager;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace RawDiskManagerDemo6;

internal class Program
{
    private static Stopwatch _elapsedTime;

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
        Console.WriteLine("RawDiskManager Demo 6: backup a physical disk and compress the data on the fly");



        var deviceID = 1;  // <---------------- CHANGE THIS TO YOUR DRIVE ! // choose a small disk, i.e. a USB drive!
                           // <---------------- look up this number in disc management



        try
        {
            string destinationDirectory = GetTempDirectory();
            var manager = new PhysicalDiskManager();
            manager.BufferSize = 100 * 1024 * 1024; // 100 MB buffer size
            Console.WriteLine($"Working buffer is {SizeFormatter.Format(manager.BufferSize)}");
            var physicalDiscs = manager.GetPhysicalDiscs();

            var disc = manager.GetDiscByDeviceID(physicalDiscs, deviceID);
            if (disc is null)
                throw new Exception($"Device with ID {deviceID} not found");


            Console.WriteLine();
            Console.WriteLine($"------------------ read a physical disc completely (all partitions at once, sector by sector) ------------------");
            var mbr = manager.ReadMBR(deviceID);
            (var gpt1, var gpt2, var gpt1Decoded, var gpt2Decoded) = manager.ReadGPTs(deviceID, disc.LogicalSectorSize, disc.Size);

            var sourcePath = disc.Path;
            if (sourcePath is null)
                throw new Exception($"sourcePath is invalid for disc {disc.DeviceId}");
            (var sizeToRead, var messages) = manager.FindEndOfLastPartition(disc);

            var destinationFilename = Path.Combine(destinationDirectory, $"ImageBackup_{disc.DeviceId}_{DateTime.Now.ToString("yyyyMMddhhmmss")}.zip");
            Console.WriteLine($"Saving data to {destinationFilename}");
            if (File.Exists(destinationFilename))
                File.Delete(destinationFilename);

            _elapsedTime = Stopwatch.StartNew();


            // Create a Zip file and compress all partition data on the fly
            using (var outStream = new FileStream(destinationFilename, FileMode.CreateNew, FileAccess.Write, FileShare.None, (int)manager.BufferSize))
            {
                using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
                {
                    var fileInArchive = archive.CreateEntry("mbr", CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    using (var fileToCompressStream = new MemoryStream(mbr))
                        fileToCompressStream.CopyTo(entryStream);

                    fileInArchive = archive.CreateEntry("gpt1", CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    using (var fileToCompressStream = new MemoryStream(gpt1))
                        fileToCompressStream.CopyTo(entryStream);

                    fileInArchive = archive.CreateEntry("gpt2", CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    using (var fileToCompressStream = new MemoryStream(gpt2))
                        fileToCompressStream.CopyTo(entryStream);

                    fileInArchive = archive.CreateEntry("partitions", CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    {
                        manager.ReadFromDiskToStream(sourcePath, sizeToRead, entryStream, MyProgressHandler);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   
    }

    private static void MyProgressHandler(PhysicalDiskManager.ProgressData data)
    {
        double speed = (double)data.CompletedBytes / (double)_elapsedTime.Elapsed.TotalSeconds;
        speed = speed / (1024*1024);

        Console.Write($"Progress: {data.Percentage:N1} %       Speed: {speed:N0} MB/sec            \r");
    }

    private static string GetTempDirectory()
    {
        var destinationDirectory = @$"C:\Temp";
        if (!Directory.Exists(destinationDirectory))
            destinationDirectory = Path.GetTempPath();
        return destinationDirectory;
    }
}
