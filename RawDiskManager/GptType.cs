namespace RawDiskManager
{
    public class GptType
    {
        public const string System            = "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}"; // An EFI system partition.
        public const string MicrosoftReserved = "{e3c9e316-0b5c-4db8-817d-f92df00215ae}"; // A Microsoft reserved partition.
        public const string BasicData         = "{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}"; // A basic data partition. This is the data partition type that is created and recognized by Windows. Only partitions of this type can be assigned drive letters, receive volume GUID paths, host mounted folders (also called volume mount points) and be enumerated by calls to FindFirstVolume and FindNextVolume.
        public const string LDMMetadata       = "{5808c8aa-7e8f-42e0-85d2-e1e90434cfb3}"; // A Logical Disk Manager (LDM) metadata partition on a dynamic disk.
        public const string LDMData           = "{af9b60a0-1431-4f62-bc68-3311714a69ad}"; // The partition is an LDM data partition on a dynamic disk.
        public const string MicrosoftRecovery = "{de94bba4-06d1-4d40-a16a-bfd50179d6ac}";
        public const string Unknown           = "";

        public static string GetTypeDescByGuid(string guid)
        {
            return guid switch
            {
                System            => nameof(System           ),
                MicrosoftReserved => nameof(MicrosoftReserved),
                BasicData         => nameof(BasicData        ),
                LDMMetadata       => nameof(LDMMetadata      ),
                LDMData           => nameof(LDMData          ),
                MicrosoftRecovery => nameof(MicrosoftRecovery),
                _                 => "Unknown",
            };
        }
    }
}
