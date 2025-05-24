using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Beutl.Media;
using Beutl.Media.Encoding;
using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter;

public class SequentialImageExportSettings : VideoEncoderSettings
{
    public static readonly CoreProperty<float> QualityProperty;
    public static readonly CoreProperty<int> PngzLibLevelProperty;
    public static readonly CoreProperty<PngEncoderFilterFlags> PngFilterFlagsProperty;
    public static readonly CoreProperty<SKJpegEncoderDownsample> JpegDownsampleProperty;
    public static readonly CoreProperty<SKJpegEncoderAlphaOption> JpegAlphaOptionProperty;
    public static readonly CoreProperty<SKWebpEncoderCompression> WebpCompressionProperty;
    private int pngzliblevel = 6;
    private PngEncoderFilterFlags pngFilterFlags = new();
    private float quality = 100f;
    private SKJpegEncoderDownsample downsample = SKJpegEncoderDownsample.Downsample420;
    private SKJpegEncoderAlphaOption jpegAlphaOption = SKJpegEncoderAlphaOption.Ignore;
    private SKWebpEncoderCompression compression = SKWebpEncoderCompression.Lossy;

    static SequentialImageExportSettings()
    {
        PngzLibLevelProperty =
            ConfigureProperty<int, SequentialImageExportSettings>(nameof(PngzLibLevel))
                .Accessor(o => o.PngzLibLevel, (o, v) => o.PngzLibLevel = v)
                .DefaultValue(6)
                .Register();
        
        PngFilterFlagsProperty =
            ConfigureProperty<PngEncoderFilterFlags, SequentialImageExportSettings>(nameof(PngFilterFlags))
                .Accessor(o => o.PngFilterFlags, (o, v) => o.PngFilterFlags = v)
                .DefaultValue(new PngEncoderFilterFlags())
                .Register();

        QualityProperty = ConfigureProperty<float, SequentialImageExportSettings>(nameof(Quality))
            .Accessor(o => o.Quality, (o, v) => o.Quality = v)
            .DefaultValue(100f)
            .Register();
        
        JpegDownsampleProperty =
            ConfigureProperty<SKJpegEncoderDownsample, SequentialImageExportSettings>(nameof(JpegDownsample))
                .Accessor(o => o.JpegDownsample, (o, v) => o.JpegDownsample = v)
                .DefaultValue(SKJpegEncoderDownsample.Downsample420)
                .Register();

        JpegAlphaOptionProperty =
            ConfigureProperty<SKJpegEncoderAlphaOption, SequentialImageExportSettings>(nameof(JpegAlphaOption))
                .Accessor(o => o.JpegAlphaOption, (o, v) => o.JpegAlphaOption = v)
                .DefaultValue(SKJpegEncoderAlphaOption.Ignore)
                .Register();

        WebpCompressionProperty =
            ConfigureProperty<SKWebpEncoderCompression, SequentialImageExportSettings>(nameof(WebpCompression))
                .Accessor(o => o.WebpCompression, (o, v) => o.WebpCompression = v)
                .DefaultValue(SKWebpEncoderCompression.Lossy)
                .Register();

        DestinationSizeProperty.OverrideMetadata<SequentialImageExportSettings>(
            new CorePropertyMetadata<PixelSize>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
        BitrateProperty.OverrideMetadata<SequentialImageExportSettings>(
            new CorePropertyMetadata<int>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
        KeyframeRateProperty.OverrideMetadata<SequentialImageExportSettings>(
            new CorePropertyMetadata<int>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
    }
    
    [Range(0, 9)]
    public int PngzLibLevel
    {
        get => pngzliblevel;
        set => SetAndRaise(PngzLibLevelProperty, ref pngzliblevel, value);
    }
    
    public PngEncoderFilterFlags PngFilterFlags
    {
        get => pngFilterFlags;
        set => SetAndRaise(PngFilterFlagsProperty, ref pngFilterFlags, value);
    }

    [Range(0f, 100f)]
    public float Quality
    {
        get => quality;
        set => SetAndRaise(QualityProperty, ref quality, value);
    }

    public SKJpegEncoderDownsample JpegDownsample
    {
        get => downsample;
        set => SetAndRaise(JpegDownsampleProperty, ref downsample, value);
    }

    public SKJpegEncoderAlphaOption JpegAlphaOption
    {
        get => jpegAlphaOption;
        set => SetAndRaise(JpegAlphaOptionProperty, ref jpegAlphaOption, value);
    }

    public SKWebpEncoderCompression WebpCompression
    {
        get => compression;
        set => SetAndRaise(WebpCompressionProperty, ref compression, value);
    }
}