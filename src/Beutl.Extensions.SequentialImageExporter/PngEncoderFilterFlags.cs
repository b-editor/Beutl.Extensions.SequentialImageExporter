using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter;

public class PngEncoderFilterFlags : CoreObject
{
    public static readonly CoreProperty<bool> NoneProperty;
    public static readonly CoreProperty<bool> SubProperty;
    public static readonly CoreProperty<bool> UpProperty;
    public static readonly CoreProperty<bool> AvgProperty;
    public static readonly CoreProperty<bool> PaethProperty;
    private bool none = true;
    private bool sub = true;
    private bool up = true;
    private bool avg = true;
    private bool paeth = true;
    
    static PngEncoderFilterFlags()
    {
        NoneProperty = ConfigureProperty<bool, PngEncoderFilterFlags>(nameof(None))
            .Accessor(o => o.None, (o, v) => o.None = v)
            .DefaultValue(true)
            .Register();

        SubProperty = ConfigureProperty<bool, PngEncoderFilterFlags>(nameof(Sub))
            .Accessor(o => o.Sub, (o, v) => o.Sub = v)
            .DefaultValue(true)
            .Register();

        UpProperty = ConfigureProperty<bool, PngEncoderFilterFlags>(nameof(Up))
            .Accessor(o => o.Up, (o, v) => o.Up = v)
            .DefaultValue(true)
            .Register();

        AvgProperty = ConfigureProperty<bool, PngEncoderFilterFlags>(nameof(Avg))
            .Accessor(o => o.Avg, (o, v) => o.Avg = v)
            .DefaultValue(true)
            .Register();

        PaethProperty = ConfigureProperty<bool, PngEncoderFilterFlags>(nameof(Paeth))
            .Accessor(o => o.Paeth, (o, v) => o.Paeth = v)
            .DefaultValue(true)
            .Register();
    }
    
    public bool None
    {
        get => none;
        set => SetAndRaise(NoneProperty, ref none, value);
    }
    
    public bool Sub
    {
        get => sub;
        set => SetAndRaise(SubProperty, ref sub, value);
    }
    
    public bool Up
    {
        get => up;
        set => SetAndRaise(UpProperty, ref up, value);
    }
    
    public bool Avg
    {
        get => avg;
        set => SetAndRaise(AvgProperty, ref avg, value);
    }
    
    public bool Paeth
    {
        get => paeth;
        set => SetAndRaise(PaethProperty, ref paeth, value);
    }
    
    internal SKPngEncoderFilterFlags ToSkiaFlags()
    {
        var flags = SKPngEncoderFilterFlags.None;
        if (None) flags |= SKPngEncoderFilterFlags.None;
        if (Sub) flags |= SKPngEncoderFilterFlags.Sub;
        if (Up) flags |= SKPngEncoderFilterFlags.Up;
        if (Avg) flags |= SKPngEncoderFilterFlags.Avg;
        if (Paeth) flags |= SKPngEncoderFilterFlags.Paeth;
        return flags;
    }
}