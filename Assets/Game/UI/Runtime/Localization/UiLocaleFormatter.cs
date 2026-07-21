using System;
using System.Globalization;

namespace MSC.UI.Runtime.Localization
{
    public enum UiPluralForm
    {
        One = 0,
        Few = 1,
        Many = 2,
        Other = 3,
    }

    /// <summary>
    /// Localized composite-format patterns for a count. English callers normally
    /// provide One and Other; Russian callers additionally provide Few and Many.
    /// Missing language-specific forms deliberately fall back to Other.
    /// </summary>
    public readonly struct UiPluralPattern
    {
        public UiPluralPattern(
            string one,
            string other,
            string few = null,
            string many = null)
        {
            if (string.IsNullOrEmpty(one))
            {
                throw new ArgumentException("A plural One pattern is required.", nameof(one));
            }

            if (string.IsNullOrEmpty(other))
            {
                throw new ArgumentException("A plural Other pattern is required.", nameof(other));
            }

            One = one;
            Few = few;
            Many = many;
            Other = other;
        }

        public string One { get; }

        public string Few { get; }

        public string Many { get; }

        public string Other { get; }

        internal string Resolve(UiPluralForm form)
        {
            if (string.IsNullOrEmpty(One) || string.IsNullOrEmpty(Other))
            {
                throw new InvalidOperationException(
                    "UiPluralPattern must be created with non-empty One and Other patterns.");
            }

            switch (form)
            {
                case UiPluralForm.One:
                    return One;
                case UiPluralForm.Few:
                    return string.IsNullOrEmpty(Few) ? Other : Few;
                case UiPluralForm.Many:
                    return string.IsNullOrEmpty(Many) ? Other : Many;
                default:
                    return Other;
            }
        }
    }

    /// <summary>
    /// Culture-aware formatting and the bounded English/Russian plural rules
    /// required by Milestone 08A. Unsupported languages use Other instead of
    /// silently pretending that English grammar is correct for them.
    /// </summary>
    public sealed class UiLocaleFormatter
    {
        public UiLocaleFormatter(string localeId)
            : this(CreateCulture(localeId))
        {
        }

        public UiLocaleFormatter(CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            Culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
        }

        public CultureInfo Culture { get; }

        public string Format(string compositeFormat, params object[] arguments)
        {
            if (compositeFormat == null)
            {
                throw new ArgumentNullException(nameof(compositeFormat));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return string.Format(Culture, compositeFormat, arguments);
        }

        public UiPluralForm SelectPluralForm(decimal count)
        {
            decimal absoluteCount = count < 0m ? -count : count;
            bool hasVisibleFraction = HasVisibleFraction(count);
            string language = Culture.TwoLetterISOLanguageName;

            if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            {
                return !hasVisibleFraction && absoluteCount == 1m
                    ? UiPluralForm.One
                    : UiPluralForm.Other;
            }

            if (!string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase))
            {
                return UiPluralForm.Other;
            }

            if (hasVisibleFraction)
            {
                return UiPluralForm.Other;
            }

            int lastDigit = decimal.ToInt32(absoluteCount % 10m);
            int lastTwoDigits = decimal.ToInt32(absoluteCount % 100m);
            if (lastDigit == 1 && lastTwoDigits != 11)
            {
                return UiPluralForm.One;
            }

            if (lastDigit >= 2 &&
                lastDigit <= 4 &&
                (lastTwoDigits < 12 || lastTwoDigits > 14))
            {
                return UiPluralForm.Few;
            }

            return UiPluralForm.Many;
        }

        public string FormatPlural(decimal count, UiPluralPattern pattern)
        {
            string selectedPattern = pattern.Resolve(SelectPluralForm(count));
            return string.Format(Culture, selectedPattern, count);
        }

        private static CultureInfo CreateCulture(string localeId)
        {
            if (string.IsNullOrWhiteSpace(localeId))
            {
                throw new ArgumentException("A locale identifier is required.", nameof(localeId));
            }

            return CultureInfo.GetCultureInfo(localeId);
        }

        private static bool HasVisibleFraction(decimal value)
        {
            int flags = decimal.GetBits(value)[3];
            return ((flags >> 16) & 0x7F) != 0;
        }
    }
}
