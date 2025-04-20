using RawDiskManager;

namespace RawDiskManagerDemo3;

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
        Console.WriteLine("RawDiskManager Demo 3: reading physical disks, partitions and volumes");



        var driveLetter = "H";  // <---------------- CHANGE THIS TO YOUR DRIVE LETTER ! // choose a small disk, i.e. a USB drive!



        try
        {
            string destinationDirectory = GetTempDirectory();
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();



            Console.WriteLine($"------------------ find the disc that has a partition with this drive letter ------------------");
            var disc1 = manager.GetDiscByDriveLetter(physicalDiscs, driveLetter);
            if (disc1 is null)
                throw new Exception($"Device with drive letter {driveLetter} not found");
            var sourcePath = disc1.Path;
            if (sourcePath is null)
                throw new Exception($"sourcePath is invalid for drive letter {driveLetter}");




            Console.WriteLine($"------------------ read a physical disc completely ------------------");
            var destinationFilename = Path.Combine(destinationDirectory, $"PhysicalDisc{disc1.DeviceId}.img");
            Console.WriteLine($"Reading {sourcePath} with size {SizeFormatter.Format(disc1.Size)}");
            Console.WriteLine($"Saving data to {destinationFilename}");
            manager.ReadFromDiskAndSaveToFile(sourcePath, disc1.Size, destinationFilename, MyProgressHandler);



            
            // Console.WriteLine($"------------------ read a partition ------------------");
            // driveLetter = "D";
            // var disc2 = manager.GetDiscByDriveLetter(physicalDiscs, driveLetter);
            // if (disc2 is null)
            //     throw new Exception($"Device with drive letter {driveLetter} not found");
            // var partitionNumber = 2; // in my computer, this is a 16 MB UEFI partition with no drive letter
            // var partition = manager.GetPartitionByNumber(disc2, partitionNumber);
            // if (partition is null)
            //     throw new Exception($"Partition {partitionNumber} not found on device {disc2.DeviceId}");
            // sourcePath = $@"\\.\GLOBALROOT\Device\Harddisk{disc2.DeviceId}\Partition{partition.PartitionNumber}";
            // destinationFilename = Path.Combine(destinationDirectory, $"Harddisc{disc2.DeviceId}_Partition{partition.PartitionNumber}_Drive{driveLetter}.bin");
            // Console.WriteLine($"Reading {sourcePath} with size {SizeFormatter.Format(partition.Size)}");
            // Console.WriteLine($"Saving data to {destinationFilename}");
            // manager.ReadFromDiskAndSaveToFile(sourcePath, partition.Size, destinationFilename, MyProgressHandler);




            // Console.WriteLine($"------------------ read a volume ------------------");
            // driveLetter = "E";
            // var disc3 = manager.GetDiscByDriveLetter(physicalDiscs, driveLetter);
            // if (disc3 is null)
            //     throw new Exception($"Device with drive letter {driveLetter} not found");
            // var volume = manager.GetVolumeByDriveLetter(disc3, driveLetter);
            // if (volume is null)
            //     throw new Exception($"Volume with drive letter {driveLetter} not found on device {disc3.DeviceId}");
            // var volumeGuid = volume.UniqueId ?? null;
            // if (string.IsNullOrEmpty(volumeGuid))
            //     throw new Exception($"Volume GUID is invalid for drive letter {driveLetter} ");
            // sourcePath = manager.GetDosDevicenameForVolumeUniqueId(volumeGuid);
            // destinationFilename = Path.Combine(destinationDirectory, $"VolumeHarddisk{disc3!.DeviceId}Partition{partition.PartitionNumber}.bin");
            // Console.WriteLine($"Reading volume with GUID {volumeGuid} and size {SizeFormatter.Format(volume.Size)}");
            // Console.WriteLine($"Saving data to {destinationFilename}");
            // manager.ReadFromDiskAndSaveToFile(sourcePath, volume.Size, destinationFilename, MyProgressHandler);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   
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
