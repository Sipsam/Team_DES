using System;
using System.Globalization;
using System.Text;
using Unity.Collections;

namespace TwinEdge.Gameplay
{
    internal static class PlayerNickname
    {
        internal const int MaxLength = 12;
        internal const string Fallback = "플레이어";
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly int MaxBytes = default(FixedString64Bytes).Capacity;
        internal static bool IsAllowed(char c) => !char.IsControl(c) &&
            char.GetUnicodeCategory(c) is not (UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);

        internal static bool TryNormalize(string input, out string value)
        {
            value = string.Empty;
            // Bound raw input before Unicode normalization or segmentation.
            if (string.IsNullOrEmpty(input) || input.Length > 128) return false;
            foreach (char c in input)
                if (!IsAllowed(c)) return false;

            try
            {
                string normalized = input.Trim().Normalize(NormalizationForm.FormC);
                int length = new StringInfo(normalized).LengthInTextElements;
                if (length < 2 || length > MaxLength || Utf8.GetByteCount(normalized) > MaxBytes)
                    return false;
                value = normalized;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static byte[] Encode(string value) => Utf8.GetBytes(value);

        internal static bool TryDecode(byte[] payload, out string value)
        {
            value = string.Empty;
            if (payload == null || payload.Length == 0 || payload.Length > MaxBytes)
                return false;
            try { return TryNormalize(Utf8.GetString(payload), out value); }
            catch (DecoderFallbackException) { return false; }
        }
    }
}
