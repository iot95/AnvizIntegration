using Anviz.SDK.Users;
using System;
using System.Collections.Generic;

namespace Anviz.SDK.Utils
{
    public static class DeviceCapabilities
    {
        private static readonly Dictionary<string, DeviceFaceTemplateFormat> KnownFaceFormats = new Dictionary<string, DeviceFaceTemplateFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["FACE7"] = DeviceFaceTemplateFormat.FacePass7,
            ["FACEPASS7"] = DeviceFaceTemplateFormat.FacePass7,
            ["FACEDEEP5"] = DeviceFaceTemplateFormat.FaceDeep5
        };

        public static DeviceFaceTemplateFormat GetFaceTemplateFormat(string deviceTypeCode)
        {
            if (string.IsNullOrWhiteSpace(deviceTypeCode))
            {
                return DeviceFaceTemplateFormat.Default;
            }

            if (KnownFaceFormats.TryGetValue(deviceTypeCode.Trim(), out var format))
            {
                return format;
            }

            var normalized = deviceTypeCode.Trim().ToUpperInvariant();
            if (normalized.Contains("FACEDEEP"))
            {
                return DeviceFaceTemplateFormat.FaceDeep5;
            }

            if (normalized.Contains("FACE"))
            {
                return DeviceFaceTemplateFormat.FacePass7;
            }

            return DeviceFaceTemplateFormat.Default;
        }

        public static bool SupportsFaceTemplates(BiometricType biometricType, string deviceTypeCode = null)
        {
            if (biometricType == BiometricType.Face)
            {
                return true;
            }

            if (biometricType == BiometricType.Unknown && !string.IsNullOrWhiteSpace(deviceTypeCode))
            {
                var descriptor = GetFaceTemplateFormat(deviceTypeCode);
                return descriptor != DeviceFaceTemplateFormat.Default;
            }

            return false;
        }
    }
}

