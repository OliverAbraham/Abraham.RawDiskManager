namespace RawDiskManager
{
    public class MbrType
    {
        public enum Type
        {
            FAT12    = 1,
            FAT16    = 4,
            Extended = 5,
            Huge     = 6,
            IFS      = 7,
            FAT32    = 12,
        }

        public static string GetTypeDescByID(int id)
        {
            return id switch
            {
                1  => "FAT12",
                4  => "FAT16",
                5  => "Extende",
                6  => "Huge",
                7  => "IFS",
                12 => "FAT32",
                _  => "Unknown",
            };
        }
    }
}
