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
using System.Collections.ObjectModel;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PipeExplorer.ViewModels
{
    class AccessRuleViewModel
    {
        public string Principal { get; }
        public bool Allow { get; }
        public bool IsInherited { get; }
        public IEnumerable<string> Rights { get; }

        public AccessRuleViewModel(AccessRule rule)
        {
            Principal = rule.IdentityReference.Value;
            Allow = rule.AccessControlType == AccessControlType.Allow;
            IsInherited = rule.IsInherited;
            Rights = Array.Empty<string>();
        }
    }

    class AclViewModel
    {
        public ObservableCollection<AccessRuleViewModel> Rules { get; } = new ObservableCollection<AccessRuleViewModel>();

        public AclViewModel(PipeSecurity acl)
        {
            foreach (var rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).OfType<AccessRule>())
                Rules.Add(new AccessRuleViewModel(rule));
        }
    }
}
