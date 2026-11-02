using System.Collections.Frozen;
using System.Runtime.InteropServices;
using Beutl.Extensibility;
using Beutl.Graphics;
using Beutl.Media;
using Beutl.Media.Encoding;
using Beutl.Media.Pixel;
using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter;

public class SequentialImageExportController : EncodingController
{
    private const string StagingDirectoryPrefix = ".beutl-output-";

    private static readonly FrozenDictionary<string, EncodedImageFormat> ExtensionToFormat = ImmutableCollectionsMarshal
        .AsImmutableArray<KeyValuePair<string, EncodedImageFormat>>(
        [
            new(".bmp", EncodedImageFormat.Bmp),
            new(".gif", EncodedImageFormat.Gif),
            new(".ico", EncodedImageFormat.Ico),
            new(".jpg", EncodedImageFormat.Jpeg),
            new(".jpeg", EncodedImageFormat.Jpeg),
            new(".png", EncodedImageFormat.Png),
            new(".wbmp", EncodedImageFormat.Wbmp),
            new(".webp", EncodedImageFormat.Webp),
            new(".pkm", EncodedImageFormat.Pkm),
            new(".ktx", EncodedImageFormat.Ktx),
            new(".astc", EncodedImageFormat.Astc),
            new(".dng", EncodedImageFormat.Dng),
            new(".heif", EncodedImageFormat.Heif)
        ]).ToFrozenDictionary();

    public SequentialImageExportController(string outputFile) : base(outputFile)
    {
        VideoSettings = new SequentialImageExportSettings();
        AudioSettings = new SequentialImageExportAudioSettings();
    }

    public override async ValueTask Encode(
        IFrameProvider frameProvider, ISampleProvider sampleProvider,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(OutputFile);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The directory '{directory}' does not exist.");
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(OutputFile);
        var extension = Path.GetExtension(OutputFile);
        if (string.IsNullOrEmpty(fileNameWithoutExtension) || string.IsNullOrEmpty(extension))
            throw new ArgumentException("Output file must have a valid name and extension.");

        var digits = (int)Math.Log10(frameProvider.FrameCount) + 1;

        for (long i = 0; i < frameProvider.FrameCount; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var fileName = $"{fileNameWithoutExtension}_{i.ToString().PadLeft(digits, '0')}{extension}";
            var filePath = Path.Combine(directory, fileName);

            using var frame = await frameProvider.ReadNextFrame().ConfigureAwait(false)
                ?? throw new InvalidOperationException("The frame provider ended before its frame count.");
            await Task.Run(() => Save(frame, filePath, extension), cancellationToken);
        }

        if (!cancellationToken.IsCancellationRequested
            && Path.GetFileName(directory).StartsWith(StagingDirectoryPrefix, StringComparison.Ordinal))
        {
            PublishFromStaging(directory);
        }
    }

    // Beutl's export passes a path inside a staging directory beside the destination. After Encode it moves only
    // OutputFile out and deletes the directory with everything else, so the frames have to leave here, and a copy of
    // the first frame stands in for OutputFile.
    private void PublishFromStaging(string stagingDirectory)
    {
        var frames = Directory.GetFiles(stagingDirectory);
        if (frames.Min(StringComparer.Ordinal) is not { } firstFrame) return;

        File.Copy(firstFrame, OutputFile);

        var destination = Path.GetDirectoryName(stagingDirectory)!;
        foreach (var frame in frames)
        {
            File.Move(frame, Path.Combine(destination, Path.GetFileName(frame)), overwrite: true);
        }
    }

    private void Save(Bitmap bitmap, string filePath, string extension)
    {
        using var converted = bitmap.Convert(BitmapColorType.Bgra8888, BitmapAlphaType.Premul, BitmapColorSpace.Srgb);
        using var skPixmap = new SKPixmap(
            new(converted.Width, converted.Height, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()),
            converted.Data);

        var data = extension switch
        {
            ".png" => skPixmap.Encode(new SKPngEncoderOptions(
                VideoSettings.PngFilterFlags.Value.ToSkiaFlags(),
                VideoSettings.PngzLibLevel.Value)),
            ".jpg" or ".jpeg" => skPixmap.Encode(new SKJpegEncoderOptions(
                (int)VideoSettings.Quality.Value,
                VideoSettings.JpegDownsample.Value,
                VideoSettings.JpegAlphaOption.Value)),
            ".webp" => skPixmap.Encode(new SKWebpEncoderOptions(
                VideoSettings.WebpCompression.Value,
                VideoSettings.Quality.Value)),
            _ => skPixmap.Encode(
                (SKEncodedImageFormat)ExtensionToFormat[extension],
                (int)VideoSettings.Quality.Value)
        };

        if (data == null)
        {
            throw new InvalidOperationException("Failed to encode image.");
        }

        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    public override SequentialImageExportSettings VideoSettings { get; }

    public override AudioEncoderSettings AudioSettings { get; }

    public override bool IsAudioEnabled => false;
}
