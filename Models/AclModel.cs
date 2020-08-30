using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeExplorer.Models
{
    class AclRuleModel : IEquatable<AclRuleModel>
    {
        public string Principal { get; }
        public bool Allows { get; }
        public PipeAccessRights Rights { get; }

        public AclRuleModel(string principal, bool allows, PipeAccessRights rights)
        {
            Principal = principal ?? throw new ArgumentNullException(nameof(principal));
            Allows = allows;
            Rights = rights;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AclRuleModel);
        }

        public bool Equals(AclRuleModel other)
        {
            return other != null &&
                   Principal == other.Principal &&
                   Allows == other.Allows &&
                   Rights == other.Rights;
        }

        public override int GetHashCode()
        {
            int hashCode = 35785606;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Principal);
            hashCode = hashCode * -1521134295 + Allows.GetHashCode();
            hashCode = hashCode * -1521134295 + Rights.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(AclRuleModel left, AclRuleModel right)
        {
            return EqualityComparer<AclRuleModel>.Default.Equals(left, right);
        }

        public static bool operator !=(AclRuleModel left, AclRuleModel right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            var rights = string.Join(", ", Rights.ToString("f"));
            return string.Format(Allows ? Properties.Resources.AclAllowFor : Properties.Resources.AclDenyFor, Principal, rights);
        }
    }

    class AclModel : IEquatable<AclModel>
    {
        public string Owner { get; }
        public string Group { get; }
        public IEnumerable<AclRuleModel> Rules { get; }

        public AclModel(string owner, string group, IEnumerable<AclRuleModel> rules)
        {
            Owner = owner ?? "";
            Group = group ?? "";
            Rules = rules ?? Array.Empty<AclRuleModel>();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AclModel);
        }

        public bool Equals(AclModel other)
        {
            return other != null &&
                   Owner == other.Owner &&
                   Group == other.Group &&
                   Rules.SequenceEqual(other.Rules);
        }

        public override int GetHashCode()
        {
            int hashCode = -1621901248;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Owner);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Group);
            hashCode = hashCode * -1521134295 + EqualityComparer<IEnumerable<AclRuleModel>>.Default.GetHashCode(Rules);
            return hashCode;
        }

        public static bool operator ==(AclModel left, AclModel right)
        {
            return EqualityComparer<AclModel>.Default.Equals(left, right);
        }

        public static bool operator !=(AclModel left, AclModel right)
        {
            return !(left == right);
        }
    }
}
