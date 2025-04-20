using System;
using System.Collections.Generic;

namespace RawDiskManager
{
    public class Partition
    {
        public string?              Guid                 { get; set; }
        public ulong                Size                 { get; set; }
        public ulong                Offset               { get; set; }
        public string?              DriveLetter          { get; set; }
        public bool                 IsBootable           { get; set; }
        public bool                 IsReadOnly           { get; set; }
        public bool                 IsOffline            { get; set; }
        public UInt32               DiskNumber           { get; set; }
        public UInt32               PartitionNumber      { get; set; }
        public string[]             AccessPaths          { get; set; } = new string[0];
        public OperationalStatus    OperationalStatus    { get; set; } = OperationalStatus.Unknown;
        public UInt16               TransitionState      { get; set; }
        public ushort               MbrType              { get; set; }
        public string?              GptType              { get; set; }
        public bool                 IsSystem             { get; set; }
        public bool                 IsBoot               { get; set; }
        public bool                 IsActive             { get; set; }
        public bool                 IsHidden             { get; set; }
        public bool                 IsShadowCopy         { get; set; }
        public bool                 NoDefaultDriveLetter { get; set; }
        public List<Volume>         Volumes              { get; set; } = new List<Volume>();
        public Volume               Volume               { get; set; } = new Volume();
        public string?              GptTypeDesc          { get; set; }
        public string?              MbrTypeDesc          { get; set; }
        public string?              BackupFilename       { get; set; }
    }
}
