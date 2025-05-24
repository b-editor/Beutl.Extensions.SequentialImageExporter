using System.ComponentModel;
using Beutl.Media.Encoding;

namespace Beutl.Extensions.SequentialImageExporter;

public class SequentialImageExportAudioSettings : AudioEncoderSettings
{
    static SequentialImageExportAudioSettings()
    {
        BitrateProperty.OverrideMetadata<SequentialImageExportAudioSettings>(
            new CorePropertyMetadata<int>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
        SampleRateProperty.OverrideMetadata<SequentialImageExportAudioSettings>(
            new CorePropertyMetadata<int>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
        ChannelsProperty.OverrideMetadata<SequentialImageExportAudioSettings>(
            new CorePropertyMetadata<int>(shouldSerialize: false, attributes: new BrowsableAttribute(false)));
    }
}