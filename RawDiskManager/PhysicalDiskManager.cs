using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

//[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("windows")]

namespace RawDiskManager
{
    /// <summary>
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
    public class PhysicalDiskManager
    {
        #region ------------- Types and constants -------------------------------------------------
        public delegate void ProgressHandler(ProgressData data);

        public class ProgressData
        {
            public ulong    TotalBytes              { get; internal set; }
            public ulong    CompletedBytes          { get; internal set; }
            public ulong    RemainingBytes          { get; internal set; }
            public double   Percentage              { get; internal set; }
            public double   TotalPercentage         { get; internal set; }
            public bool     IsCancellationRequested { get; internal set; }
            public bool     End                     { get; internal set; }
        }
        #endregion



        #region ------------- Properties ----------------------------------------------------------
        /// <summary>
        /// Holds the size of a buffer that will be allocated by read and write functions.
        /// </summary>
        public ulong BufferSize { get; set; } = 1 * 1024 * 1024; // 1 MB

        public string ErrorMessages { get; private set; } = "";
        public ulong  OverallJobSize { get; set; }
        #endregion



        #region ------------- Fields --------------------------------------------------------------
        private ulong  _overallProgress = 0;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        #endregion



        #region ------------- Methods -------------------------------------------------------------
        public List<PhysicalDisk> GetPhysicalDiscs()
        {
            var discs = GetDiscs();

            var physicalDiscs = new List<PhysicalDisk>();
            var scope         = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
            var query         = new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk");
            var searcher      = new ManagementObjectSearcher(scope, query);
            var objects       = searcher.Get();

            foreach(ManagementObject obj in objects)
            {
                var disc = MapToPhysicalDisk(obj);
                physicalDiscs.Add(disc);
            }

            physicalDiscs = physicalDiscs.OrderBy(x => x.DeviceId).ToList();

            foreach (var phys in physicalDiscs)
            {
                if (phys.DeviceId < 0)
                    continue;
                phys.Partitions = GetPartitionsByDeviceId(scope, phys.DeviceId);
                FindDiscAndCopyProperties(discs, phys);
                phys.VolumeName = TakeTheVolumeNameOfTheFirstDriveLetter(phys);
            }

            return physicalDiscs.OrderBy(x => x.DeviceId).ToList();
        }

        public List<Disk> GetDiscs()
        {
            var discs    = new List<Disk>();
            var scope    = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
            var query    = new ObjectQuery("SELECT * FROM MSFT_Disk");
            var searcher = new ManagementObjectSearcher(scope, query);
            var objects  = searcher.Get();

            foreach(ManagementObject obj in objects)
            {
                var disc = MapToDisk(obj);
                discs.Add(disc);
            }

            return discs;
        }

