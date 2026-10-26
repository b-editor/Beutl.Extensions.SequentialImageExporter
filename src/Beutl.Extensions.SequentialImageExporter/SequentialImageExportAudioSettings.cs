using System.ComponentModel;
using Beutl.Media.Encoding;

namespace Beutl.Extensions.SequentialImageExporter;

public partial class SequentialImageExportAudioSettings : AudioEncoderSettings
{
    public SequentialImageExportAudioSettings()
    {
        ScanProperties<SequentialImageExportAudioSettings>();
    }

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<int> Bitrate => base.Bitrate;

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<int> SampleRate => base.SampleRate;

    [Browsable(false)]
    [NotAutoSerialized]
    public new IProperty<int> Channels => base.Channels;
}
