using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace RawDiskManager
{
    public static class PlatformShim
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        public static SafeFileHandle CreateDeviceHandle(string path, FileAccess access, FileAttributes flagsAndAttributes = FileAttributes.Normal)
        {
            SafeFileHandle handle = CreateFile(path, access, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, flagsAndAttributes, IntPtr.Zero);

            int win32Error = Marshal.GetLastWin32Error();
            if (win32Error != 0)
                throw new Win32Exception(win32Error);

            return handle;
        }
    }
}
