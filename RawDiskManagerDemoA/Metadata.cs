using RawDiskManager;

namespace RawDiskManagerDemoA
{
    internal class Metadata
    {
        public List<PhysicalDisk> PhysicalDisks { get; set; } = new();
        public MbrContents MbrDecoded { get; set; }
        public GptContents GptDecoded { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Log { get; set; }
    }
}
