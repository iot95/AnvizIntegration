using Anviz.SDK.Commands;
using Anviz.SDK.Utils;
using System;
using System.Threading.Tasks;

namespace Anviz.SDK.Commands
{
    class SetFingerprintTemplateCommand : Command
    {
        private const byte SET_FPTEMPLATE = 0x45;
        public SetFingerprintTemplateCommand(ulong deviceId, ulong employeeID, byte slot, byte[] template) : base(deviceId)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var payload = new byte[template.Length + 6];
            Bytes.Write(5, employeeID).CopyTo(payload, 0);
            payload[5] = slot;
            template.CopyTo(payload, 6);
            BuildPayload(SET_FPTEMPLATE, payload);
        }
    }
}

namespace Anviz.SDK
{
    public partial class AnvizDevice
    {
        public Task SetFingerprintTemplate(ulong employeeID, Finger finger, byte[] template)
        {
            return SetBiometricTemplate(employeeID, (byte)(finger + 1), template);
        }

        public async Task SetBiometricTemplate(ulong employeeID, byte slot, byte[] template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            await DeviceStream.SendCommand(new SetFingerprintTemplateCommand(DeviceId, employeeID, slot, template)).ConfigureAwait(false);
        }
    }
}