using System;
using System.Collections.Generic;
using TMPro;

namespace AnimalCafe.EditorTools.Phase5
{
    public static class Phase5UiFontCoverage
    {
        public static IReadOnlyList<uint> FindMissingUnicodeScalars(TMP_FontAsset font, string text)
        {
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (text == null) throw new ArgumentNullException(nameof(text));
            var missing = new List<uint>();
            for (var index = 0; index < text.Length; index++)
            {
                var scalar = char.ConvertToUtf32(text, index);
                if (char.IsHighSurrogate(text[index])) index++;
                if (!font.HasCharacter(scalar) && !missing.Contains((uint)scalar))
                    missing.Add((uint)scalar);
            }
            return missing;
        }
    }
}
