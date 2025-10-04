using Anviz.SDK.Responses;
using System;
using System.Linq;

namespace Anviz.SDK.Utils
{
    public static class UserInfoExtensions
    {
        private const int DeviceNameMaxChars = 10;

        public static UserInfo CloneForDevice(this UserInfo source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var clone = new UserInfo(source)
            {
                Name = NormalizeName(source.Name)
            };

            return clone;
        }

        public static bool EquivalentTo(this UserInfo source, UserInfo target)
        {
            if (ReferenceEquals(source, target))
            {
                return true;
            }

            if (source == null || target == null)
            {
                return false;
            }

            return source.Id == target.Id &&
                   NullableEquals(source.Password, target.Password) &&
                   NullableEquals(source.Card, target.Card) &&
                   string.Equals(NormalizeName(source.Name), NormalizeName(target.Name), StringComparison.Ordinal) &&
                   source.Department == target.Department &&
                   source.Group == target.Group &&
                   source.Mode == target.Mode &&
                   source.PWDH8 == target.PWDH8 &&
                   source.Keep == target.Keep &&
                   source.Message == target.Message &&
                   source.EnrolledFingerprints.SequenceEqual(target.EnrolledFingerprints);
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim().TrimEnd('\0');
            if (trimmed.Length <= DeviceNameMaxChars)
            {
                return trimmed;
            }

            return trimmed.Substring(0, DeviceNameMaxChars);
        }

        private static bool NullableEquals(ulong? left, ulong? right)
        {
            if (!left.HasValue && !right.HasValue)
            {
                return true;
            }

            if (left.HasValue != right.HasValue)
            {
                return false;
            }

            return left.GetValueOrDefault() == right.GetValueOrDefault();
        }
    }
}

