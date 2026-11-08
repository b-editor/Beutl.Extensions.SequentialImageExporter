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
        Assert.That(controller.OutputFiles,
            Is.EqualTo(Enumerable.Range(0, 12).Select(i => Path.Combine(directory, $"frame_{i:D2}{extension}"))));
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
        Assert.That(controller.OutputFiles, Is.Empty);
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

        IReadOnlyList<string> published;
        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var controller = new SequentialImageExporterExtension().CreateController(output.TemporaryPath);
            using var renderer = ExportRendererFactory.Create(scene);
            using var progress = new Subject<MediaTime>();
            using var frames = new FrameProviderImpl(scene, rate, renderer, progress);
            using var samples = new UnusedSampleProvider();

            await controller.Encode(frames, samples, CancellationToken.None);
            published = output.Commit(controller.OutputFiles, CancellationToken.None);
        }

        string[] names = ["frame_0.png", "frame_1.png", "frame_2.png"];
        Assert.That(published, Is.EqualTo(names.Select(name => Path.Combine(directory, name))));
        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EquivalentTo(names));
        for (int i = 0; i < colors.Length; i++)
        {
            using var decoded = SKBitmap.Decode(Path.Combine(directory, $"frame_{i}.png"));
            Assert.That((decoded.Width, decoded.Height), Is.EqualTo((64, 32)));
            var center = decoded.GetPixel(32, 16);
            Assert.That((center.Red, center.Green, center.Blue, center.Alpha),
                Is.EqualTo((colors[i].R, colors[i].G, colors[i].B, (byte)255)));
        }
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
            var controller = new SequentialImageExportController(output.TemporaryPath);
            await controller.Encode(frames, samples, CancellationToken.None);
            output.Commit(controller.OutputFiles, CancellationToken.None);
        }

        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EquivalentTo(names));
        using var first = SKBitmap.Decode(Path.Combine(directory, "frame_0.png"));
        using var second = SKBitmap.Decode(Path.Combine(directory, "frame_1.png"));
        Assert.That((first.GetPixel(0, 0).Red, second.GetPixel(0, 0).Red), Is.EqualTo(((byte)0, (byte)16)));
        // 2.0.3 left a copy of the first frame under the name the user chose, which an export no longer writes.
        Assert.That(File.ReadAllText(Path.Combine(directory, "frame.png")), Is.EqualTo("earlier export"));
    }

    [Test]
    public async Task StagedExport_CancelledBetweenFramesPublishesNothing()
    {
        string earlierFrame = Path.Combine(directory, "frame_0.png");
        File.WriteAllText(earlierFrame, "earlier export");
        using var cancellation = new CancellationTokenSource();
        using var frames = new FrameProvider(3)
        {
            BeforeFrameCount = reads =>
            {
                if (reads == 1) cancellation.Cancel();
            }
        };
        using var samples = new UnusedSampleProvider();

        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var controller = new SequentialImageExportController(output.TemporaryPath);
            await controller.Encode(frames, samples, cancellation.Token);
            Assert.Throws<OperationCanceledException>(() => output.Commit(controller.OutputFiles, cancellation.Token));
        }

        Assert.That(frames.ReadCount, Is.EqualTo(1));
        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EqualTo(new[] { "frame_0.png" }));
        Assert.That(File.ReadAllText(earlierFrame), Is.EqualTo("earlier export"));
    }

    // As in OutputViewModel, a failed Encode never reaches Commit, and disposing the staged output deletes its frames.
    [Test]
    public async Task StagedExport_FailedEncodeLeavesAnEarlierExportAlone()
    {
        string[] names = ["frame_0.png", "frame_1.png", "frame_2.png"];
        foreach (string name in names)
            File.WriteAllText(Path.Combine(directory, name), "earlier export");
        using var frames = new FrameProvider(3) { EndAfter = 2 };
        using var samples = new UnusedSampleProvider();

        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var controller = new SequentialImageExportController(output.TemporaryPath);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.Encode(frames, samples, CancellationToken.None).AsTask());
        }

        Assert.That(frames.ReadCount, Is.EqualTo(3));
        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName), Is.EquivalentTo(names));
        Assert.That(names.Select(name => File.ReadAllText(Path.Combine(directory, name))), Is.All.EqualTo("earlier export"));
    }

    // A folder in the way of the second frame stops the publication after the first frame replaced the earlier one.
    [Test]
    public async Task StagedExport_FailedPublicationPutsBackAnEarlierExport()
    {
        File.WriteAllText(Path.Combine(directory, "frame_0.png"), "earlier export");
        Directory.CreateDirectory(Path.Combine(directory, "frame_1.png"));
        using var frames = new FrameProvider(3);
        using var samples = new UnusedSampleProvider();

        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var controller = new SequentialImageExportController(output.TemporaryPath);
            await controller.Encode(frames, samples, CancellationToken.None);
            Assert.Catch(() => output.Commit(controller.OutputFiles, CancellationToken.None));
        }

        Assert.That(Directory.GetFileSystemEntries(directory).Select(Path.GetFileName),
            Is.EquivalentTo(new[] { "frame_0.png", "frame_1.png" }));
        Assert.That(File.ReadAllText(Path.Combine(directory, "frame_0.png")), Is.EqualTo("earlier export"));
    }

    // What the output tab shows as the size: while frames are written, that of the files in the staging folder when
    // each frame is read, and once they are published, that of each file Commit returns.
    [Test]
    public async Task StagedExport_OutputTabSizeCountsEveryFrame()
    {
        var sizesWhileWriting = new List<long>();
        IReadOnlyList<string> published;
        using (var output = new StagedOutput(Path.Combine(directory, "frame.png")))
        {
            var staging = new DirectoryInfo(Path.GetDirectoryName(output.TemporaryPath)!);
            using var frames = new FrameProvider(3)
            {
                OnRead = () => sizesWhileWriting.Add(SizeOf(staging.EnumerateFiles()))
            };
            using var samples = new UnusedSampleProvider();
            var controller = new SequentialImageExportController(output.TemporaryPath);

            await controller.Encode(frames, samples, CancellationToken.None);
            published = output.Commit(controller.OutputFiles, CancellationToken.None);
        }

        FileInfo[] written = [.. Enumerable.Range(0, 3).Select(i => new FileInfo(Path.Combine(directory, $"frame_{i}.png")))];
        Assert.That(sizesWhileWriting, Is.EqualTo(new[] { 0, SizeOf(written[..1]), SizeOf(written[..2]) }));
        Assert.That(SizeOf(published.Select(path => new FileInfo(path))), Is.EqualTo(SizeOf(written)));
    }

    private static long SizeOf(IEnumerable<FileInfo> files) => files.Sum(file => file.Length);

    // Beutl's export writes through this internal type, so the tests use the real one and fail if its contract changes.
    private sealed class StagedOutput(string destination) : IDisposable
    {
        private static readonly Type OutputType = typeof(CoreObject).Assembly.GetType("Beutl.IO.StagedOutputFile", true)!;
        private readonly IDisposable output = (IDisposable)Activator.CreateInstance(OutputType, destination)!;

        public string TemporaryPath => (string)OutputType.GetProperty(nameof(TemporaryPath))!.GetValue(output)!;

        // OutputViewModel passes EncodingController.OutputFiles and reads the size from the files this returns.
        public IReadOnlyList<string> Commit(IReadOnlyList<string> files, CancellationToken cancellationToken)
            => (IReadOnlyList<string>)OutputType
                .GetMethod(nameof(Commit), [typeof(IReadOnlyList<string>), typeof(CancellationToken)])!
                .Invoke(output, BindingFlags.DoNotWrapExceptions, null, [files, cancellationToken], null)!;

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
