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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using PipeExplorer.Models;
using PipeExplorer.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;

namespace PipeExplorer.ViewModels
{
    class PipeViewModel : ReactiveObject
    {
        public string Host { get; }
        public string Name { get; }
        public string Path { get; }
        public string Created { get; private set; }
        [Reactive] public string Hint { get; private set; }
        [Reactive] public string MaxConnections { get; private set; }
        [Reactive] public uint ActiveConnections { get; private set; }
        [Reactive] public string Connections { get; private set; }
        [Reactive] public AclModel Acl { get; private set; }
        [Reactive] public string PlainAcl { get; private set; }

        [Reactive] public bool BeingRemoved { get; private set; }
        [Reactive] public bool RecentlyUpdated { get; private set; }
        [Reactive] public bool RecentlyAdded { get; private set; }

        private DateTime lastUpdateTimestamp;

        public void MarkForRemoval()
        {
            RecentlyAdded = false;
            RecentlyUpdated = false;
            BeingRemoved = true;
        }

        public PipeViewModel(PipeModel model, bool markAsRecentlyAdded = true)
        {
            Host = model.Host;
            Name = model.Name;
            Path = model.Path;
            
            var nowStr = DateTime.Now.ToString(CultureInfo.CurrentUICulture);
            if (markAsRecentlyAdded)
            {
                RecentlyAdded = true;
                Created = nowStr;
            }
            else
            {
                Created = string.Format(Properties.Resources.DateCreatedUpToFormatted, nowStr);
            }

            SyncWithModel(model);
            this.WhenValueChanged(x => x.Acl).Subscribe(UpdatePlainAcl);

            Task.Delay(Locator.Current.GetService<ISettings>().HighlightDuration).ContinueWith(_ => RecentlyAdded = false);
        }

        private void UpdatePlainAcl(AclModel acl)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(acl.Owner))
            {
                sb.AppendFormat("{0}: {1}", Properties.Resources.AclOwner, acl.Owner);
                if (!string.IsNullOrEmpty(acl.Group) || acl.Rules.Any())
                    sb.Append(", ");
            }
            if (!string.IsNullOrEmpty(acl.Group))
            {
                sb.AppendFormat("{0}: {1}", Properties.Resources.AclGroup, acl.Group);
                if (acl.Rules.Any())
                    sb.Append(", ");
            }
            if (acl.Rules.Any())
            {
                var rules = string.Join("; ", acl.Rules.Select(r => r.ToString()));
                sb.AppendFormat("{0}: {1}", Properties.Resources.AclRules, rules);
            }
            PlainAcl = sb.ToString();
        }

        public void Update(PipeModel model)
        {
            if (BeingRemoved)
            {
                BeingRemoved = false;
                RecentlyAdded = true;
                var nowStr = DateTime.Now.ToString(CultureInfo.CurrentUICulture);
                Created = nowStr;
            }

            SyncWithModel(model);

            if (!RecentlyAdded)
            {
                RecentlyUpdated = true;
                var ts = lastUpdateTimestamp = DateTime.Now;
                Task.Delay(Locator.Current.GetService<ISettings>().HighlightDuration).ContinueWith(_ =>
                {
                    if (ts == lastUpdateTimestamp)
                        RecentlyUpdated = false;
                });
            }
        }

        /// <summary>
        /// Common code for both <see cref="SyncWithModel(PipeModel)"/> constructor and <see cref="Update(PipeModel)"/>.
        /// </summary>
        /// <param name="model">Model to take new values from</param>
        private void SyncWithModel(PipeModel model)
        {
            ActiveConnections = model.ActiveConnections;
            MaxConnections = model.MaxConnections >= 0 ? model.MaxConnections.ToString() : "∞";
            Connections = $"{ActiveConnections} / {MaxConnections}";
            Hint = model.Hint;
            Acl = model.Acl;
        }
    }
}
