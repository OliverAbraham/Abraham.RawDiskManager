using RawDiskManager;

namespace RawDiskManagerDemo9
{
    internal class Metadata
    {
        public List<PhysicalDisk> PhysicalDisks { get; set; } = new();
        public MbrContents        MbrDecoded    { get; set; }
        public GptContents        Gpt1Decoded   { get; set; }
        public GptContents        Gpt2Decoded   { get; set; }
        public DateTime           StartTime     { get; set; }
        public DateTime           EndTime       { get; set; }
        public string             Log           { get; set; }
    }
}
