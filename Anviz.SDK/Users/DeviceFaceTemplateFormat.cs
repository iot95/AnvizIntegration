using System;

namespace Anviz.SDK.Users
{
    public sealed class DeviceFaceTemplateFormat
    {
        public static DeviceFaceTemplateFormat Default { get; } = new DeviceFaceTemplateFormat(15360, true, 10);
        public static DeviceFaceTemplateFormat FacePass7 { get; } = new DeviceFaceTemplateFormat(15360, true, 10);
        public static DeviceFaceTemplateFormat FaceDeep5 { get; } = new DeviceFaceTemplateFormat(15360, true, 10);

        public int TemplateSize { get; }
        public bool RequiresPadding { get; }
        public byte SlotIndex { get; }

        public DeviceFaceTemplateFormat(int templateSize, bool requiresPadding, byte slotIndex)
        {
            if (templateSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(templateSize), "Template size must be positive.");
            }

            TemplateSize = templateSize;
            RequiresPadding = requiresPadding;
            SlotIndex = slotIndex;
        }
    }
}

