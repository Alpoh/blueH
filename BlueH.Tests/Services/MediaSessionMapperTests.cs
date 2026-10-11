using BlueH.Models;
using BlueH.Services;
using Windows.Media.Control;

namespace BlueH.Tests.Services;

[TestFixture]
public class MediaSessionMapperTests
{
    private const string EdgeAppId = "MSEdge";
    private const string Title = "The Mundane and the Magic";
    private const string Artist = "Dark Tranquillity";

    private static readonly MediaTransportControls PlayingControls = new(
        CanPlay: false,
        CanPause: true,
        CanSkipNext: true,
        CanSkipPrevious: true);

    [Test]
    public void TheMetadataIsCarriedVerbatim()
    {
        MediaSession session = MediaSessionMapper.Map(
            EdgeAppId,
            Title,
            Artist,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            PlayingControls);

        Assert.Multiple(() =>
        {
            Assert.That(session.SourceAppId, Is.EqualTo(EdgeAppId));
            Assert.That(session.Title, Is.EqualTo(Title));
            Assert.That(session.Artist, Is.EqualTo(Artist));
            Assert.That(session.Controls, Is.EqualTo(PlayingControls));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankMetadataIsModelledAsAbsent(string? blank)
    {
        MediaSession session = MediaSessionMapper.Map(
            EdgeAppId,
            blank,
            blank,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            PlayingControls);

        Assert.Multiple(() =>
        {
            Assert.That(session.Title, Is.Null);
            Assert.That(session.Artist, Is.Null);
        });
    }

    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, true)]
    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused, false)]
    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped, false)]
    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing, false)]
    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened, false)]
    [TestCase(GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed, false)]
    public void OnlyThePlayingStatusCountsAsPlaying(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus playbackStatus,
        bool expectedIsPlaying)
    {
        MediaSession session = MediaSessionMapper.Map(EdgeAppId, Title, Artist, playbackStatus, PlayingControls);

        Assert.That(session.IsPlaying, Is.EqualTo(expectedIsPlaying));
    }
}
