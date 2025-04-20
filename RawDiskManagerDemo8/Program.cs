using RawDiskManager;
using VssSample;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace RawDiskManagerDemo8;

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
        try
        {
            Console.WriteLine("RawDiskManager Demo 8: copy a volume to a file, using AlphaVSS, in 2 variants: using volume shadow copy and directly");


            var driveLetter = "K";  // <---------------- CHANGE THIS TO YOUR DRIVE LETTER ! // choose a small disk, at best an empty partition

            var destinationDirectory = GetTempDirectory();

            var testfile = @$"{driveLetter}:\VSS_TESTFILE.TXT";
            File.WriteAllText(testfile, "ORIGINALCONTENT");
            Console.WriteLine($"Created test file {testfile}");


            Console.WriteLine($"Enumerate all discs, partitions and volumes");
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();
            var disc = manager.GetDiscByDriveLetter(physicalDiscs, driveLetter);
            if (disc is null)
                throw new Exception($"Device with drive letter {driveLetter} not found");

            // search for the volume with the given drive letter
            var volume = manager.GetVolumeByDriveLetter(disc, driveLetter);
            if (volume is null)
                throw new Exception($"Volume with drive letter {driveLetter} not found on device {disc.DeviceId}");
            var volumeGuid = volume.UniqueId ?? null;
            if (string.IsNullOrEmpty(volumeGuid))
                throw new Exception($"Volume GUID is invalid for drive letter {driveLetter} ");

            var sourcePath = manager.GetDosDevicenameForVolumeUniqueId(volumeGuid);
            var destinationFilename1 = Path.Combine(destinationDirectory, $"Volume{disc!.DeviceId}_directly.bin");
            var destinationFilename2 = Path.Combine(destinationDirectory, $"Volume{disc!.DeviceId}_vss.bin");


            Console.WriteLine($"Initializing the shadow copy subsystem for the volume");
            using (VssBackup vss = new VssBackup())
            {
                string vssPath = @$"{driveLetter}:\";
                vss.Setup(Path.GetPathRoot(vssPath));
                string snapshot = vss.GetSnapshotPath(vssPath).TrimEnd('\\');

                File.WriteAllText(testfile, "FIRSTCHANGE");


                Console.WriteLine($"Reading volume directly: {sourcePath} ({SizeFormatter.Format(volume.Size)})");
                Console.WriteLine($"Saving to {destinationFilename1}");
                manager.ReadFromDiskAndSaveToFile(sourcePath, volume.Size, destinationFilename1);

                Console.WriteLine($"Reading volume from VSS: {snapshot} ({SizeFormatter.Format(volume.Size)})");
                Console.WriteLine($"Saving to {destinationFilename2}");
                manager.ReadFromDiskAndSaveToFile(snapshot, volume.Size, destinationFilename2);
            }


            // When you open the file "destinationFilename1", you will find the content "FIRSTCHANGE",
            // because we're reading the volume directly, getting the latest content.
            // When you open the file "destinationFilename2", you will find "ORIGINALCONTENT",
            // because we're reading the snapshot.
            // This proves the VSS subsystem is working correctly.
            // To do a complete image backup of a physical disk, you would need to enumerate all volumes,
            // create a snapshot for each volume, then read all the volumes from the snapshot.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   
    }

    private static string GetTempDirectory()
    {
        var destinationDirectory = @$"C:\Temp";
        if (!Directory.Exists(destinationDirectory))
            destinationDirectory = Path.GetTempPath();
        return destinationDirectory;
    }
}