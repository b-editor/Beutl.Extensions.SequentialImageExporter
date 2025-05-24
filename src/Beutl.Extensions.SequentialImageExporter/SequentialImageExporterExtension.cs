using Beutl.Extensibility;
using Beutl.Graphics;

namespace Beutl.Extensions.SequentialImageExporter;

[Export]
public class SequentialImageExporterExtension : ControllableEncodingExtension
{
    public override string Name => "SequentialImageExporterExtension";
    public override string DisplayName => "連番画像エクスポーター";

    public override IEnumerable<string> SupportExtensions()
    {
        yield return ".png";
        yield return ".jpg";
        yield return ".jpeg";
        yield return ".bmp";
        yield return ".gif";
        yield return ".ico";
        yield return ".wbmp";
        yield return ".webp";
        yield return ".pkm";
        yield return ".ktx";
        yield return ".astc";
        yield return ".dng";
        yield return ".heif";
    }

    public override EncodingController CreateController(string file)
    {
        return new SequentialImageExportController(file);
    }
}