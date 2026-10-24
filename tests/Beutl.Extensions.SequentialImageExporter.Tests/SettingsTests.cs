using System.ComponentModel;
using System.Text.Json.Nodes;
using Beutl.Editor.Services;
using Beutl.Media;
using Beutl.Media.Encoding;
using SkiaSharp;
using RangeAttribute = System.ComponentModel.DataAnnotations.RangeAttribute;

namespace Beutl.Extensions.SequentialImageExporter.Tests;

[TestFixture]
public class SettingsTests
{
    [Test]
    public void Preview8VideoSettings_PreserveValuesAcrossLoadAndSave()
    {
        // Captured from extension commit 9d72325 using Beutl 2.0.0-preview.8.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Fixtures", "preview8-video.json")))!.AsObject();
        var settings = new SequentialImageExportSettings();
        EncoderSettingsJson.Populate(settings, json);

        Assert.That(settings.Quality.Value, Is.EqualTo(42.5f));
        Assert.That(settings.PngzLibLevel.Value, Is.EqualTo(3));
        Assert.That(settings.JpegDownsample.Value, Is.EqualTo(SKJpegEncoderDownsample.Downsample444));
        Assert.That(settings.JpegAlphaOption.Value, Is.EqualTo(SKJpegEncoderAlphaOption.BlendOnBlack));
        Assert.That(settings.WebpCompression.Value, Is.EqualTo(SKWebpEncoderCompression.Lossless));
        Assert.That(settings.FrameRate.Value, Is.EqualTo(new Rational(24000, 1001)));
        Assert.That(settings.SourceSize.Value, Is.EqualTo(new PixelSize(1920, 1080)));
        var flags = settings.PngFilterFlags.Value;
        Assert.That(new[] { flags.None.Value, flags.Sub.Value, flags.Up.Value, flags.Avg.Value, flags.Paeth.Value },
            Is.EqualTo(new[] { false, true, false, true, false }));

        var saved = EncoderSettingsJson.Serialize(settings)!;
        foreach (string name in new[] { "PngzLibLevel", "PngFilterFlags", "Quality", "JpegDownsample",
                     "JpegAlphaOption", "WebpCompression", "SourceSize", "FrameRate", "Id", "Name" })
        {
            Assert.That(JsonNode.DeepEquals(saved[name], json[name]), Is.True, name);
        }

        var restored = new SequentialImageExportSettings();
        EncoderSettingsJson.Populate(restored, saved);
        Assert.That(JsonNode.DeepEquals(EncoderSettingsJson.Serialize(restored), saved), Is.True);
    }

    [Test]
    public void Preview8AudioSettings_LoadWithoutEnablingAudioFields()
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Fixtures", "preview8-audio.json")))!.AsObject();
        var settings = new SequentialImageExportAudioSettings();

        EncoderSettingsJson.Populate(settings, json);

        Assert.That(settings.Id.Value.ToString(), Is.EqualTo(json["Id"]!.GetValue<string>()));
        Assert.That(settings.SampleRate.Value, Is.EqualTo(44100));
        Assert.That(settings.Channels.Value, Is.EqualTo(2));
        Assert.That(settings.Bitrate.Value, Is.EqualTo(128000));
        Assert.That(new[] { settings.SampleRate, settings.Channels, settings.Bitrate }
            .All(property => property.GetAttributes()!.OfType<BrowsableAttribute>().Single().Browsable == false), Is.True);
    }

    [Test]
    public void DefaultsAndPropertyRegistration_PreserveEditableSettings()
    {
        var settings = new SequentialImageExportSettings();
        var other = new SequentialImageExportSettings();

        Assert.That(settings.Quality.Value, Is.EqualTo(100f));
        Assert.That(settings.PngzLibLevel.Value, Is.EqualTo(6));
        Assert.That(settings.JpegDownsample.Value, Is.EqualTo(SKJpegEncoderDownsample.Downsample420));
        Assert.That(settings.JpegAlphaOption.Value, Is.EqualTo(SKJpegEncoderAlphaOption.Ignore));
        Assert.That(settings.WebpCompression.Value, Is.EqualTo(SKWebpEncoderCompression.Lossy));
        var flags = settings.PngFilterFlags.Value;
        Assert.That(flags.Properties.Where(property => property.ValueType == typeof(bool))
            .Select(property => property.Value), Is.EqualTo(new[] { true, true, true, true, true }));
        flags.Sub.Value = false;
        Assert.That(other.PngFilterFlags.Value.Sub.Value, Is.True);

        Assert.That(settings.Properties.Select(property => property.Name), Is.Unique);
        Assert.That(settings.Properties, Does.Contain(settings.Quality));
        Assert.That(settings.Quality.GetOwnerObject(), Is.SameAs(settings));
        Assert.That(settings.Quality.GetAttributes()!.OfType<RangeAttribute>().Single().Maximum, Is.EqualTo(100d));
        Assert.That(settings.PngzLibLevel.GetAttributes()!.OfType<RangeAttribute>().Single().Maximum, Is.EqualTo(9));
        string? changed = null;
        settings.PropertyChanged += (_, args) => changed = args.PropertyName;
        settings.Quality.Value = 75f;
        Assert.That(changed, Is.EqualTo(nameof(settings.Quality)));
    }

    [Test]
    public void InheritedUnusedSettings_StayHiddenAndAreNotWritten()
    {
        VideoEncoderSettings video = new SequentialImageExportSettings();
        AudioEncoderSettings audio = new SequentialImageExportAudioSettings();
        IProperty[] hidden = [video.DestinationSize, video.Bitrate, video.KeyframeRate,
            audio.Bitrate, audio.SampleRate, audio.Channels];

        foreach (var property in hidden)
        {
            Assert.That(property.GetAttributes()!.OfType<BrowsableAttribute>().Single().Browsable,
                Is.False, property.Name);
            Assert.That(property.GetAttributes()!.OfType<NotAutoSerializedAttribute>(), Is.Not.Empty, property.Name);
        }

        var videoJson = EncoderSettingsJson.Serialize(video)!;
        var audioJson = EncoderSettingsJson.Serialize(audio)!;
        Assert.That(new[] { "DestinationSize", "Bitrate", "KeyframeRate" }.Any(videoJson.ContainsKey), Is.False);
        Assert.That(new[] { "Bitrate", "SampleRate", "Channels" }.Any(audioJson.ContainsKey), Is.False);
    }
}
