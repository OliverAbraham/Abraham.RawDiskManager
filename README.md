# Abraham.RawDiskManager

![](https://img.shields.io/github/downloads/oliverabraham/Abraham.RawDiskManager/total) ![](https://img.shields.io/github/license/oliverabraham/Abraham.RawDiskManager) ![](https://img.shields.io/github/languages/count/oliverabraham/Abraham.RawDiskManager) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham/Abraham.RawDiskManager?label=repo%20stars) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham?label=user%20stars)



## OVERVIEW

Read,write data from/to physical disks, partitions, volumes. 
Read/write/analyze MBR and GPT, disc structures. 
Create discs, partitions, volumes.


## CREDITS
Many thanks to Michael Bisbjerg and his RawDiskLib and DiskLib:
https://github.com/LordMike/RawDiskLib

Many thanks to Peter Palotas and his AlphaVSS and AlphaFS libraries: 
https://github.com/alphaleonis/AlphaVSS-Samples


## LICENSE

Licensed under Apache licence.
https://www.apache.org/licenses/LICENSE-2.0


## Compatibility

The nuget package was build with DotNET Standard 2.1



## INSTALLATION

Install the Nuget package "Abraham.RawDiskManager" into your application (from https://www.nuget.org).
Please follow my samples to see how to use it.

For example, to analyze a physical harddisk, add the following code:
```C#
using Abraham.RawDiskManager;

var manager = new PhysicalDiskManager();
var physicalDiscs = manager.GetPhysicalDiscs();
```


## DEMOS

I have included several demos in the repository, to guide you:
- Demo 1: enumerate all devices
- Demo 2: read and analyze MBR and both GPTs (Guid Partition Tables)
- Demo 3: reading physical disks, partitions and volumes
- Demo 4: backup a physical disk completely
- Demo 5: restore a physical disk completely
- Demo 6: backup a physical disk and compress the data on the fly
- Demo 7: copy one file using AlphaVSS (volume shadow copy service) and AlphaFS
- Demo 8: copy a volume to a file, using AlphaVSS, in 2 variants: using volume shadow copy and directly
- Demo 9: backup a physical disk completely (image backup using VSS and on-the-fly zip compression) 
- Demo A: restore a physical disk completely / from zip file created by Demo9

**Note**: 
Demos 9 and A are preliminary and not fully working. Currently I'm on it.
When they're working, I will most likely move them to my BackupRestore repository.



## BACKUP AND RESTORE

If you intend to do backup and restore, look at my BackupRestore package!
It contains the logic to 
- create image backups using VSS
- create incremental file backups using VSS
- restore images



## COMMENTS 

### Useful links
- https://en.wikipedia.org/wiki/Disk_partitioning
- https://en.wikipedia.org/wiki/Master_boot_record
- https://en.wikipedia.org/wiki/GUID_Partition_Table

### GPT partitioning 
Storage of GPTs (Guid Partition Tables) is a bit weird. On every disc, two copies exist with different structure.
- The disc starts with a dummy MBR at LBA 0 (logical block address). It's called "protective MBR".
- The first GPT has header first at LBA 1, then the partition table at LBA 2.
- The second is at the end of the disc, starting with the table and ending with the header in the very last LBA.

If you want to restore an image backup to a new disc, you will most likely not have the exact same size than your backup has. 
So you want to relocate the second GPT to the end of the new disc.
For this, you need to adjust the pointers in both GPTs.
I tried restoring a sector-by-sector backup to a larger disk 1:1 without relocation. 
You can start the OS, but I don't recommend it.

Identification of Partitions in GPT partitioning scheme is done by reading the partition type GUID 
from the partition table and looking it up in a table.
The table is included on page https://en.wikipedia.org/wiki/GUID_Partition_Table.
I have added that table as class "PartitionTypeGuid" to my library, 

### Hybrid MBR (LBA 0 + GPT)
My library currently does not support hybrid MBRs.

### Volume shadow copy service
If you create a snapshot, you can use it for both image and file backups.
So it doesn't matter if you want to do a full image backup of your C drive or an incremental file backup.

### Performance
My demo 6 does a full image backup a physical disk and compresses the data on the fly with the standard ZIP algorithm.
The algorithm is straight forward and not optimized for speed. It only uses one core. There's room for improvement.



## HOW TO INSTALL A NUGET PACKAGE
This is very simple:
- Start Visual Studio (with NuGet installed) 
- Right-click on your project's References and choose "Manage NuGet Packages..."
- Choose Online category from the left
- Enter the name of the nuget package to the top right search and hit enter
- Choose your package from search results and hit install
- Done!


or from NuGet Command-Line:

    Install-Package Abraham.RawDiskManager


## AUTHOR

Oliver Abraham, mail@oliver-abraham.de, https://www.oliver-abraham.de

Please feel free to comment and suggest improvements!



## SOURCE CODE

The source code is hosted at:

https://github.com/OliverAbraham/Abraham.RawDiskManager

The Nuget Package is hosted at: 

https://www.nuget.org/packages/Abraham.RawDiskManager



## SCREENSHOTS




# MAKE A DONATION !

If you find this library useful, buy me a coffee!
I would appreciate a small donation on https://www.buymeacoffee.com/oliverabraham

<a href="https://www.buymeacoffee.com/app/oliverabraham" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>
