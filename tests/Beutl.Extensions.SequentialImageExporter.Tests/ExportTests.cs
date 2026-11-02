using System.Reactive.Subjects;
using System.Reflection;
using Beutl.Audio;
using Beutl.Extensibility;
using Beutl.Graphics.Backend;
using Beutl.Graphics.Rendering;
using Beutl.Graphics.Shapes;
using Beutl.Media;
using Beutl.Models;
using Beutl.ProjectSystem;
using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter.Tests;

[TestFixture]
public class ExportTests
{
    private string directory = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Directory.CreateTempSubdirectory("sequential-image-exporter-").FullName;
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(directory, true);
    }

    [TestCase(".png")]
    [TestCase(".jpg")]
    [TestCase(".jpeg")]
    [TestCase(".webp")]
    public async Task Encode_PreservesFrameOrderAndZeroPaddedNames(string extension)
    {
        var controller = (SequentialImageExportController)new SequentialImageExporterExtension()
            .CreateController(Path.Combine(directory, "frame" + extension));
        controller.VideoSettings.WebpCompression.Value = SKWebpEncoderCompression.Lossless;
        using var frames = new FrameProvider(12);
        using var samples = new UnusedSampleProvider();

        await controller.Encode(frames, samples, CancellationToken.None);

        Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName),
            Is.EquivalentTo(Enumerable.Range(0, 12).Select(i => $"frame_{i:D2}{extension}")));
        for (int i = 0; i < 12; i++)
        {
            using var decoded = SKBitmap.Decode(Path.Combine(directory, $"frame_{i:D2}{extension}"));
            Assert.That(decoded, Is.Not.Null);
            Assert.That((decoded.Width, decoded.Height), Is.EqualTo((16, 8)));
            var color = decoded.GetPixel(0, 0);
            int tolerance = extension is ".jpg" or ".jpeg" ? 2 : 0;
            Assert.That((int)color.Red, Is.EqualTo(i * 16).Within(tolerance));
            Assert.That((int)color.Green, Is.EqualTo(64).Within(tolerance));
            Assert.That((int)color.Blue, Is.EqualTo(128).Within(tolerance));
        }

        Assert.That(frames.ReadCount, Is.EqualTo(12));
        Assert.That(frames.ReturnedFrames.All(frame => frame.IsDisposed), Is.True);
        Assert.That(frames.IsDisposed, Is.False);
        Assert.That(samples.IsDisposed, Is.False);
        Assert.That(controller.IsAudioEnabled, Is.False);
    }

    [Test]
    public async Task Encode_PrematureEndDoesNotReportSuccess()
    {
        var controller = new SequentialImageExportController(Path.Combine(directory, "frame.png"));
        using var frames = new FrameProvider(3) { EndAfter = 1 };
        using var samples = new UnusedSampleProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Encode(frames, samples, CancellationToken.None).AsTask());

        Assert.That(exception!.Message, Is.EqualTo("The frame provider ended before its frame count."), exception.ToString());
        Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName), Is.EqualTo(new[] { "frame_0.png" }));
        Assert.That(frames.ReadCount, Is.EqualTo(2));
        Assert.That(frames.ReturnedFrames.Single().IsDisposed, Is.True);
        Assert.That(frames.IsDisposed, Is.False);
        Assert.That(samples.IsDisposed, Is.False);
    }

    [TestCase(0, false)]
    [TestCase(3, true)]
    public async Task Encode_EmptyOrAlreadyCancelledDoesNotRead(long frameCount, bool cancelled)
    {
        var controller = new SequentialImageExportController(Path.Combine(directory, "frame.png"));
        using var frames = new FrameProvider(frameCount);
        using var samples = new UnusedSampleProvider();

        await controller.Encode(frames, samples, new CancellationToken(cancelled));

        Assert.That(frames.ReadCount, Is.Zero);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Assert.That(frames.IsDisposed, Is.False);
        Assert.That(samples.IsDisposed, Is.False);
    }

    [Test]
    public async Task Encode_CancellationAfterReadDisposesFrameWithoutSaving()
    {
        var controller = new SequentialImageExportController(Path.Combine(directory, "frame.png"));
        using var cancellation = new CancellationTokenSource();
        using var frames = new FrameProvider(3) { OnRead = cancellation.Cancel };
        using var samples = new UnusedSampleProvider();

        await Assert.ThatAsync(async () => await controller.Encode(frames, samples, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(frames.ReadCount, Is.EqualTo(1));
        Assert.That(frames.ReturnedFrames.Single().IsDisposed, Is.True);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Assert.That(frames.IsDisposed, Is.False);
        Assert.That(samples.IsDisposed, Is.False);
    }

    // Renders a real scene and exports it the way Beutl's OutputViewModel does.
    [Test]
    public async Task StagedExport_PublishesRenderedFrames()
    {
        if (!RenderThread.Dispatcher.Invoke(() => GraphicsContextFactory.GetOrCreateShared() is not null))
            Assert.Ignore("No graphics context is available on this host.");

        var rate = new Rational(30);
        Color[] colors = [Colors.Red, Colors.Lime, Colors.Blue];
        var scene = new Scene(64, 32, "Export") { Duration = { Value = MediaTime.FromFrames(colors.Length, rate) } };
        for (int i = 0; i < colors.Length; i++)
        {
            var element = new Element
            {
                Start = { Value = MediaTime.FromFrames(i, rate) },
                Length = { Value = MediaTime.FromFrames(1, rate) },
            };
            element.AddObject(new RectShape
            {
                Width = { Value = 32 },
                Height = { Value = 16 },
                Fill = { Value = new SolidColorBrush(colors[i]) },
            });
            scene.AddChild(element);
        }

        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var controller = new SequentialImageExporterExtension().CreateController(output.TemporaryPath);
            using var renderer = ExportRendererFactory.Create(scene);
            using var progress = new Subject<MediaTime>();
            using var frames = new FrameProviderImpl(scene, rate, renderer, progress);
            using var samples = new UnusedSampleProvider();

            await controller.Encode(frames, samples, CancellationToken.None);
            output.Commit(CancellationToken.None);
        }

        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName),
            Is.EquivalentTo(new[] { "frame.png", "frame_0.png", "frame_1.png", "frame_2.png" }));
        for (int i = 0; i < colors.Length; i++)
        {
            using var decoded = SKBitmap.Decode(Path.Combine(directory, $"frame_{i}.png"));
            Assert.That((decoded.Width, decoded.Height), Is.EqualTo((64, 32)));
            var center = decoded.GetPixel(32, 16);
            Assert.That((center.Red, center.Green, center.Blue, center.Alpha),
                Is.EqualTo((colors[i].R, colors[i].G, colors[i].B, (byte)255)));
        }

        Assert.That(File.ReadAllBytes(Path.Combine(directory, "frame.png")),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, "frame_0.png"))));
    }

    [Test]
    public async Task StagedExport_ReplacesAnEarlierExport()
    {
        string[] names = ["frame.png", "frame_0.png", "frame_1.png"];
        foreach (string name in names)
            File.WriteAllText(Path.Combine(directory, name), "earlier export");
        using var frames = new FrameProvider(2);
        using var samples = new UnusedSampleProvider();

        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            await new SequentialImageExportController(output.TemporaryPath)
                .Encode(frames, samples, CancellationToken.None);
            output.Commit(CancellationToken.None);
        }

        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EquivalentTo(names));
        using var first = SKBitmap.Decode(Path.Combine(directory, "frame_0.png"));
        using var second = SKBitmap.Decode(Path.Combine(directory, "frame_1.png"));
        Assert.That((first.GetPixel(0, 0).Red, second.GetPixel(0, 0).Red), Is.EqualTo(((byte)0, (byte)16)));
        Assert.That(File.ReadAllBytes(Path.Combine(directory, "frame.png")),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, "frame_0.png"))));
    }

    [Test]
    public async Task StagedExport_CancelledBetweenFramesPublishesNothing()
    {
        string destination = Path.Combine(directory, "frame.png");
        File.WriteAllText(destination, "earlier export");
        using var cancellation = new CancellationTokenSource();
        using var frames = new FrameProvider(3)
        {
            BeforeFrameCount = reads =>
            {
                if (reads == 1) cancellation.Cancel();
            }
        };
        using var samples = new UnusedSampleProvider();

        using (var output = new StagedOutput(destination))
        {
            await new SequentialImageExportController(output.TemporaryPath)
                .Encode(frames, samples, cancellation.Token);
            Assert.Throws<OperationCanceledException>(() => output.Commit(cancellation.Token));
        }

        Assert.That(frames.ReadCount, Is.EqualTo(1));
        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EqualTo(new[] { "frame.png" }));
        Assert.That(File.ReadAllText(destination), Is.EqualTo("earlier export"));
    }

    // Beutl's export writes through this internal type, so the tests use the real one and fail if its contract changes.
    private sealed class StagedOutput(string destination) : IDisposable
    {
        private static readonly Type OutputType = typeof(CoreObject).Assembly.GetType("Beutl.IO.StagedOutputFile", true)!;
        private readonly IDisposable output = (IDisposable)Activator.CreateInstance(OutputType, destination)!;

        public string TemporaryPath => (string)OutputType.GetProperty(nameof(TemporaryPath))!.GetValue(output)!;

        public void Commit(CancellationToken cancellationToken)
            => OutputType.GetMethod(nameof(Commit))!.Invoke(output, BindingFlags.DoNotWrapExceptions, null, [cancellationToken], null);

        public void Dispose() => output.Dispose();
    }

    private sealed class FrameProvider(long frameCount) : IFrameProvider
    {
        public long FrameCount
        {
            get
            {
                BeforeFrameCount?.Invoke(ReadCount);
                return frameCount;
            }
        }

        public Rational FrameRate => new(30, 1);
        public Action<int>? BeforeFrameCount { get; init; }
        public int EndAfter { get; init; } = int.MaxValue;
        public Action? OnRead { get; init; }
        public int ReadCount { get; private set; }
        public bool IsDisposed { get; private set; }
        public List<Bitmap> ReturnedFrames { get; } = [];

        public async ValueTask<Bitmap?> ReadNextFrame()
        {
            await Task.Yield();
            int index = ReadCount++;
            if (index >= EndAfter) return null;
            var pixels = new SKBitmap(16, 8, SKColorType.Bgra8888, SKAlphaType.Premul);
            pixels.Erase(new SKColor((byte)(index * 16), 64, 128));
            var frame = new Bitmap(pixels);
            ReturnedFrames.Add(frame);
            OnRead?.Invoke();
            return frame;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class UnusedSampleProvider : ISampleProvider
    {
        public long SampleCount => throw new AssertionException("Image export must not read audio.");
        public long SampleRate => throw new AssertionException("Image export must not read audio.");
        public bool IsDisposed { get; private set; }

        public ValueTask<AudioBuffer?> ReadNextSamples(int length)
            => throw new AssertionException("Image export must not read audio.");

        public void Dispose() => IsDisposed = true;
    }
}
