using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter;

public partial class PngEncoderFilterFlags : CoreObject
{
    public PngEncoderFilterFlags()
    {
        ScanProperties<PngEncoderFilterFlags>();
    }

    public IProperty<bool> None { get; } = Property.CreateValue(true);

    public IProperty<bool> Sub { get; } = Property.CreateValue(true);

    public IProperty<bool> Up { get; } = Property.CreateValue(true);

    public IProperty<bool> Avg { get; } = Property.CreateValue(true);

    public IProperty<bool> Paeth { get; } = Property.CreateValue(true);

    internal SKPngEncoderFilterFlags ToSkiaFlags()
    {
        var flags = SKPngEncoderFilterFlags.None;
        if (None.Value) flags |= SKPngEncoderFilterFlags.None;
        if (Sub.Value) flags |= SKPngEncoderFilterFlags.Sub;
        if (Up.Value) flags |= SKPngEncoderFilterFlags.Up;
        if (Avg.Value) flags |= SKPngEncoderFilterFlags.Avg;
        if (Paeth.Value) flags |= SKPngEncoderFilterFlags.Paeth;
        return flags;
    }
}
