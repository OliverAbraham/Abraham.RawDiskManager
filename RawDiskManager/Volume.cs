using System;

namespace RawDiskManager
{
    public class Volume
    {
        public string?              ObjectId              { get; set; }
        public OperationalStatus[]  OperationalStatus     { get; set; } = new OperationalStatus[0];
        public ushort               HealthStatus          { get; set; }
        public string?              DriveType             { get; set; }
        public string?              FileSystemType        { get; set; }
        public string?              DedupMode             { get; set; }
        public string?              ReFSDedupMode         { get; set; }
        public string?              PassThroughClass      { get; set; }
        public string[]             PassThroughIds        { get; set; } = new string[0];
        public string?              PassThroughNamespace  { get; set; }
        public string?              PassThroughServer     { get; set; }
        public string?              UniqueId              { get; set; }
        public UInt32               AllocationUnitSize    { get; set; }
        public string?              DriveLetter           { get; set; }
        public string?              FileSystem            { get; set; }
        public string?              FileSystemLabel       { get; set; }
        public string?              Path                  { get; set; }
        public ulong                SizeRemaining         { get; set; }

        public uint                 SectorsPerCluster     { get; set; }
        public uint                 BytesPerSector        { get; set; }
        public uint                 NumberOfFreeClusters  { get; set; }
        public uint                 TotalNumberOfClusters { get; set; }
        public ulong                Size                    => TotalNumberOfClusters * AllocationUnitSize;
        public ulong                FreeSpace               => SizeRemaining;
        public bool                 HasDriveLetter          => !string.IsNullOrEmpty(DriveLetter);
    }
}
