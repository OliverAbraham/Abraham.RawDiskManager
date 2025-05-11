using Microsoft.Win32.SafeHandles;
using System;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Threading;

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
namespace RawDiskManager
{
    /// <summary>
    /// Reads and writes physical sectors.
    /// </summary>
    public class PhysicalDevice : IDisposable
    {
        #region ------------- Types and constants -------------------------------------------------

        public const uint FSCTL_LOCK_VOLUME      = 0x00090018;
        public const uint FSCTL_UNLOCK_VOLUME    = 0x0009001c;
        public const uint FSCTL_DISMOUNT_VOLUME  = 0x00090020;

        public const short FILE_ATTRIBUTE_NORMAL = 0x80;
        public const short INVALID_HANDLE_VALUE  = -1;
        public const uint  GENERIC_READ          = 0x80000000;
        public const uint  GENERIC_WRITE         = 0x40000000;
        public const uint  CREATE_NEW            = 1;
        public const uint  CREATE_ALWAYS         = 2;
        public const uint  OPEN_EXISTING         = 3;

        public const uint  FILE_BEGIN   = 0; // Der Startpunkt ist 0 oder der Anfang der Datei. Wenn dieses Flag angegeben wird, wird der parameter liDistanceToMove als wert ohne Vorzeichen interpretiert.
        public const uint  FILE_CURRENT = 1; // Der Startpunkt ist der aktuelle Wert des Dateizeigers. Wenn dieses Flag angegeben wird, wird der parameter liDistanceToMove als wert mit Vorzeichen interpretiert.
        public const uint  FILE_END     = 2; //Der Startpunkt ist die aktuelle Position am Ende der Datei. 
        #endregion



