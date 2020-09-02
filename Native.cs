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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using PipeExplorer.Models;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.AdvApi32;
using Vanara.PInvoke;
using System.Text;
using System.IO.Pipes;
using System.Security.AccessControl;

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
        [DllImport("ntdll.dll")]
        internal static extern UInt32 NtQueryDirectoryFile(
            //SafeFileHandle FileHandle,
            IntPtr FileHandle,
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
        const Int32 BufferLength = 0x00100000;
        const int READ_CONTROL = 0x00020000;

        private static bool IsNtSuccess(uint code) => code <= 0x7FFFFFFF;

        private static System.ComponentModel.Win32Exception ExceptionFromLastError()
        {
            return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        private static T PtrToStruct<T>(IntPtr p)
        {
            return (T)Marshal.PtrToStructure(p, typeof(T));
        }

        public static IEnumerable<PipeModel> GetPipes(string pipeHost = ".")
        {
            IntPtr dir, tmp;
            bool isFirstQuery = true;

            var pipesPath = $@"\\{pipeHost}\Pipe\";
            var pipes = CreateFile(pipesPath, Kernel32.FileAccess.GENERIC_READ, FileShare.Read | FileShare.Write | FileShare.Delete, null, FileMode.Open, 0, HFILE.NULL);
            if (pipes.IsInvalid)
                throw new IOException("could open named pipes list", ExceptionFromLastError());

            dir = Marshal.AllocHGlobal(BufferLength);
            try
            {
                while (true)
                {
                    var code = NativeMethods.NtQueryDirectoryFile(
                        pipes.DangerousGetHandle(), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var isb, dir,
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
                        // fdi.FileNameLength/2 - because FileNameLength is in bytes
                        var name = Marshal.PtrToStringUni(namePtr, (int)fdi.FileNameLength / 2);

                        AclModel acl = null;
                        var err = GetNamedSecurityInfo(pipesPath + name, SE_OBJECT_TYPE.SE_FILE_OBJECT,
                            SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION | SECURITY_INFORMATION.GROUP_SECURITY_INFORMATION | SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                            out var ownerSid, out var groupSid, out var dacl, out _, out var sd);
                        if (err.Succeeded)
                        {
                            try
                            {
                                int ownerNameBufLen = 1024, groupNameBufLen = 1024, domainBufLen = 1024;
                                StringBuilder ownerNameBuf = new StringBuilder(ownerNameBufLen);
                                StringBuilder groupNameBuf = new StringBuilder(groupNameBufLen);
                                StringBuilder domainBuf = new StringBuilder(domainBufLen);
                                LookupAccountSid(null, ownerSid, ownerNameBuf, ref ownerNameBufLen, domainBuf, ref domainBufLen, out var ownerAccType);
                                LookupAccountSid(null, groupSid, groupNameBuf, ref groupNameBufLen, null, ref domainBufLen, out var groupAccType);

                                List<AclRuleModel> rules = null;
                                if (dacl.IsValidAcl())
                                {
                                    var cnt = dacl.AceCount();
                                    rules = new List<AclRuleModel>((int)cnt);
                                    for (uint i = 0; i < cnt; i++)
                                    {
                                        if (GetAce(dacl, i, out var ace))
                                        {
                                            var sid = ace.GetSid();
                                            int sidNameLen = 1024, sidDomainLen = 1024;
                                            StringBuilder sidNameBuf = new StringBuilder(sidNameLen);
                                            StringBuilder sidDomainBuf = new StringBuilder(sidDomainLen);
                                            LookupAccountSid(null, sid, sidNameBuf, ref sidNameLen, sidDomainBuf, ref sidDomainLen, out var sidAccType);

                                            bool isAllowing;
                                            switch (ace.GetHeader().AceType)
                                            {
                                                case AceType.AccessAllowed:
                                                    isAllowing = true;
                                                    break;
                                                case AceType.AccessDenied:
                                                    isAllowing = false;
                                                    break;
                                                default:
                                                    continue;
                                            }

                                            var mask = ace.GetMask();
                                            // make Enum formatter happy, since there are no flags for 0x60 bits
                                            mask &= 0xFFFFFF9F;

                                            if (sidNameBuf.Length > 0)
                                                rules.Add(new AclRuleModel(sidNameBuf.ToString(), isAllowing, (PipeAccessRights)mask));
                                        }
                                    }
                                }

                                acl = new AclModel(ownerNameBuf.ToString(), groupNameBuf.ToString(), rules);
                            }
                            finally
                            {
                                sd.Dispose();
                            }
                        }

                        yield return new PipeModel(pipeHost, name, (int)fdi.AllocationSize.LowPart, fdi.EndOfFile.LowPart, acl);

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
