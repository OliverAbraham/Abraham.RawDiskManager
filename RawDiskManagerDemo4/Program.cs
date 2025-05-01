using RawDiskManager;
using System.CodeDom;

namespace RawDiskManagerDemo4;

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
        Console.WriteLine("RawDiskManager Demo 4: backup a physical disk completely");



        var deviceID = 1;  // <---------------- CHANGE THIS TO YOUR DRIVE ! // choose a small disk, i.e. a USB drive!
                           // <---------------- look up this number in disc management



        try
        {
            string destinationDirectory = GetTempDirectory();
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();

            var disc = manager.GetDiscByDeviceID(physicalDiscs, deviceID);
            if (disc is null)
                throw new Exception($"Device with ID {deviceID} not found");


            Console.WriteLine($"------------------ read MBR ------------------");
            var mbr = manager.ReadMBR(deviceID);
            File.WriteAllBytes(Path.Combine(destinationDirectory, $"PhysicalDisc{deviceID}_mbr.bin"), mbr.Item1);


            Console.WriteLine();
            Console.WriteLine($"------------------ read GPT ------------------");
            (var gpt1, var gpt2, var gpt1Decoded, var gpt2Decoded) = manager.ReadGPTs(deviceID, disc.LogicalSectorSize, disc.Size);
            File.WriteAllBytes(Path.Combine(destinationDirectory, $"PhysicalDisc{deviceID}_gpt1.bin"), gpt1);
            File.WriteAllBytes(Path.Combine(destinationDirectory, $"PhysicalDisc{deviceID}_gpt2.bin"), gpt2);



            Console.WriteLine();
            Console.WriteLine($"------------------ read a physical disc completely (all partitions at once, sector by sector) ------------------");
            // ATTENTION:
            // We're also reading here the MBR and the first GPT, because they're part of the LBA adressing.
            // So basically reading MBR and first GPT ahead is unnecessary.
            // the only advantage is that we can stop reading the disc when we reach the end of the last partition.
            // There could be free space between the end of the last partition and the start of the seconds GPT.
            var sourcePath = disc.Path;
            if (sourcePath is null)
                throw new Exception($"sourcePath is invalid for disc {disc.DeviceId}");
            (var sizeToRead, var messages) = manager.FindEndOfLastPartition(disc);

            var destinationFilename = Path.Combine(destinationDirectory, $"PhysicalDisc{disc.DeviceId}_AllPartitions.bin");
            Console.WriteLine($"Saving data to {destinationFilename}");

            manager.ReadFromDiskAndSaveToFile(sourcePath, sizeToRead, destinationFilename, MyProgressHandler);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   


        // an example how to find a physical disc by its volume label:
        //Console.WriteLine($"------------------ looking up disc ------------------");
        //var volumeLabel = "CompleteBackup";
        //var disc = manager.GetDiscByVolumeName(physicalDiscs, volumeLabel);
        //if (disc is null)
        //    throw new Exception($"Device with volume label {volumeLabel} not found");
        //var deviceID = disc.DeviceId;
        //Console.WriteLine($"Physical device with volume label {volumeLabel} has deviceID {deviceID} and size {manager.FormatDiskSize(disc.Size)}");
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
}
