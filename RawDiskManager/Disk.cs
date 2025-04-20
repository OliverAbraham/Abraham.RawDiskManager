namespace RawDiskManager
{
    public class Disk
    {
        public string?              Path                 { get; set; }
        public string?              Location             { get; set; }
        public uint                 Number               { get; set; }
        public string?              SerialNumber         { get; set; }
        public string?              Manufacturer         { get; set; }
        public string?              Model                { get; set; }
        public ulong                LargestFreeExtent    { get; set; }
        public uint                 NumberOfPartitions   { get; set; }
        public ushort               ProvisioningType     { get; set; }
        public ushort               PartitionStyle       { get; set; }
        public uint                 Signature            { get; set; }
        public string?              Guid                 { get; set; }
        public bool                 IsOffline            { get; set; }
        public ushort               OfflineReason        { get; set; }
        public bool                 IsReadOnly           { get; set; }
        public bool                 IsSystem             { get; set; }
        public bool                 IsClustered          { get; set; }
        public bool                 IsBoot               { get; set; }
        public bool                 BootFromDisk         { get; set; }
    }
}
