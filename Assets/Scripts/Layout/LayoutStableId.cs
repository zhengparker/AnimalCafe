using System;
using System.Text.RegularExpressions;

namespace AnimalCafe.Layout
{
    /// <summary>Authoritative stable ID contract shared by Layout and Scene adapters.</summary>
    internal static class LayoutStableId
    {
        private static readonly Regex Pattern = new Regex(
            "^[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant);

        public static void Validate(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!IsValid(value))
            {
                throw new ArgumentException(
                    "Layout stable ID has an invalid format.",
                    parameterName);
            }
        }

        public static bool IsValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Pattern.IsMatch(value);
        }
    }
}
