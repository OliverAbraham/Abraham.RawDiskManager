namespace RawDiskManager
{
    public static class SizeFormatter
    {
        public static string Format(ulong size)
        {
            if (size >= 1024 * 1024 * 1024)
                return $"{size / (1024 * 1024 * 1024)} GB";
            else if (size >= 1024 * 1024)
                return $"{size / (1024 * 1024)} MB";
            else if (size >= 1024)
                return $"{size / 1024} KB";
            else
                return $"{size} B";
        }
    }
}