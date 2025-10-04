using Anviz.SDK.Utils;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Anviz.SDK.Users
{
    public sealed class FaceTemplate
    {
        public byte Index { get; }
        public byte[] Data { get; }
        public string ContentHash { get; }

        public FaceTemplate(byte[] template) : this(1, template)
        {
        }

        public FaceTemplate(byte index, byte[] template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            Index = index;
            Data = TrimTrailingZeros(template);
            ContentHash = ComputeHash(Data);
        }

        public bool IsEmpty => Data.Length == 0;

        public FaceTemplate Clone()
        {
            return new FaceTemplate(Index, Data.ToArray());
        }

        public bool ContentEquals(FaceTemplate other)
        {
            if (other == null)
            {
                return false;
            }

            return Index == other.Index && string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal);
        }

        public byte[] ToDevicePayload(ulong employeeId, DeviceFaceTemplateFormat format)
        {
            var descriptor = format ?? DeviceFaceTemplateFormat.Default;
            if (Data.Length > descriptor.TemplateSize)
            {
                throw new ArgumentOutOfRangeException(nameof(format), "Template size exceeds device capacity.");
            }

            var payload = new byte[descriptor.TemplateSize + 6];
            Bytes.Write(5, employeeId).CopyTo(payload, 0);
            payload[5] = Index;
            Buffer.BlockCopy(Data, 0, payload, 6, Data.Length);
            if (!descriptor.RequiresPadding && Data.Length < descriptor.TemplateSize)
            {
                Array.Resize(ref payload, Data.Length + 6);
            }
            return payload;
        }

        public static FaceTemplate FromDevicePayload(byte[] payload)
        {
            if (payload == null || payload.Length < 6)
            {
                return new FaceTemplate(Array.Empty<byte>());
            }

            var templateLength = payload.Length - 6;
            if (templateLength <= 0)
            {
                return new FaceTemplate(payload[5], Array.Empty<byte>());
            }

            var template = new byte[templateLength];
            Buffer.BlockCopy(payload, 6, template, 0, templateLength);
            return new FaceTemplate(payload[5], template);
        }

        private static byte[] TrimTrailingZeros(byte[] template)
        {
            var length = template.Length;
            while (length > 0 && template[length - 1] == 0)
            {
                length--;
            }

            if (length == template.Length)
            {
                return template.ToArray();
            }

            var trimmed = new byte[length];
            Buffer.BlockCopy(template, 0, trimmed, 0, length);
            return trimmed;
        }

        private static string ComputeHash(byte[] data)
        {
            if (data.Length == 0)
            {
                return string.Empty;
            }

            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(data));
            }
        }
    }
}

