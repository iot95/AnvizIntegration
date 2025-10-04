using Anviz.SDK.Commands;
using Anviz.SDK.Users;
using Anviz.SDK.Utils;
using System;
using System.Threading.Tasks;

namespace Anviz.SDK.Commands
{
    class SetFaceTemplateCommand : Command
    {
        private const byte SET_FACETEMPLATE = 0x45;
        public SetFaceTemplateCommand(ulong deviceId, ulong employeeID, FaceTemplate template, DeviceFaceTemplateFormat format) : base(deviceId)
        {
            var descriptor = format ?? DeviceFaceTemplateFormat.Default;
            var payload = template.ToDevicePayload(employeeID, descriptor);
            BuildPayload(SET_FACETEMPLATE, payload);
        }
    }
}

namespace Anviz.SDK
{
    public partial class AnvizDevice
    {
        public async Task SetFaceTemplate(ulong employeeID, FaceTemplate template, DeviceFaceTemplateFormat format = null)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var descriptor = format ?? DeviceCapabilities.GetFaceTemplateFormat(DeviceTypeCode);
            await DeviceStream.SendCommand(new SetFaceTemplateCommand(DeviceId, employeeID, template, descriptor));
        }

        public async Task SetFaceTemplate(ulong employeeID, byte[] template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            await SetFaceTemplate(employeeID, new FaceTemplate(template));
        }
    }
}