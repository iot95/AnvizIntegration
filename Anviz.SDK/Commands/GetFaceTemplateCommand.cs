using Anviz.SDK.Commands;
using Anviz.SDK.Users;
using Anviz.SDK.Utils;
using System.Threading.Tasks;

namespace Anviz.SDK.Commands
{
    class GetFaceTemplateCommand : Command
    {
        private const byte GET_FACETEMPLATE = 0x44;
        public GetFaceTemplateCommand(ulong deviceId, ulong employeeID, byte slot) : base(deviceId)
        {
            var payload = new byte[6];
            Bytes.Write(5, employeeID).CopyTo(payload, 0);
            payload[5] = slot;
            BuildPayload(GET_FACETEMPLATE, payload);
        }
    }
}

namespace Anviz.SDK
{
    public partial class AnvizDevice
    {
        public async Task<byte[]> GetFaceTemplate(ulong employeeID)
        {
            var descriptor = await EnsureFaceTemplateFormatAsync().ConfigureAwait(false);
            var response = await DeviceStream.SendCommand(new GetFaceTemplateCommand(DeviceId, employeeID, descriptor.SlotIndex)).ConfigureAwait(false);
            return response.DATA;
        }

        public async Task<FaceTemplate> GetFaceTemplateInfo(ulong employeeID)
        {
            var descriptor = await EnsureFaceTemplateFormatAsync().ConfigureAwait(false);
            var response = await DeviceStream.SendCommand(new GetFaceTemplateCommand(DeviceId, employeeID, descriptor.SlotIndex)).ConfigureAwait(false);
            return FaceTemplate.FromDevicePayload(response.DATA);
        }

        private async Task<DeviceFaceTemplateFormat> EnsureFaceTemplateFormatAsync()
        {
            if (string.IsNullOrWhiteSpace(DeviceTypeCode))
            {
                await GetDeviceTypeCode().ConfigureAwait(false);
            }

            return DeviceCapabilities.GetFaceTemplateFormat(DeviceTypeCode);
        }
    }
}
