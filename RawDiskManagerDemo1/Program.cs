using RawDiskManager;

namespace RawDiskManagerDemo;

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
        Console.WriteLine("RawDiskManager Demo 1: enumerate all devices");




        try
        {
            var manager = new PhysicalDiskManager();
            var physicalDiscs = manager.GetPhysicalDiscs();

            foreach (var disc in physicalDiscs)
            {
                Console.WriteLine($"DeviceID         : {disc.DeviceId}");
                Console.WriteLine($"Friendly Name    : {disc.FriendlyName}");
                Console.WriteLine($"Volume label     : {disc.VolumeName}");
                Console.WriteLine($"Description      : {disc.Description}");
                Console.WriteLine($"Size             : {SizeFormatter.Format(disc.Size)}  Allocated: {SizeFormatter.Format(disc.AllocatedSize)}");
                Console.WriteLine($"Firmware Version : {disc.FirmwareVersion}");
                Console.WriteLine($"Bus Type         : {disc.BusType}");
                Console.WriteLine($"Media type       : {disc.MediaType}");
                Console.WriteLine($"Online status    : {(disc.IsOffline ? "Offline" : "Online")} ({disc.OfflineReason})");
                Console.WriteLine($"OperationalStatus: {string.Join(',', disc.OperationalStatus)}");
                Console.WriteLine($"Disc path        : {disc.Path}");
                Console.WriteLine($"Partitions:");
                Console.WriteLine($"{"DeviceID",-8} {"Part#",-5} {"Size",-10} {"Offset",20}   {"End",20}   {"DriveLetter",-12} " +
                                      $"{"FileSystem",-10} {"FreeSpace",10}  {"Bootable",-8} {"ReadOnly",-8} {"Offline",-8} " +
                                      $"{"OperationalStatus",-20} {"TransitionState",-15} {"MbrType",-10} {"GptType",-17} {"IsSystem",-10} " +
                                      $"{"IsBoot",-7} {"IsActive",-10} {"IsHidden",-10} {"ShadowCopy",-11} {"NoDefDrvLtr",-11} " +
                                      $"{"AccessPaths",-15} ");
                Console.WriteLine($"------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
                ulong lastPartitionEnd = 0;
                foreach (var p in disc.Partitions)
                {
                    Console.WriteLine(
                        $"{p.DiskNumber,-8} {p.PartitionNumber,-5} " +
                        $"{SizeFormatter.Format(p.Size),10} " + 
                        $"{p.Offset,20}   " +
                        $"{p.Offset+p.Size,20}   " +
                        $"{p.DriveLetter,-12} " +
                        $"{p.Volume.FileSystem,-10} {SizeFormatter.Format(p.Volume.FreeSpace),10}  {p.IsBootable,-8} {p.IsReadOnly,-8} {p.IsOffline,-8} " +
                        $"{p.OperationalStatus,-20} {p.TransitionState,-15} {p.MbrTypeDesc,-10} {p.GptTypeDesc,-17} {p.IsSystem,-10} " +
                        $"{p.IsBoot,-7} {p.IsActive,-10} {p.IsHidden,-10} {p.IsShadowCopy,-11} {p.NoDefaultDriveLetter,-11} " +
                        $"{(p.AccessPaths != null ? string.Join(',', p.AccessPaths) : ""),-15}");
                    lastPartitionEnd = p.Offset + p.Size;
                }
            
                if (lastPartitionEnd < disc.Size)
                    Console.WriteLine("Free space at the end: " + SizeFormatter.Format(disc.Size - lastPartitionEnd));

                Console.WriteLine();
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }
    }
}