        #region ------------- Interop (P/Invoke) functions from kernel32.dll ----------------------
        // Use interop to call the CreateFile function.
        // For more information about CreateFile,
        // see the unmanaged MSDN reference library.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
          uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
          uint dwFlagsAndAttributes, IntPtr hTemplateFile);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetFilePointer(
            IntPtr hFile, 
            int lDistanceToMove, 
            ref int lpNewFilePointer, 
            uint dwMoveMethod);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFilePointerEx(
            IntPtr hFile,
            long liDistanceToMove,
            out long lpNewFilePointer,
            uint dwMoveMethod);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToRead,
            ref int lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            IntPtr hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToWrite,
            out int lpNumberOfBytesWritten,
            IntPtr lpOverlapped);


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFileEx(
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            ref NativeOverlapped lpOverlapped,
            FileIOCompletionRoutine lpCompletionRoutine);

        private delegate void FileIOCompletionRoutine(uint dwErrorCode, uint dwNumberOfBytesTransfered, ref NativeOverlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetDiskFreeSpace(string lpRootPathName,
            out uint lpSectorsPerCluster,
            out uint lpBytesPerSector,
            out uint lpNumberOfFreeClusters,
            out uint lpTotalNumberOfClusters);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint QueryDosDevice(string lpDeviceName, char[] lpTargetPath, int ucchMax);


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool GetVolumeInformationW(
            string lpRootPathName,
            char[] lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            char[] lpFileSystemNameBuffer,
            int nFileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool DeviceIoControl(
          SafeFileHandle hDevice,               // handle to a volume
          uint dwIoControlCode,         // FSCTL_LOCK_VOLUME,   // dwIoControlCode
          IntPtr lpInBuffer,            // NULL,                        // lpInBuffer
          uint nInBufferSize,           // 0,                           // nInBufferSize
          IntPtr lpOutBuffer,           // NULL,                        // lpOutBuffer
          uint nOutBufferSize,          // 0,                           // nOutBufferSize
          IntPtr lpBytesReturned,       // number of bytes returned
          IntPtr lpOverlapped);         // OVERLAPPED structure
        #endregion



        #region ------------- Fields --------------------------------------------------------------
        private bool disposed = false;
        private SafeFileHandle? _safeHandle = null;
        //private FileStream _fs = null;
        private IntPtr _filePointer;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public PhysicalDevice()
        {
        }
        #endregion



        #region ------------- Methods -------------------------------------------------------------
        public void LockDevice()
        {
            uint bytesReturned = 0;

            var result = DeviceIoControl(
                _safeHandle,                // handle to a volume
                FSCTL_LOCK_VOLUME,          // dwIoControlCode
                0,                          // lpInBuffer
                0,                          // nInBufferSize
                0,                          // lpOutBuffer
                0,                          // nOutBufferSize
                (nint)bytesReturned,        // number of bytes returned
                IntPtr.Zero);               // OVERLAPPED structure;
        }

        public void UnlockDevice()
        {
            uint bytesReturned = 0;

            var result = DeviceIoControl(
                _safeHandle,                // handle to a volume
                FSCTL_UNLOCK_VOLUME,        // dwIoControlCode
                0,                          // lpInBuffer
                0,                          // nInBufferSize
                0,                          // lpOutBuffer
                0,                          // nOutBufferSize
                (nint)bytesReturned,        // number of bytes returned
                IntPtr.Zero);               // OVERLAPPED structure;
        }

        public byte[] Read(string device, ulong offset, ulong length)
        {
            OpenFileHandles(device);
            var buffer = new byte[length];
            ReadRawFromDevice(buffer, offset, length);
            CloseFileHandles();
            return buffer;
        }

        public void Write(string device, byte[] sourceData, ulong offset, ulong length)
        {
            OpenFileHandles(device, GENERIC_WRITE);
            var buffer = new byte[length];
            WriteRawToDevice(sourceData, offset, length);
            CloseFileHandles();
        }

        public void OpenFileHandles(string Path, uint desiredAccess = GENERIC_READ, uint creationDisposition = OPEN_EXISTING)
        {
            if (string.IsNullOrEmpty(Path))
                throw new ArgumentNullException("Path");

            _filePointer = CreateFile(Path, desiredAccess, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            _safeHandle = new SafeFileHandle(_filePointer, true);
            //_fs = new FileStream(_safeHandle, FileAccess.Read);

            // If the handle is invalid, get the last Win32 error and throw a Win32Exception.
            if (_safeHandle.IsInvalid)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }

        public void CloseFileHandles()
        {
            _safeHandle?.Close();
        }

        /// <summary>
        /// Reads a byte block from a physical disk.
        /// </summary>
        /// <param name="destBuffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and 
        /// (offset + count - 1) replaced by the bytes read from the current source. </param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. </param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns></returns>
        public void ReadRawFromDevice(byte[] destBuffer, ulong offset, ulong count, uint seekDirection = FILE_BEGIN)
        {
            if (offset > long.MaxValue)
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range for a long integer");

            var handle = _safeHandle?.DangerousGetHandle();
            if (handle == IntPtr.Zero)
                throw new ObjectDisposedException("Handle is closed");
            if (handle == null)
                throw new ObjectDisposedException("Handle is closed");

            long lpNewFilePointer = 0;
            if (offset > 0)
                SetFilePointerEx((IntPtr)handle, (long)offset, out lpNewFilePointer, seekDirection);

            int bytesRead =0;
            if (!ReadFile((IntPtr)handle, destBuffer, (int)count, ref bytesRead, IntPtr.Zero))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            if (count != (ulong)bytesRead)
                throw new Exception($"Read {bytesRead} bytes, but expected {count}");
        }

        public void WriteRawToDevice(byte[] sourceData, ulong offset, ulong count, uint seekDirection = FILE_BEGIN)
        {
            if (offset > long.MaxValue)
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range for a long integer");

            var handle = _safeHandle?.DangerousGetHandle();
            if (handle == IntPtr.Zero)
                throw new ObjectDisposedException("Handle is closed");
            if (handle == null)
                throw new ObjectDisposedException("Handle is closed");

            long lpNewFilePointer = 0;
            if (offset > 0)
                SetFilePointerEx((IntPtr)handle, (long)offset, out lpNewFilePointer, seekDirection);

            int bytesWritten =0;
            if (!WriteFile((IntPtr)handle, sourceData, (int)count, out bytesWritten, IntPtr.Zero))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            if (count != (ulong)bytesWritten)
                throw new Exception($"Wrote {bytesWritten} bytes, but expected {count}");
        }

        public void Close()
        {
            _safeHandle?.Close();
            _safeHandle?.Dispose();
            _safeHandle = null;
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (_safeHandle != null)
                    {
                        //_fs.Dispose();
                        _safeHandle.Close();
                        _safeHandle.Dispose();
                        _safeHandle = null;
                    }
                }
                // Note disposing has been done.
                disposed = true;

            }
        }
        #endregion
    }
}