        public List<Partition> GetPartitionsByDeviceId(ManagementScope scope, int deviceId)
        {
            var partitions = new List<Partition>();
            var query = new ObjectQuery($"SELECT * FROM MSFT_Partition WHERE DiskNumber = \"{deviceId}\"");

            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                using (var partitionQuery = searcher.Get())
                {
                    try
                    {
                        foreach (var part in partitionQuery)
                        {
                            var partition = MapPartition(part);
                            partition.Volumes = GetVolumesByPartitionId(scope, partition);

                            partition.Volume = partition.Volumes
                                .Where(v => partition.AccessPaths.Any() && 
                                            partition.AccessPaths[0] == v.UniqueId)
                                .FirstOrDefault();

                            if (partition.Volume is null)
                                partition.Volume = partition.Volumes.Where(v => v.DriveLetter == partition.DriveLetter).FirstOrDefault();
                            if (partition.Volume is null)
                                partition.Volume = new Volume();

                            AddFreeSpaceData(partition);

                            partitions.Add(partition);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }

            return partitions.OrderBy(x => x.Offset).ToList();
        }

        public List<Volume> GetVolumesByPartitionId(ManagementScope scope, Partition partition)
        {
            var volumes = new List<Volume>();
            var query = new ObjectQuery($"SELECT * FROM MSFT_Volume");

            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                using (var volumeQuery = searcher.Get())
                {
                    try
                    {
                        foreach (var vol in volumeQuery)
                        {
                            var volume = MapVolume(vol);

                            var uniqueIdContainsPartitionGuid = partition.Guid != null && 
                                                                volume.UniqueId != null && 
                                                                volume.UniqueId.Contains(partition.Guid);
                            if (uniqueIdContainsPartitionGuid)
                            {
                                volumes.Add(volume);
                            }
                            else 
                            {
                                if (partition.AccessPaths != null && partition.AccessPaths.Any())
                                {
                                    foreach (var path in partition.AccessPaths)
                                    {
                                        if (path.Contains(volume.UniqueId))
                                            volumes.Add(volume);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "PhysicalDiskManager", "GetVolumesByPartitionId");
                    }
                }
            }

            return volumes.OrderBy(x => x.DriveLetter).ToList();
        }

        /// <summary>
        /// Get the DOS name like 
        /// \Device\HarddiskVolume16 
        /// for VolumeIDs like
        /// \\?\Volume{3fc09c92-6fa7-4521-bc9f-c0ef4a600a54}\
        /// </summary>
        public string GetDosDevicenameForVolumeUniqueId(string volumeUniqueId)
        {
            var shortID = volumeUniqueId.Substring(4, volumeUniqueId.Length - 5);
            char[] volumePathNames = new char[260];
            var returnedLength = PhysicalDevice.QueryDosDevice(shortID, volumePathNames, volumePathNames.GetLength(0));
            var volume = new string(volumePathNames, 0, (int)returnedLength).TrimEnd('\0');
            var sourcePath = $@"\\.\GLOBALROOT" + volume;
            return sourcePath;
        }

        /// <summary>
        /// Reads a number of bytes from a physical disk, partition or volume and directly saves it to a file.
        /// </summary>
        /// <param name="sourcePath">GUID or Volume ID</param>
        /// <param name="sourceSize">Size that will only be forwarded to the progressHandler, for percent calculation</param>
        /// <param name="destinationFilename"></param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void ReadFromDiskAndSaveToFile(
            string sourcePath, 
            ulong sourceSize, 
            string destinationFilename, 
            ProgressHandler progressHandler = null,
            CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(destinationFilename))
                throw new ArgumentException($"Invalid destinationFilename: '{destinationFilename}'");

            if (File.Exists(destinationFilename))
                File.Delete(destinationFilename);
            using (File.Create(destinationFilename)) { }

            using (FileStream destStream = new FileStream(destinationFilename, FileMode.Append, FileAccess.Write))
            {
                ReadFromDiskToStream(sourcePath, sourceSize, destStream, progressHandler, token);
            }
        }

        /// <summary>
        /// Reads a number of bytes from a physical disk, partition or volume and sends it into a stream.
        /// </summary>
        /// <param name="sourcePath">GUID or Volume ID</param>
        /// <param name="sourceSize">Size that will only be forwarded to the progressHandler, for percent calculation</param>
        /// <param name="destinationFilename"></param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void ReadFromDiskToStream(
            string sourcePath, 
            ulong sourceSize, 
            Stream destStream, 
            ProgressHandler progressHandler = null,
            CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException($"Invalid sourcePath: '{sourcePath}'");
            if (sourceSize <= 0)
                throw new ArgumentException($"Invalid sourceSize: '{sourceSize}'");
            if (destStream is null)
                throw new ArgumentException($"Invalid destination stream:");
            if (!destStream.CanWrite)
                throw new ArgumentException($"Invalid destination stream: cannot write to stream");

            var _access = FileAccess.Read;
            var attributes = (FileAttributes)(/*File_Attributes.Normal |*/ File_Attributes.BackupSemantics);
            var diskHandle = PlatformShim.CreateDeviceHandle(sourcePath, _access, attributes);
            if (diskHandle.IsInvalid)
                throw new ArgumentException($"Invalid sourcePath: '{sourcePath}'");
            
            var sourceStream = new FileStream(diskHandle, _access);
            if (!sourceStream.CanRead)
                throw new ArgumentException($"Invalid source stream: cannot read from stream");

            CopyFromStreamToStream(sourceStream, destStream, sourceSize, 0, 0, progressHandler, token);
        }

        /// <summary>
        /// Reads a number of bytes from a physical disk, partition or volume and sends it into a stream.
        /// </summary>
        /// <param name="sourcePath">GUID or Volume ID</param>
        /// <param name="sourceSize">Size that will only be forwarded to the progressHandler, for percent calculation</param>
        /// <param name="destinationFilename"></param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void ReadFromDiskToStream(
            string sourcePath, 
            ulong sourceSize, 
            MemoryStream destStream, 
            ProgressHandler progressHandler = null,
            CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException($"Invalid sourcePath: '{sourcePath}'");
            if (sourceSize <= 0)
                throw new ArgumentException($"Invalid sourceSize: '{sourceSize}'");
            if (destStream is null)
                throw new ArgumentException($"Invalid destination stream:");
            if (!destStream.CanWrite)
                throw new ArgumentException($"Invalid destination stream: cannot write to stream");

            var _access = FileAccess.Read;
            var attributes = (FileAttributes)(/*File_Attributes.Normal |*/ File_Attributes.BackupSemantics);
            var diskHandle = PlatformShim.CreateDeviceHandle(sourcePath, _access, attributes);
            if (diskHandle.IsInvalid)
                throw new ArgumentException($"Invalid sourcePath: '{sourcePath}'");
            
            var sourceStream = new FileStream(diskHandle, _access);
            if (!sourceStream.CanRead)
                throw new ArgumentException($"Invalid source stream: cannot read from stream");

            CopyFromStreamToStream(sourceStream, destStream, sourceSize, 0, 0, progressHandler, token);
        }

        /// <summary>
        /// Reads the contents of a file and writes it to a physical disk, partition or volume.
        /// </summary>
        /// <param name="sourceFilename"></param>
        /// <param name="destinationPath">GUID or Volume ID</param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void WriteFromFileToDisk(
            string sourceFilename, 
            string destinationPath, 
            ulong sourceOffset,
            ulong destinationOffset,
            ProgressHandler progressHandler = null,
            CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException($"Invalid destinationPath: '{destinationPath}'");
            if (string.IsNullOrEmpty(sourceFilename))
                throw new ArgumentException($"Invalid sourceFilename: '{sourceFilename}'");
            if (!File.Exists(sourceFilename))
                throw new ArgumentException($"Invalid sourceFilename: file '{sourceFilename}' doesn't exist");

            var sourceFileInfo = new FileInfo(sourceFilename);
            var sourceSize = (ulong)sourceFileInfo.Length;

            using (FileStream sourceStream = new FileStream(sourceFilename, FileMode.Open, FileAccess.Read))
            {
                WriteFromStreamToDisk(sourceStream, sourceSize, destinationPath, sourceOffset, destinationOffset, progressHandler, token);
            }
        }

