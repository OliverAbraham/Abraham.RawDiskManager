using RawDiskManager;
using static RawDiskManager.PhysicalDiskManager;

namespace RawDiskManagerDemo5;

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
        Console.WriteLine("RawDiskManager Demo 5: restore a physical disk completely");



        var deviceID = 4;  // <---------------- CHANGE THIS TO YOUR DRIVE ! // choose a small disk, i.e. a USB drive!
                           // <---------------- look up this number in disc management



        var fileID = 1;
        string destinationDirectory = GetTempDirectory();
        var filename1 = Path.Combine(destinationDirectory, $"PhysicalDisc{fileID}_mbr.bin");
        var filename2 = Path.Combine(destinationDirectory, $"PhysicalDisc{fileID}_gpt.bin");
        var filename3 = Path.Combine(destinationDirectory, $"PhysicalDisc{fileID}_gpt_copy.bin");
        var filename4 = Path.Combine(destinationDirectory, $"PhysicalDisc{fileID}_AllPartitions.bin");

        if (!File.Exists(filename1)) throw new Exception($"File {filename1} not found");
        if (!File.Exists(filename2)) throw new Exception($"File {filename2} not found");
        if (!File.Exists(filename3)) throw new Exception($"File {filename3} not found");
        if (!File.Exists(filename4)) throw new Exception($"File {filename4} not found");


        try
        {
            Console.WriteLine($"------------------ looking up disc ------------------");
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();
            var disc = manager.GetDiscByDeviceID(physicalDiscs, deviceID);
            if (disc is null)
                throw new Exception($"WARNING: Device with ID {deviceID} not found");
            var destinationPath = @$"\\.\PHYSICALDRIVE{deviceID}";




            Console.WriteLine($"------------------ checking backup data integrity------------------");
            var mbr = File.ReadAllBytes(filename1);
            Console.WriteLine($"Read MBR ({mbr.GetLength(0)} Bytes)");
            if (mbr.GetLength(0) != 512)
                throw new Exception($"MBR file {filename1} is not 512 bytes long");
            Console.WriteLine($"MBR is OK");

            var gpt1 = File.ReadAllBytes(filename2);
            Console.WriteLine($"Read GPT1 ({gpt1.GetLength(0)} Bytes)");
            if (gpt1.GetLength(0) < 512)
                throw new Exception($"GPT file {filename2} is not 512 bytes long");
            var gptParser = new GptParser(disc.LogicalSectorSize);
            var gptDecoded = gptParser.Parse(gpt1);
            Console.WriteLine($"GPT1 is OK");

            var gpt2 = File.ReadAllBytes(filename3);
            Console.WriteLine($"Read GPT2 ({gpt2.GetLength(0)} Bytes)");
            if (gpt2.GetLength(0) < 512)
                throw new Exception($"GPT file {filename3} is not 512 bytes long");
            Console.WriteLine($"GPT2 is OK");

            var fileInfo = new FileInfo(filename4);
            if ((ulong)fileInfo.Length > disc.Size)
                throw new Exception($"Destination disk is too small for restore. " +
                    $"Backup data is {SizeFormatter.Format((ulong)fileInfo.Length)}, " + 
                    $"but the target disc only has a capacity of {SizeFormatter.Format(disc.Size)}");
            else if ((ulong)fileInfo.Length < disc.Size)
                Console.WriteLine($"Destination disk is larger than necessary. " + 
                    $"Backup data is {SizeFormatter.Format((ulong)fileInfo.Length)}, " + 
                    $"but the target disc has a capacity of {SizeFormatter.Format(disc.Size)}");

            destinationPath = disc.Path;
            if (destinationPath is null)
                throw new Exception($"sourcePath is invalid for disc {disc.DeviceId}");
            Console.WriteLine($"Partition image is OK");




            Console.WriteLine($"ATTENTION!!!!! YOU ARE ABOUT TO OVERWRITE A DISC COMPLETELY !!!!  press y to continue");
            if (Console.ReadKey().KeyChar != 'y') return;
            Console.WriteLine($"ATTENTION!!!!! ALL DATA ON THE DISC WITH ID {deviceID} SIZE {SizeFormatter.Format(disc.Size)} WILL BE LOST  press y to continue\");");
            if (Console.ReadKey().KeyChar != 'y') return;
            Console.WriteLine($"ATTENTION!!!!! ALL DATA ON THE DISC WITH ID {deviceID} SIZE {SizeFormatter.Format(disc.Size)} WILL BE LOST  press y to continue\");");
            if (Console.ReadKey().KeyChar != 'y') return;
            Console.WriteLine();



            Console.WriteLine($"------------------ restoring MBR ------------------");
            manager.WritePhysicalSectors(destinationPath, mbr, 0, 512);
            Console.WriteLine($"Wrote {mbr.GetLength(0)} Bytes to disc");



            Console.WriteLine($"------------------ restoring GPT 1 ------------------");
            manager.WritePhysicalSectors(destinationPath, gpt1, 512, (ulong)gpt1.GetLength(0));
            Console.WriteLine($"Wrote {gpt1.GetLength(0)} Bytes to disc");



            Console.WriteLine($"------------------ restoring GPT 2 ------------------");
            manager.WritePhysicalSectors(destinationPath, gpt2, gptDecoded.BackupLBA * 512, (ulong)gpt2.GetLength(0));
            Console.WriteLine($"Wrote {gpt2.GetLength(0)} Bytes to disc");



            Console.WriteLine($"------------------ restoring all partitions of the physical disc (all at once, sector by sector) ------------------");
            Console.WriteLine($"Reading from {filename4}");
            // ATTENTION:
            // In Demo4, we've also read the MBR and the first GPT in filename4, because they're part of the LBA adressing.
            // We need to keep that in mind. Means, we should skip the first n sectors of filename4 (1 for the MBR and m for the first GPT)
            manager.WriteFromFileToDisk(filename4, destinationPath, gptDecoded.FirstUsableLBA*512, gptDecoded.FirstUsableLBA*512, MyProgressHandler);
            Console.WriteLine($"Restore successful.");
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
