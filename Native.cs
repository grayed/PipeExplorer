#region Licensing information
/*
 * Copyright(c) 2020 Vadim Zhukov<zhuk@openbsd.org>
 * 
 * Permission to use, copy, modify, and distribute this software for any
 * purpose with or without fee is hereby granted, provided that the above
 * copyright notice and this permission notice appear in all copies.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 * WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS.IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 * ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 * WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 * ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 * OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PipeExplorer.Models;

namespace PipeExplorer
{
    //
    // Mostly from https://www.pvsm.ru/c-2/145816
    //

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct LARGE_INTEGER
    {
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        [FieldOffset(0)] internal Int64 QuadPart;
        [FieldOffset(0)] internal UInt32 LowPart;
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        [FieldOffset(4)] internal Int32 HighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FILE_DIRECTORY_INFORMATION
    {
        internal UInt32 NextEntryOffset;
        internal UInt32 FileIndex;
        internal LARGE_INTEGER CreationTime;
        internal LARGE_INTEGER LastAccessTime;
        internal LARGE_INTEGER LastWriteTime;
        internal LARGE_INTEGER ChangeTime;
        internal LARGE_INTEGER EndOfFile;
        internal LARGE_INTEGER AllocationSize;
        internal UInt32 FileAttributes;
        internal UInt32 FileNameLength;
        internal UInt16 FileName;

        internal static Int32 FileNameOffset { get; } = Marshal.OffsetOf(typeof(FILE_DIRECTORY_INFORMATION), "FileName").ToInt32();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_STATUS_BLOCK
    {
        internal UIntPtr Status;
        internal UIntPtr Information;       // ULONG_PTR
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            String lpFileName,
            UInt32 dwDesiredAccess,
            System.IO.FileShare dwShareMode,
            IntPtr lpSecuriryAttributes,
            FileMode dwCreationDisposition,
            UInt32 dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("ntdll.dll")]
        internal static extern UInt32 NtQueryDirectoryFile(
            SafeFileHandle FileHandle,
            IntPtr Event,
            IntPtr ApcRoutine,
            IntPtr ApcContext,
            // out IO_STATUS_BLOCK IoStatusBlock,
            out IO_STATUS_BLOCK IoStatusBlock,
            [Out] IntPtr FileInformation,
            UInt32 Length,
            UInt32 FileInformationClass,
            [MarshalAs(UnmanagedType.Bool)] Boolean ReturnSingleEntry,
            IntPtr FileName,
            [MarshalAs(UnmanagedType.Bool)] Boolean RestartScan
        );
    }

    static class Native
    {
        const UInt32 FileDirectoryInformation = 1;
        const UInt32 GENERIC_READ = 0x80000000;
        const Int32 BufferLength = 0x00100000;

        private static bool IsNtSuccess(uint code) => code <= 0x7FFFFFFF;

        private static Win32Exception ExceptionFromLastError()
        {
            return new Win32Exception(Marshal.GetLastWin32Error());
        }

        private static T PtrToStruct<T>(IntPtr p)
        {
            return (T)Marshal.PtrToStructure(p, typeof(T));
        }

        public static IEnumerable<PipeModel> GetPipes(string pipeHost = ".")
        {
            IntPtr dir, tmp;
            bool isFirstQuery = true;

            var pipes = NativeMethods.CreateFile($@"\\{pipeHost}\Pipe\", GENERIC_READ, FileShare.Read, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            if (pipes.IsInvalid)
                throw new IOException("could open named pipes list", ExceptionFromLastError());

            dir = Marshal.AllocHGlobal(BufferLength);
            try
            {
                while (true)
                {
                    var code = NativeMethods.NtQueryDirectoryFile(
                        pipes, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var isb, dir,
                        BufferLength, FileDirectoryInformation, false, IntPtr.Zero, isFirstQuery);
                    if (!IsNtSuccess(code))
                    {
                        break;
                    }

                    tmp = dir;
                    while (true)
                    {
                        FILE_DIRECTORY_INFORMATION fdi = PtrToStruct<FILE_DIRECTORY_INFORMATION>(tmp);
                        IntPtr namePtr = (IntPtr)(FILE_DIRECTORY_INFORMATION.FileNameOffset + tmp.ToInt64());

                        yield return new PipeModel(pipeHost, Marshal.PtrToStringUni(namePtr), (int)fdi.AllocationSize.LowPart, fdi.EndOfFile.LowPart, null /* TODO */);

                        if (fdi.NextEntryOffset == 0)
                            break;
                        tmp = (IntPtr)(tmp.ToInt64() + fdi.NextEntryOffset);
                    }

                    isFirstQuery = false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(dir);
                pipes.Dispose();
            }
        }
    }
}
