using System.Collections.Generic;

namespace RawDiskManager
{
    public class PhysicalDisk
    {
        public ulong                AllocatedSize                    { get; set; }                                  
        public BusType              BusType                          { get; set; }                                  
        public ushort[]             CannotPoolReason                 { get; set; } = new ushort[0];                 
        public bool                 CanPool                          { get; set; }                                  
        public string?              Description                      { get; set; }                                  
        public int                  DeviceId                         { get; set; }                                  
        public int                  EnclosureNumber                  { get; set; }                                  
        public string?              FirmwareVersion                  { get; set; }                                  
        public string?              FriendlyName                     { get; set; }                                  
        public ushort               HealthStatus                     { get; set; }                                  
        public bool                 IsIndicationEnabled              { get; set; }                                  
        public bool                 IsPartial                        { get; set; }                                  
        public ulong                LogicalSectorSize                { get; set; }                                  
        public MediaType            MediaType                        { get; set; }                                  
        public string[]             OperationalDetails               { get; set; } = new string[0];                 
        public OperationalStatus[]  OperationalStatus                { get; set; } = new OperationalStatus[0];      
        public string?              OtherCannotPoolReasonDescription { get; set; }                                  
        public string?              PartNumber                       { get; set; }                                  
        public string?              PhysicalLocation                 { get; set; }                                  
        public ulong                PhysicalSectorSize               { get; set; }                                  
        public ulong                Size                             { get; set; }                                  
        public int                  SlotNumber                       { get; set; }                                  
        public string?              SoftwareVersion                  { get; set; }                                  
        public uint                 SpindleSpeed                     { get; set; }                                  
        public ushort[]             SupportedUsages                  { get; set; } = new ushort[0];                 
        public string?              UniqueId                         { get; set; }                                  
        public ushort               UniqueIdFormat                   { get; set; }                                  
        public ushort               Usage                            { get; set; }                                  
        public ulong                VirtualDiskFootprint             { get; set; }                                  
        public string?              ObjectId                         { get; set; }                                  
        public List<Partition>      Partitions                       { get; set; } = new List<Partition>();         
        public string?              VolumeName                       { get; set; }                                  


        public string?              Path                             { get; set; }
        public string?              Location                         { get; set; }
        public uint                 Number                           { get; set; }
        public string?              SerialNumber                     { get; set; }
        public string?              Manufacturer                     { get; set; }
        public string?              Model                            { get; set; }
        public ulong                LargestFreeExtent                { get; set; }
        public uint                 NumberOfPartitions               { get; set; }
        public ushort               ProvisioningType                 { get; set; }
        public ushort               PartitionStyle                   { get; set; }
        public uint                 Signature                        { get; set; }
        public string?              Guid                             { get; set; }
        public bool                 IsOffline                        { get; set; }
        public ushort               OfflineReason                    { get; set; }
        public bool                 IsReadOnly                       { get; set; }
        public bool                 IsSystem                         { get; set; }
        public bool                 IsClustered                      { get; set; }
        public bool                 IsBoot                           { get; set; }
        public bool                 BootFromDisk                     { get; set; }
    }
}
