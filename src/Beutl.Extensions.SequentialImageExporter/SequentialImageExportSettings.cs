using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Beutl.Media;
using Beutl.Media.Encoding;
using SkiaSharp;

namespace Beutl.Extensions.SequentialImageExporter;

public partial class SequentialImageExportSettings : VideoEncoderSettings
{
    public SequentialImageExportSettings()
    {
        ScanProperties<SequentialImageExportSettings>();
    }

    [Range(0, 9)]
    public IProperty<int> PngzLibLevel { get; } = Property.CreateValue(6);

    public IProperty<PngEncoderFilterFlags> PngFilterFlags { get; } = Property.CreateValue(new PngEncoderFilterFlags());

    [Range(0f, 100f)]
    public IProperty<float> Quality { get; } = Property.CreateValue(100f);

    public IProperty<SKJpegEncoderDownsample> JpegDownsample { get; } = Property.CreateValue(SKJpegEncoderDownsample.Downsample420);

    public IProperty<SKJpegEncoderAlphaOption> JpegAlphaOption { get; } = Property.CreateValue(SKJpegEncoderAlphaOption.Ignore);

    public IProperty<SKWebpEncoderCompression> WebpCompression { get; } = Property.CreateValue(SKWebpEncoderCompression.Lossy);

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<PixelSize> DestinationSize => base.DestinationSize;

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<int> Bitrate => base.Bitrate;

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<int> KeyframeRate => base.KeyframeRate;
}