        /// <summary>
        /// Reads from a stream and writes it to a physical disk, partition or volume.
        /// </summary>
        /// <param name="sourceStream"></param>
        /// <param name="sourceSize">Size that will only be forwarded to the progressHandler, for percent calculation</param>
        /// <param name="destinationPath">GUID or Volume ID</param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void WriteFromStreamToDisk(
            Stream sourceStream, 
            ulong sourceSize, 
            string destinationPath, 
            ulong sourceOffset,
            ulong destinationOffset, 
            ProgressHandler progressHandler = null, 
            CancellationToken token = default)
        {
            //using (var handle = File.OpenHandle(destinationPath, FileMode.Create, FileAccess.Write))
            //{
            //   CopyFromStreamToStream2(sourceStream, handle, sourceSize, sourceOffset, destinationOffset, progressHandler, token);
            //   handle.Close();
            //}


            //var _access = FileAccess.Write;
            //var attributes = (FileAttributes)(/*File_Attributes.Normal |*/ File_Attributes.BackupSemantics);
            //var diskHandle = PlatformShim.CreateDeviceHandle(destinationPath, _access, attributes);
            //if (diskHandle.IsInvalid)
            //    throw new ArgumentException($"Invalid destinationPath: '{destinationPath}'");
            //CopyFromStreamToStream2(sourceStream, diskHandle, sourceSize, sourceOffset, destinationOffset, progressHandler, token);
            //diskHandle.Close();


            var _access = FileAccess.Write;
            var attributes = (FileAttributes)(/*File_Attributes.Normal |*/ File_Attributes.BackupSemantics);
            var diskHandle = PlatformShim.CreateDeviceHandle(destinationPath, _access, attributes);
            if (diskHandle.IsInvalid)
                throw new ArgumentException($"Invalid destinationPath: '{destinationPath}'");
            
            var destStream = new FileStream(diskHandle, _access);
            if (!destStream.CanWrite)
                throw new ArgumentException($"Invalid destination stream: cannot write to stream");
            
            CopyFromStreamToStream(sourceStream, destStream, sourceSize, sourceOffset, destinationOffset, progressHandler, token);
        }

        /// <summary>
        /// Generic method to copy data from a stream to another stream.
        /// </summary>
        /// <param name="sourceStream">A readable stream</param>
        /// <param name="destStream">A writeable stream</param>
        /// <param name="sourceSize">Size that will only be forwarded to the progressHandler, for percent calculation</param>
        /// <param name="progressHandler">Callback method that will be called after a chunk was read. (determined by the BufferSize)</param>
        /// <param name="token">Optional Cancellation token to cancel the processing at any time</param>
        /// <exception cref="ArgumentNullException">Will be thrown for parameter problems</exception>
        /// <exception cref="ArgumentException">Will be thrown for parameter problems</exception>
        /// <exception cref="Exception">Will be thrown for I/O problems.</exception>
        public void CopyFromStreamToStream(
            Stream sourceStream, 
            Stream destStream, 
            ulong sourceSize, 
            ulong sourceOffset,
            ulong destinationOffset,
            ProgressHandler progressHandler = null, 
            CancellationToken token = default)
        {
            if (sourceStream is null)
                throw new ArgumentNullException(nameof(sourceStream));
            if (destStream is null)
                throw new ArgumentNullException(nameof(destStream));
            if (sourceSize <= 0)
                throw new ArgumentException("Source size must be greater than 0");
            if (BufferSize < 512)
                throw new ArgumentException("Buffer size must be greater or equal to 512, (the smallest size of a sector)");

            var progress = new ProgressData() { TotalBytes = sourceSize };

            byte[] buffer = new byte[BufferSize];

            int sourceSizeInt = (sourceSize > int.MaxValue) ? int.MaxValue : (int)sourceSize;

            // it could be that the stream is in total smaller than the buffer
            int numBytesToRead = Math.Min(buffer.GetLength(0), sourceSizeInt);
            int bytesRead;

            if (sourceOffset > 0)
                sourceStream.Seek((long)sourceOffset, SeekOrigin.Begin);

            var destLBA = destinationOffset / 512;

            if (destinationOffset > 0)
                destStream.Seek((long)destinationOffset, SeekOrigin.Begin);

            do
            {
                progress.RemainingBytes = progress.TotalBytes - progress.CompletedBytes;
                if (progress.RemainingBytes <= 0)
                    break;

                if (progress.RemainingBytes < (ulong)numBytesToRead) // adjust the buffer for the last chunk
                    numBytesToRead = (int)progress.RemainingBytes;

                // we might get less bytes than requested, so we need to check that and loop over it until our buffer is full ans has a multiple of a sector size
                int bytesTotal = bytesRead = sourceStream.Read(buffer, 0, numBytesToRead);
                while (bytesTotal < numBytesToRead && bytesTotal > 0 && bytesRead > 0 && !token.IsCancellationRequested)
                {
                    var chunk = numBytesToRead - bytesTotal;
                    bytesRead = sourceStream.Read(buffer, bytesTotal, chunk);
                    bytesTotal += bytesRead;
                }
                bytesRead = bytesTotal;

                if (token.IsCancellationRequested)
                    break;

                destStream.Write(buffer, 0, bytesRead);

                progress.CompletedBytes += (ulong)bytesRead;
                _overallProgress        += (ulong)bytesRead;
                CalculateAndReportProgress(progressHandler, progress);
            }
            while (!token.IsCancellationRequested && bytesRead > 0);
            destStream.Close();

            progress.IsCancellationRequested = token.IsCancellationRequested;
            progress.End = true;
            progress.CompletedBytes = progress.TotalBytes;
            CalculateAndReportProgress(progressHandler, progress);
        }

        //public void CopyFromStreamToStream2(
        //    Stream sourceStream, 
        //    SafeFileHandle destinationHandle, 
        //    ulong sourceSize, 
        //    ulong sourceOffset,
        //    ulong destinationOffset, 
        //    ProgressHandler progressHandler = null, 
        //    CancellationToken token = default)
        //{
        //    if (sourceStream is null)
        //        throw new ArgumentNullException(nameof(sourceStream));
        //    if (destinationHandle is null)
        //        throw new ArgumentNullException(nameof(destinationHandle));
        //    if (sourceSize <= 0)
        //        throw new ArgumentException("Source size must be greater than 0");
        //    if (BufferSize < 512)
        //        throw new ArgumentException("Buffer size must be greater or equal to 512, (the smallest size of a sector)");
        //
        //    var progress = new ProgressData() { TotalBytes = sourceSize };
        //
        //    byte[] buffer = new byte[BufferSize];
        //
        //    int sourceSizeInt = (sourceSize > int.MaxValue) ? int.MaxValue : (int)sourceSize;
        //
        //    // it could be that the stream is in total smaller than the buffer
        //    int numBytesToRead = Math.Min(buffer.GetLength(0), sourceSizeInt);
        //    int bytesRead;
        //
        //    if (sourceOffset > 0)
        //        sourceStream.Seek((long)sourceOffset, SeekOrigin.Begin);
        //
        //    var destLBA = destinationOffset / 512;
        //
        //    do
        //    {
        //        progress.RemainingBytes = progress.TotalBytes - progress.CompletedBytes;
        //        if (progress.RemainingBytes <= 0)
        //            break;
        //
        //        if (progress.RemainingBytes < (ulong)numBytesToRead) // adjust the buffer for the last chunk
        //            numBytesToRead = (int)progress.RemainingBytes;
        //
        //        // we might get less bytes than requested, so we need to check that and loop over it until our buffer is full ans has a multiple of a sector size
        //        int bytesTotal = bytesRead = sourceStream.Read(buffer, 0, numBytesToRead);
        //        while (bytesTotal < numBytesToRead && bytesTotal > 0 && bytesRead > 0 && !token.IsCancellationRequested)
        //        {
        //            var chunk = numBytesToRead - bytesTotal;
        //            bytesRead = sourceStream.Read(buffer, bytesTotal, chunk);
        //            bytesTotal += bytesRead;
        //            //Console.WriteLine($"Expected {chunk,10} bytes, but only got {bytesRead,10} bytes. got in total {bytesTotal,10}");
        //        }
        //        bytesRead = bytesTotal;
        //
        //        if (token.IsCancellationRequested)
        //            break;
        //
        //        Console.WriteLine($"Writing {bytesRead} bytes...");
        //        var buffer2 = new ReadOnlyMemory<byte>(buffer, 0, bytesRead);
        //        RandomAccess.Write(destinationHandle, [buffer2], (long)destinationOffset);
        //        destinationOffset += (ulong)bytesRead;
        //
        //        progress.CompletedBytes += (ulong)bytesRead;
        //        CalculateAndReportProgress(progressHandler, progress);
        //    }
        //    while (!token.IsCancellationRequested && bytesRead > 0);
        //
        //    progress.IsCancellationRequested = token.IsCancellationRequested;
        //    progress.End = true;
        //    CalculateAndReportProgress(progressHandler, progress);
        //}

        public PhysicalDisk GetDiscByDeviceID(List<PhysicalDisk> discs, int deviceId)
        {
            return discs.FirstOrDefault(x => x.DeviceId == deviceId);
        }

        public PhysicalDisk? GetDiscByDriveLetter(List<PhysicalDisk> discs, string driveLetter)
        {
            foreach(var disc in discs)
            {
                foreach (var partition in disc.Partitions)
                {
                    if (partition.DriveLetter == driveLetter)
                        return disc;
                }
            }
            return null;
        }

        public Partition GetPartitionByNumber(PhysicalDisk disc, int partitionNumber)
        {
            return disc.Partitions.FirstOrDefault(x => x.PartitionNumber == partitionNumber);
        }

        public Volume GetVolumeByDriveLetter(PhysicalDisk disc, string driveLetter)
        {
            var volumes = disc.Partitions.SelectMany(p => p.Volumes);
            return volumes.FirstOrDefault(x => x.DriveLetter == driveLetter);
        }

        public PhysicalDisk GetDiscByVolumeName(List<PhysicalDisk> discs, string volumeName)
        {
            return discs.FirstOrDefault(d => d.VolumeName == volumeName);
        }

        public (ulong,string) FindEndOfLastPartition(PhysicalDisk disc, bool sectorBySectorBackup = false)
        {
            if (sectorBySectorBackup)
                return (disc.Size, "Sector by sector backup: all sectors will be read.");

            var sizeToRead = disc.Size;
            var messages = "";
            var lastPartition = disc.Partitions[disc.Partitions.Count - 1];
            var endOfLastPartition = lastPartition.Offset + lastPartition.Size;
            
            if (endOfLastPartition >= disc.Size)
                return (disc.Size, "Reading whole disc");

            return (endOfLastPartition, $"Free space at the end of the disc: {SizeFormatter.Format(disc.Size - endOfLastPartition)} will be skipped.");
        }

        public (byte[], MbrContents) ReadMBR(int deviceID)
        {
            var physicalDevice = new PhysicalDevice();
            var sourcePath = @$"\\.\PHYSICALDRIVE{deviceID}";
            var mbr = physicalDevice.Read(sourcePath, 0, 512);

            var mbrParser = new MbrParser();
            var mbrDecoded = mbrParser.Parse(mbr);

            return (mbr, mbrDecoded);
        }

        public (byte[],byte[],GptContents, GptContents) ReadGPTs(int deviceID, ulong logicalSectorSize, ulong totalDiscSize)
        {
            var physicalDevice = new PhysicalDevice();
            var gptParser = new GptParser(logicalSectorSize);
            var sourcePath = @$"\\.\PHYSICALDRIVE{deviceID}";

            // read the first sector of the GPT to get the total length of the GPT:
            var gpt1HeaderOffset = 1 * logicalSectorSize;
            var gpt1Header = physicalDevice.Read(sourcePath, gpt1HeaderOffset, 1*logicalSectorSize);
            (var pos, var length) = gptParser.ParseArrayPositionAndSize(gpt1Header);
            
            // read the GPT again in full length:
            var gpt1Array = physicalDevice.Read(sourcePath, pos, length);
            var gpt1Decoded = gptParser.Parse(gpt1Header, gpt1Array);

            var gpt1 = new byte[gpt1Decoded.GptHeaderLength + gpt1Decoded.GptArrayLength];
            gpt1Header.CopyTo(gpt1, 0);
            if (gpt1.Length > gpt1Array.Length)
                gpt1Array.CopyTo(gpt1, (int)gpt1Decoded.GptHeaderLength);



            // read the second GPT at the end of the disk (it has the same length)
            // https://en.wikipedia.org/wiki/GUID_Partition_Table#Partition_table_header_(LBA_1)
            
            // "In addition to the primary GPT header and Partition Entry Array, stored at the beginning of the disk,
            // there is a backup GPT header and Partition Entry Array, stored at the end of the disk.
            // The backup GPT header must be at the last block on the disk (LBA -1)
            // and the backup Partition Entry Array is placed between the end of the last partition and the last block.

            var gpt2HeaderOffset = totalDiscSize - 1 * logicalSectorSize;
            var gpt2Header = physicalDevice.Read(sourcePath, gpt2HeaderOffset, gpt1Decoded.GptHeaderLength);

            (var gpt2ArrayOffset, var gpt2Arraylength) = gptParser.ParseArrayPositionAndSize(gpt2Header);
            var gpt2Array  = physicalDevice.Read(sourcePath, gpt2ArrayOffset, gpt2Arraylength);

            var gpt2Decoded = gptParser.Parse(gpt2Header, gpt2Array);

            var gpt2 = new byte[gpt1Decoded.GptHeaderLength + gpt1Decoded.GptArrayLength];
            gpt2Header.CopyTo(gpt2, 0);
            if (gpt2.Length > gpt2Array.Length)
                gpt2Array.CopyTo(gpt2, (int)gpt1Decoded.GptHeaderLength);

            return (gpt1, gpt2, gpt1Decoded, gpt2Decoded);
        }

        public void WritePhysicalSectors(string destinationPath, byte[] data, ulong offset, ulong length)
        {
            var physicalDevice = new PhysicalDevice();
            physicalDevice.Write(destinationPath, data, offset, length);
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        private Disk MapToDisk(ManagementObject obj)
        {
            var disc = new Disk();

            try { disc.Path = obj["Path"]?.ToString(); }
            catch (Exception ex) { disc.Path = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.Location = obj["Location"]?.ToString(); }
            catch (Exception ex) { disc.Location = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.Number = (UInt32)(obj["Number"] ?? 0); }
            catch (Exception ex) { disc.Number = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.SerialNumber = obj["SerialNumber"]?.ToString(); }
            catch (Exception ex) { disc.SerialNumber = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.Manufacturer = obj["Manufacturer"]?.ToString(); }
            catch (Exception ex) { disc.Manufacturer = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.Model = obj["Model"]?.ToString(); }
            catch (Exception ex) { disc.Model = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.LargestFreeExtent = (UInt64)(obj["LargestFreeExtent"] ?? 0); }
            catch (Exception ex) { disc.LargestFreeExtent = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.NumberOfPartitions = (UInt32)(obj["NumberOfPartitions"] ?? 0); }
            catch (Exception ex) { disc.NumberOfPartitions = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.ProvisioningType = (UInt16)(obj["ProvisioningType"] ?? 0); }
            catch (Exception ex) { disc.ProvisioningType = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.PartitionStyle = (UInt16)(obj["PartitionStyle"] ?? 0); }
            catch (Exception ex) { disc.PartitionStyle = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.Signature = (UInt32)(obj["Signature"] ?? 0u); }
            catch (Exception ex) { disc.Signature = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.Guid = obj["Guid"]?.ToString(); }
            catch (Exception ex) { disc.Guid = null; Log(ex, "MapDisk", "Disk"); }

            try { disc.IsOffline = (bool)(obj["IsOffline"] ?? false); }
            catch (Exception ex) { disc.IsOffline = false; Log(ex, "MapDisk", "Disk"); }

            try { disc.OfflineReason = (UInt16)(obj["OfflineReason"] ?? 0); }
            catch (Exception ex) { disc.OfflineReason = 0; Log(ex, "MapDisk", "Disk"); }

            try { disc.IsReadOnly = (bool)(obj["IsReadOnly"] ?? false); }
            catch (Exception ex) { disc.IsReadOnly = false; Log(ex, "MapDisk", "Disk"); }

            try { disc.IsSystem = (bool)(obj["IsSystem"] ?? false); }
            catch (Exception ex) { disc.IsSystem = false; Log(ex, "MapDisk", "Disk"); }

            try { disc.IsClustered = (bool)(obj["IsClustered"] ?? false); }
            catch (Exception ex) { disc.IsClustered = false; Log(ex, "MapDisk", "Disk"); }

            try { disc.IsBoot = (bool)(obj["IsBoot"] ?? false); }
            catch (Exception ex) { disc.IsBoot = false; Log(ex, "MapDisk", "Disk"); }

            try { disc.BootFromDisk = (bool)(obj["BootFromDisk"] ?? false); }
            catch (Exception ex) { disc.BootFromDisk = false; Log(ex, "MapDisk", "Disk"); }

            return disc;
        }

        private PhysicalDisk MapToPhysicalDisk(ManagementObject obj)
        {
            var result = new PhysicalDisk();
            try
            {
                result.SupportedUsages = (ushort[])(obj["SupportedUsages"] ?? 0);
            }
            catch (Exception ex)
            {
                result.SupportedUsages = new ushort[0];
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.CannotPoolReason = (ushort[])(obj["CannotPoolReason"] ?? 0);
            }
            catch (Exception ex)
            {
                result.CannotPoolReason = new ushort[0];
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                var values = (UInt16[])(obj["OperationalStatus"] ?? new UInt16[0]); 
                result.OperationalStatus = ConvertUShortArrayToOperationalStatusArray(values);
            }
            catch (Exception ex)
            {
                result.OperationalStatus = new OperationalStatus[0];
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.OperationalDetails = (string[])obj["OperationalDetails"];
            }
            catch (Exception ex)
            {
                result.OperationalDetails = new string[0];
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.ObjectId = (string)(obj["ObjectId"] ?? "");
            }
            catch (Exception ex)
            {
                result.ObjectId = "";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.UniqueId = (string)(obj["UniqueId"] ?? "");
            }
            catch (Exception ex)
            {
                result.UniqueId = "";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.UniqueIdFormat = (ushort)(obj["UniqueIdFormat"] ?? 0);
            }
            catch (Exception ex)
            {
                result.UniqueIdFormat = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                var temp = (string)obj["DeviceId"];
                result.DeviceId = Convert.ToInt32(temp);
            }
            catch (Exception ex)
            {
                result.DeviceId = -1;
                Log(ex, "MapToPhysicalDisk", "DeviceId");
            }
            try
            {
                result.FriendlyName = (string)(obj["FriendlyName"] ?? 0);
            }
            catch (Exception ex)
            {
                result.FriendlyName = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.HealthStatus = (ushort)(obj["HealthStatus"] ?? 0);
            }
            catch (Exception ex)
            {
                result.HealthStatus = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.PhysicalLocation = (string)(obj["PhysicalLocation"] ?? 0);
            }
            catch (Exception ex)
            {
                result.PhysicalLocation = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.VirtualDiskFootprint = (ulong)(obj["VirtualDiskFootprint"] ?? 0);
            }
            catch (Exception ex)
            {
                result.VirtualDiskFootprint = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.Usage = (ushort)(obj["Usage"] ?? 0);
            }
            catch (Exception ex)
            {
                result.Usage = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.Description = (string)(obj["Description"] ?? "");
            }
            catch (Exception ex)
            {
                result.Description = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.PartNumber = (string)(obj["PartNumber"] ?? "");
            }
            catch (Exception ex)
            {
                result.PartNumber = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.FirmwareVersion = (string)(obj["FirmwareVersion"] ?? "");
            }
            catch (Exception ex)
            {
                result.FirmwareVersion = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.SoftwareVersion = (string)(obj["SoftwareVersion"] ?? "");
            }
            catch (Exception ex)
            {
                result.SoftwareVersion = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.Size = (UInt64)(obj["Size"] ?? 0);
            }
            catch (Exception ex)
            {
                result.Size = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.AllocatedSize = (ulong)(obj["AllocatedSize"] ?? 0);
            }
            catch (Exception ex)
            {
                result.AllocatedSize = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                var temp = (ushort)(obj["BusType"] ?? 0);
                result.BusType = (BusType)temp;
            }
            catch (Exception ex)
            {
                result.BusType = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.PhysicalSectorSize = (ulong)(obj["PhysicalSectorSize"] ?? 0);
            }
            catch (Exception ex)
            {
                result.PhysicalSectorSize = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.LogicalSectorSize = (ulong)(obj["LogicalSectorSize"] ?? 0);
            }
            catch (Exception ex)
            {
                result.LogicalSectorSize = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.SpindleSpeed = (uint)(obj["SpindleSpeed"] ?? 0);
            }
            catch (Exception ex)
            {
                result.SpindleSpeed = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.IsIndicationEnabled = (bool)(obj["IsIndicationEnabled"] ?? 0);
            }
            catch (Exception ex)
            {
                result.IsIndicationEnabled = false;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.EnclosureNumber = (Int32)(obj["EnclosureNumber"] ?? 0);
            }
            catch (Exception ex)
            {
                result.EnclosureNumber = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.SlotNumber = (Int32)(obj["SlotNumber"] ?? 0);
            }
            catch (Exception ex)
            {
                result.SlotNumber = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.CanPool = (bool)(obj["CanPool"] ?? 0);
            }
            catch (Exception ex)
            {
                result.CanPool = false;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.OtherCannotPoolReasonDescription = (string)obj["OtherCannotPoolReasonDescription"];
            }
            catch (Exception ex)
            {
                result.OtherCannotPoolReasonDescription = "?";
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.IsPartial = (bool)(obj["IsPartial"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsPartial = false;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }
            try
            {
                result.MediaType = (MediaType)(ushort)(obj["MediaType"] ?? 0);
            }
            catch (Exception ex)
            {
                result.MediaType = 0;
                Log(ex, "MapToPhysicalDisk", "PhysicalDisk");
            }

            return result;
        }

        private Partition MapPartition(ManagementBaseObject obj)
        {
            var result = new Partition();

            try
            {
                result.Guid = obj["Guid"]?.ToString().TrimEnd('\0');
            }
            catch (Exception ex)
            {
                result.Guid = null;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.Size = (ulong)(obj["Size"] ?? 0);
            }
            catch (Exception ex)
            {
                result.Size = 0;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.Offset = (ulong)(obj["Offset"] ?? 0);
            }
            catch (Exception ex)
            {
                result.Offset = 0;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.DriveLetter = obj["DriveLetter"]?.ToString().TrimEnd('\0');
            }
            catch (Exception ex)
            {
                result.DriveLetter = null;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.IsReadOnly = (bool)(obj["IsReadOnly"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsReadOnly = false;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.IsOffline = (bool)(obj["IsOffline"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsOffline = false;
                Log(ex, "MapPartition", "Partition");
            }

            try
            {
                result.DiskNumber = (UInt32)(obj["DiskNumber"] ?? 0);
            }
            catch (Exception ex)
            {
                result.DiskNumber = 0;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.PartitionNumber = (UInt32)(obj["PartitionNumber"] ?? 0);
            }
            catch (Exception ex)
            {
                result.PartitionNumber = 0;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.AccessPaths = (string[])(obj["AccessPaths"] ?? new string[0]);
            }
            catch (Exception ex)
            {
                result.AccessPaths = new string[0];
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                var value = obj["OperationalStatus"]; 
                result.OperationalStatus = (OperationalStatus)(ushort)value;
            }
            catch (Exception ex)
            {
                result.OperationalStatus = OperationalStatus.Unknown;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.TransitionState = (UInt16)(obj["TransitionState"] ?? 0);
            }
            catch (Exception ex)
            {
                result.TransitionState = 0;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                var value = obj["MbrType"];
                result.MbrType = (value != null) ? (ushort)value : (ushort)0;
            }
            catch (Exception ex)
            {
                result.MbrType = 0;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.GptType = obj["GptType"]?.ToString().TrimEnd('\0');
            }
            catch (Exception ex)
            {
                result.GptType = null;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.IsSystem = (bool)(obj["IsSystem"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsSystem = false;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.IsBoot = (bool)(obj["IsBoot"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsBoot = false;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.IsActive = (bool)(obj["IsActive"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsActive = false;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.IsHidden = (bool)(obj["IsHidden"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsHidden = false;
                Log(ex, "MapPartition", "Partition");
            }
            try
            {
                result.IsShadowCopy = (bool)(obj["IsShadowCopy"] ?? false);
            }
            catch (Exception ex)
            {
                result.IsShadowCopy = false;
                Log(ex, "MapPartition", "IsShadowCopy");
            }
            try
            {
                result.NoDefaultDriveLetter = (bool)(obj["NoDefaultDriveLetter"] ?? false);
            }
            catch (Exception ex)
            {
                result.NoDefaultDriveLetter = false;
                Log(ex, "MapPartition", "NoDefaultDriveLetter");
            }

            result.GptTypeDesc = GptType.GetTypeDescByGuid(result.GptType ?? GptType.Unknown);
            result.MbrTypeDesc = MbrType.GetTypeDescByID(result.MbrType);
            return result;
        }

        private Volume MapVolume(ManagementBaseObject obj)
        {
            var result = new Volume();

            try
            {
                result.ObjectId = obj["ObjectId"]?.ToString();
            }
            catch (Exception ex)
            {
                result.ObjectId = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                var values = (UInt16[])(obj["OperationalStatus"] ?? new UInt16[0]); 
                result.OperationalStatus = ConvertUShortArrayToOperationalStatusArray(values);
            }
            catch (Exception ex)
            {
                result.OperationalStatus = new OperationalStatus[0];
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.HealthStatus = (ushort)(obj["HealthStatus"] ?? 0);
            }
            catch (Exception ex)
            {
                result.HealthStatus = 0;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.DriveType = obj["DriveType"]?.ToString();
            }
            catch (Exception ex)
            {
                result.DriveType = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.FileSystemType = obj["FileSystemType"]?.ToString();
            }
            catch (Exception ex)
            {
                result.FileSystemType = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.DedupMode = obj["DedupMode"]?.ToString();
            }
            catch (Exception ex)
            {
                result.DedupMode = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.ReFSDedupMode = obj["ReFSDedupMode"]?.ToString();
            }
            catch (Exception ex)
            {
                result.ReFSDedupMode = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.PassThroughClass = obj["PassThroughClass"]?.ToString();
            }
            catch (Exception ex)
            {
                result.PassThroughClass = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.PassThroughIds = (string[])(obj["PassThroughIds"] ?? Array.Empty<string>());
            }
            catch (Exception ex)
            {
                result.PassThroughIds = new string[0];
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.PassThroughNamespace = obj["PassThroughNamespace"]?.ToString();
            }
            catch (Exception ex)
            {
                result.PassThroughNamespace = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.PassThroughServer = obj["PassThroughServer"]?.ToString();
            }
            catch (Exception ex)
            {
                result.PassThroughServer = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.UniqueId = obj["UniqueId"]?.ToString();
            }
            catch (Exception ex)
            {
                result.UniqueId = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.AllocationUnitSize = (UInt32)(obj["AllocationUnitSize"] ?? 0);
            }
            catch (Exception ex)
            {
                result.AllocationUnitSize = 0;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.DriveLetter = obj["DriveLetter"]?.ToString();
            }
            catch (Exception ex)
            {
                result.DriveLetter = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.FileSystem = obj["FileSystem"]?.ToString();
            }
            catch (Exception ex)
            {
                result.FileSystem = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.FileSystemLabel = obj["FileSystemLabel"]?.ToString();
            }
            catch (Exception ex)
            {
                result.FileSystemLabel = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.Path = obj["Path"]?.ToString();
            }
            catch (Exception ex)
            {
                result.Path = null;
                Log(ex, "MapVolume", "Volume");
            }

            try
            {
                result.SizeRemaining = (ulong)(obj["SizeRemaining"] ?? 0);
            }
            catch (Exception ex)
            {
                result.SizeRemaining = 0;
                Log(ex, "MapVolume", "Volume");
            }

            return result;
        }

        private void Log(Exception ex, string v1, string v2)
        {
            if (ex is System.Management.ManagementException ex2)
            {
                if (ex2.ErrorCode == ManagementStatus.NotFound)
                    return;
            }
            ErrorMessages += $"Error: {ex.Message} {v1} {v2}\n";
        }

        private OperationalStatus[] ConvertUShortArrayToOperationalStatusArray(ushort[] values)
        {
            var results = new OperationalStatus[values.GetLength(0)];
    
            for(int i=0; i < values.GetLength(0); i++)
                results[i] = (OperationalStatus)values[i];

            return results;
        }

        private string TakeTheVolumeNameOfTheFirstDriveLetter(PhysicalDisk phys)
        {
            foreach (var partition in phys.Partitions.Where(p => p.DriveLetter != "").OrderBy(p => p.DriveLetter))
            {
                var volumeName = GetVolumeNameByDriveLetter(partition.DriveLetter + ":");
                if (!string.IsNullOrEmpty(volumeName))
                    return volumeName;
            }
            return "";
        }

        private string GetVolumeNameByDriveLetter(string driveLetter)
        {
            char[] lpVolumeNameBuffer = new char[256];
            uint lpVolumeSerialNumber;
            uint lpMaximumComponentLength;
            uint lpFileSystemFlags;
            char[] lpFileSystemNameBuffer = new char[256];
            int nFileSystemNameSize;

            PhysicalDevice.GetVolumeInformationW(driveLetter,
                lpVolumeNameBuffer,
                lpVolumeNameBuffer.GetLength(0),
                out lpVolumeSerialNumber,
                out lpMaximumComponentLength,
                out lpFileSystemFlags,
                lpFileSystemNameBuffer,
                lpFileSystemNameBuffer.GetLength(0));

            var volumeName = new string(lpVolumeNameBuffer).TrimEnd('\0');
            return volumeName;
        }

        private void FindDiscAndCopyProperties(List<Disk> discs, PhysicalDisk phys)
        {
            var disc = discs.FirstOrDefault(x => x.Number == phys.DeviceId);
            if (disc is null)
                return;

            phys.Path               = disc.Path;
            phys.Location           = disc.Location;
            phys.Number             = disc.Number;
            phys.SerialNumber       = disc.SerialNumber;
            phys.Manufacturer       = disc.Manufacturer;
            phys.Model              = disc.Model;
            phys.LargestFreeExtent  = disc.LargestFreeExtent;
            phys.NumberOfPartitions = disc.NumberOfPartitions;
            phys.ProvisioningType   = disc.ProvisioningType;        
            phys.PartitionStyle     = disc.PartitionStyle;
            phys.Signature          = disc.Signature;
            phys.Guid               = disc.Guid;
            phys.IsOffline          = disc.IsOffline;
            phys.OfflineReason      = disc.OfflineReason;
            phys.IsReadOnly         = disc.IsReadOnly;
            phys.IsSystem           = disc.IsSystem;
            phys.IsClustered        = disc.IsClustered;
            phys.IsBoot             = disc.IsBoot;
            phys.BootFromDisk       = disc.BootFromDisk;
        }

        private void AddFreeSpaceData(Partition partition)
        {
            var volID = (!string.IsNullOrEmpty(partition.Volume.DriveLetter))
                ? partition.Volume.DriveLetter + ":"
                : partition.Volume.UniqueId;

            if (volID == null)
                return;

            uint SectorsPerCluster, BytesPerSector, NumberOfFreeClusters, TotalNumberOfClusters;

            PhysicalDevice.GetDiskFreeSpace(volID, 
                out SectorsPerCluster, 
                out BytesPerSector, 
                out NumberOfFreeClusters, 
                out TotalNumberOfClusters);

            partition.Volume.SectorsPerCluster     = SectorsPerCluster;
            partition.Volume.BytesPerSector        = BytesPerSector;
            partition.Volume.NumberOfFreeClusters  = NumberOfFreeClusters;
            partition.Volume.TotalNumberOfClusters = TotalNumberOfClusters;
        }

        private void CalculateAndReportProgress(ProgressHandler progressHandler, ProgressData progress)
        {
            if (progressHandler is null)
                return;
                    
            if (progress.TotalBytes == 0)
            {
                progress.Percentage      = 0;
                progress.TotalPercentage = 0;
            }
            else
            {
                progress.Percentage      = (double)(progress.CompletedBytes * 100) / progress.TotalBytes;
                progress.TotalPercentage = (double)(_overallProgress        * 100) / OverallJobSize;
            }

            progressHandler(progress);
        }
        #endregion
    }
}
