using BlueH.Time;

namespace BlueH.Tests.Time;

[TestFixture]
public class CanonicalTimestampTests
{
    private static readonly DateTimeOffset AfternoonInUtc =
        new(2026, 10, 10, 15, 48, 3, TimeSpan.Zero);

    private const string AfternoonFormatted = "2026-10-10 15:48:03";

    [Test]
    public void FormatProducesTheCanonicalShape()
    {
        Assert.That(CanonicalTimestamp.Format(AfternoonInUtc), Is.EqualTo(AfternoonFormatted));
    }

    [Test]
    public void FormatKeepsMonthsAndMinutesInTheirOwnPositions()
    {
        DateTimeOffset distinguishable = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.That(CanonicalTimestamp.Format(distinguishable), Is.EqualTo("2026-01-02 03:04:05"));
    }

    [Test]
    public void FormatUsesTheTwentyFourHourClock()
    {
        DateTimeOffset afternoon = new(2026, 1, 2, 13, 4, 5, TimeSpan.Zero);

        Assert.That(CanonicalTimestamp.Format(afternoon), Is.EqualTo("2026-01-02 13:04:05"));
    }

    [Test]
    public void FormatConvertsAnOffsetToUtc()
    {
        DateTimeOffset twoHoursAhead = new(2026, 10, 10, 17, 48, 3, TimeSpan.FromHours(2));

        Assert.That(CanonicalTimestamp.Format(twoHoursAhead), Is.EqualTo(AfternoonFormatted));
    }

    [Test]
    [SetCulture("es-ES")]
    public void FormatIgnoresASpanishCulture()
    {
        Assert.That(CanonicalTimestamp.Format(AfternoonInUtc), Is.EqualTo(AfternoonFormatted));
    }

    [Test]
    [SetCulture("ar-SA")]
    public void FormatIgnoresACultureWithANonGregorianCalendar()
    {
        Assert.That(CanonicalTimestamp.Format(AfternoonInUtc), Is.EqualTo(AfternoonFormatted));
    }

    [Test]
    public void FormatDateOnlyOmitsTheTime()
    {
        Assert.That(CanonicalTimestamp.FormatDateOnly(AfternoonInUtc), Is.EqualTo("2026-10-10"));
    }

    [Test]
    public void TryParseAcceptsTheCanonicalShape()
    {
        bool parsed = CanonicalTimestamp.TryParse(AfternoonFormatted, out DateTimeOffset instant);

        Assert.That(parsed, Is.True);
        Assert.That(instant, Is.EqualTo(AfternoonInUtc));
    }

    [Test]
    public void TryParseTreatsTheTextAsUtc()
    {
        CanonicalTimestamp.TryParse(AfternoonFormatted, out DateTimeOffset instant);

        Assert.That(instant.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    [SetCulture("ar-SA")]
    public void TryParseIgnoresACultureWithANonGregorianCalendar()
    {
        bool parsed = CanonicalTimestamp.TryParse(AfternoonFormatted, out DateTimeOffset instant);

        Assert.That(parsed, Is.True);
        Assert.That(instant, Is.EqualTo(AfternoonInUtc));
    }

    [Test]
    public void FormatAndTryParseRoundTripTheSameInstant()
    {
        CanonicalTimestamp.TryParse(CanonicalTimestamp.Format(AfternoonInUtc), out DateTimeOffset instant);

        Assert.That(instant, Is.EqualTo(AfternoonInUtc));
    }

    [TestCase("10/10/2026 15:48:03")]
    [TestCase("2026-10-10T15:48:03")]
    [TestCase("2026-10-10 15:48")]
    [TestCase("2026-10-10")]
    [TestCase("not a timestamp")]
    [TestCase("")]
    [TestCase(null)]
    public void TryParseRejectsAnythingButTheCanonicalShape(string? text)
    {
        Assert.That(CanonicalTimestamp.TryParse(text, out _), Is.False);
    }
}
