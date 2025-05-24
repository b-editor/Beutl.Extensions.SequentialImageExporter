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

        for (var i = 0; i < frameProvider.FrameCount; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var fileName = $"{fileNameWithoutExtension}_{i.ToString().PadLeft(digits, '0')}{extension}";
            var filePath = Path.Combine(directory, fileName);

            using var frame = await frameProvider.RenderFrame(i).ConfigureAwait(false);
            await Task.Run(() => Save(frame, filePath, extension), cancellationToken);
        }
    }

    private void Save(Bitmap<Bgra8888> bitmap, string filePath, string extension)
    {
        using var skPixmap = new SKPixmap(
            new(bitmap.Width, bitmap.Height, SKColorType.Bgra8888),
            bitmap.Data);

        var data = extension switch
        {
            ".png" => skPixmap.Encode(new SKPngEncoderOptions(
                VideoSettings.PngFilterFlags.ToSkiaFlags(),
                VideoSettings.PngzLibLevel)),
            ".jpg" or ".jpeg" => skPixmap.Encode(new SKJpegEncoderOptions(
                (int)VideoSettings.Quality,
                VideoSettings.JpegDownsample,
                VideoSettings.JpegAlphaOption)),
            ".webp" => skPixmap.Encode(new SKWebpEncoderOptions(
                VideoSettings.WebpCompression,
                VideoSettings.Quality)),
            _ => skPixmap.Encode(
                (SKEncodedImageFormat)ExtensionToFormat[extension],
                (int)VideoSettings.Quality)
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
}