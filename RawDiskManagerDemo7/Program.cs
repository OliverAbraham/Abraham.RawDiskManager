using VssSample;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace RawDiskManagerDemo7;

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
        try
        {
            Console.WriteLine("RawDiskManager Demo 7: copy one file using AlphaVSS (volume shadow copy service) and AlphaFS");
            // taken from https://github.com/alphaleonis/AlphaVSS-Samples


            /// <summary>
            /// This class encapsulates some simple VSS logic.  Its goal is to allow
            /// a user to backup a single file from a shadow copy (presumably because
            /// that file is otherwise unavailable on its home volume).
            /// </summary>
            /// <example>
            /// This code creates a shadow copy and copies a single file from
            /// the new snapshot to a location on the D drive.  Here we're
            /// using the AlphaFS library to make a full-file copy of the file.
            /// <code>
            string source_file = @"C:\Windows\system32\config\sam";
            string backup_root = GetTempDirectory();
            string backup_path = Path.Combine(backup_root, Path.GetFileName(source_file));

            // Initialize the shadow copy subsystem.
            using (VssBackup vss = new VssBackup())
            {
                vss.Setup(Path.GetPathRoot(source_file));
                string snap_path = vss.GetSnapshotPath(source_file);

                // Here we use the AlphaFS library to make the copy.
                Alphaleonis.Win32.Filesystem.File.Copy(snap_path, backup_path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("If you get an access denied exception, you need to run the program as administrator.");
        }   
    }

    private static string GetTempDirectory()
    {
        var destinationDirectory = @$"C:\Temp";
        if (!Directory.Exists(destinationDirectory))
            destinationDirectory = Path.GetTempPath();
        return destinationDirectory;
    }
}