using System;

namespace Anviz.SDK.Users
{
    public sealed class DeviceFaceTemplateFormat
    {
        public static DeviceFaceTemplateFormat Default { get; } = new DeviceFaceTemplateFormat(15360, true);
        public static DeviceFaceTemplateFormat FacePass7 { get; } = new DeviceFaceTemplateFormat(15360, true);
        public static DeviceFaceTemplateFormat FaceDeep5 { get; } = new DeviceFaceTemplateFormat(15360, true);

        public int TemplateSize { get; }
        public bool RequiresPadding { get; }

        public DeviceFaceTemplateFormat(int templateSize, bool requiresPadding)
        {
            if (templateSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(templateSize), "Template size must be positive.");
            }

            TemplateSize = templateSize;
            RequiresPadding = requiresPadding;
        }
    }
}

