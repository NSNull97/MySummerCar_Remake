using System;
using MSC.UI.Runtime.Localization;
using NUnit.Framework;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class UiLocaleFormatterTests
    {
        [Test]
        public void Format_UsesConfiguredCultureDecimalSeparator()
        {
            var english = new UiLocaleFormatter("en-US");
            var russian = new UiLocaleFormatter("ru-RU");

            Assert.That(english.Format("{0:0.0}", 1234.5m), Is.EqualTo("1234.5"));
            Assert.That(russian.Format("{0:0.0}", 1234.5m), Is.EqualTo("1234,5"));
        }

        [Test]
        public void EnglishPluralRule_UsesOneOnlyForIntegerOne()
        {
            var formatter = new UiLocaleFormatter("en-US");

            Assert.That(formatter.SelectPluralForm(1m), Is.EqualTo(UiPluralForm.One));
            Assert.That(formatter.SelectPluralForm(-1m), Is.EqualTo(UiPluralForm.One));
            Assert.That(formatter.SelectPluralForm(0m), Is.EqualTo(UiPluralForm.Other));
            Assert.That(formatter.SelectPluralForm(2m), Is.EqualTo(UiPluralForm.Other));
            Assert.That(formatter.SelectPluralForm(1.0m), Is.EqualTo(UiPluralForm.Other));
        }

        [Test]
        public void RussianPluralRule_SelectsOneFewManyAndOther()
        {
            var formatter = new UiLocaleFormatter("ru-RU");

            Assert.That(formatter.SelectPluralForm(1m), Is.EqualTo(UiPluralForm.One));
            Assert.That(formatter.SelectPluralForm(21m), Is.EqualTo(UiPluralForm.One));
            Assert.That(formatter.SelectPluralForm(2m), Is.EqualTo(UiPluralForm.Few));
            Assert.That(formatter.SelectPluralForm(24m), Is.EqualTo(UiPluralForm.Few));
            Assert.That(formatter.SelectPluralForm(0m), Is.EqualTo(UiPluralForm.Many));
            Assert.That(formatter.SelectPluralForm(5m), Is.EqualTo(UiPluralForm.Many));
            Assert.That(formatter.SelectPluralForm(11m), Is.EqualTo(UiPluralForm.Many));
            Assert.That(formatter.SelectPluralForm(14m), Is.EqualTo(UiPluralForm.Many));
            Assert.That(formatter.SelectPluralForm(111m), Is.EqualTo(UiPluralForm.Many));
            Assert.That(formatter.SelectPluralForm(1.5m), Is.EqualTo(UiPluralForm.Other));
        }

        [Test]
        public void FormatPlural_SelectsPatternAndFormatsCountWithCulture()
        {
            var formatter = new UiLocaleFormatter("ru-RU");
            var pattern = new UiPluralPattern(
                "{0} файл",
                "{0} файла данных",
                few: "{0} файла",
                many: "{0} файлов");

            Assert.That(formatter.FormatPlural(1m, pattern), Is.EqualTo("1 файл"));
            Assert.That(formatter.FormatPlural(2m, pattern), Is.EqualTo("2 файла"));
            Assert.That(formatter.FormatPlural(5m, pattern), Is.EqualTo("5 файлов"));
            Assert.That(formatter.FormatPlural(1.5m, pattern), Is.EqualTo("1,5 файла данных"));
        }

        [Test]
        public void UnsupportedLanguage_UsesOtherWithoutInventingGrammar()
        {
            var formatter = new UiLocaleFormatter("fi-FI");

            Assert.That(formatter.SelectPluralForm(1m), Is.EqualTo(UiPluralForm.Other));
        }

        [Test]
        public void InvalidInputs_FailExplicitly()
        {
            Assert.Throws<ArgumentException>(() => new UiLocaleFormatter(" "));
            Assert.Throws<ArgumentException>(() => new UiPluralPattern(string.Empty, "{0} items"));
            Assert.Throws<ArgumentNullException>(() =>
                new UiLocaleFormatter("en-US").Format(null, Array.Empty<object>()));
        }
    }
}
