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

namespace PipeExplorer.Models
{
    readonly struct PipeModel : IEquatable<PipeModel>
    {
        public string Host { get; }
        public string Name { get; }
        public string Hint { get; }
        public int MaxConnections { get; }
        public AclModel Acl { get; }
        public uint ActiveConnections { get; }

        public string Path => $@"\\{Host}\{Name}";

        public PipeModel(string host, string name, int maxConn, uint activeConn, AclModel acl)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MaxConnections = maxConn;
            ActiveConnections = activeConn;
            Hint = GetHintFor(name);
            Acl = acl ?? new AclModel(null, null, null);
        }

        public static string GetHintFor(string pipeName)
        {
            switch (pipeName)
            {
                // Source: https://l.wzm.me/_security/internet/_internet/WinServices/ch04s05s03.html

                case "atsvc": return "Scheduler service";
                case "AudioSrv": return "Windows Audio service";
                case "browser": return "Computer Browser (ntsvcs alias)";
                case "cert": return "Certificate services";
                case "Ctx_Winstation_API_Service": return "Terminal Services remote management";
                case "DAV RPC SERVICE": return "WebDAV client";
                case "dnsserver": return "DNS Server";
                case "epmapper": return "RPC endpoint mapper";
                case "eventlog": return "Eventlog service (ntsvcs alias)";
                case "HydraLsPipe": return "Terminal Server Licensing";
                case "InitShutdown": return "(Remote) system shutdown";
                case "keysvc": return "Cryptographic services";
                case "locator": return "RPC Locator service";
                case "llsrpc": return "Licensing Logging service";
                case "lsarpc": return "LSA access (lsass alias)";
                case "msgsvc": return "Messenger service (ntsvcs alias)";
                case "netdfs": return "Distributed File System service";
                case "netlogon": return "Net Logon service (lsass alias)";
                case "ntsvcs": return "Plug and Play service";
                case "policyagent": return "IPSEC Policy Agent (Windows 2000)";
                case "ipsec": return "IPsec Services";
                case "ProfMapApi": return "Userenv";
                case "protected_storage": return "Protected Storage";
                case "ROUTER": return "Remote Access";
                case "samr": return "SAM access (lsass alias)";
                case "scerpc": return "Security Configuration Editor (SCE)";
                case "SECLOGON": return "Secondary logon service";
                case "SfcApi": return "Windows File Protection";
                case "spoolss": return "Spooler service";
                case "srvsvc": return "Server service (ntsvcs alias)";
                case "ssdpsrv": return "SSDP service";
                case "svcctl": return "Services control manager (ntsvcs alias)";
                case "tapsrv": return "Telephony service";
                case "trkwks": return "Distributed Link Tracking Client";
                case "W32TIME (ntsvcs alias)": return "Windows Time (Windows 2000 and XP)";
                case "W32TIME_ALT": return "Windows Time (Windows Server 2003)";
                case "winlogonrpc": return "Winlogon";
                case "winreg": return "Remote registry service";
                case "winspipe": return "WINS service";
                case "wkssvc": return "Workstation service (ntsvcs alias)";
                default: return "";
            }
        }

        public override bool Equals(object obj)
        {
            return obj is PipeModel model && Equals(model);
        }

        public bool Equals(PipeModel other)
        {
            return Host == other.Host &&
                   Name == other.Name &&
                   ActiveConnections == other.ActiveConnections &&
                   MaxConnections == other.MaxConnections &&
                   Acl.Equals(other.Acl) &&
                   Hint == other.Hint;
        }

        public override int GetHashCode()
        {
            int hashCode = -824410345;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Host);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + ActiveConnections.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxConnections.GetHashCode();
            hashCode = hashCode * -1521134295 + Acl.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Hint);
            return hashCode;
        }

        public static bool operator ==(PipeModel left, PipeModel right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PipeModel left, PipeModel right)
        {
            return !(left == right);
        }
    }
}
