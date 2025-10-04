# FaceDeep 5 Face Enrollment Investigation

## Background
The FaceDeep 5 reports its biometric type as `Unknown` through the current .NET SDK, so `UserSynchronizationService` refuses to
push face templates even though the hardware is a face terminal.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L98-L156】【F:Anviz.SDK/AnvizDevice.cs†L11-L19】
While testing, invoking `EnrollFingerprint` on the device unexpectedly opens the face capture workflow on the terminal, which
suggests the "fingerprint" command can target face slots as well.【F:Anviz.SDK/Commands/EnrollFingerprintCommand.cs†L10-L38】

## What the native SDK exposes
`crosschex.h` defines the enrolment slot (`backup`) that `CCHex_AddFingerprintOnline` operates on, and it explicitly documents
that slot values `0-9` represent fingerprints while `10` is the face slot (followed by two iris slots).【F:Docs/crosschex.h†L780-L792】【F:Docs/crosschex.h†L520-L559】
This means the same command that the .NET SDK names `EnrollFingerprint` is also the documented path for face capture when
`backup == 10`.

The header also lists multiple fingerprint template sizes (338, 1200, 2048, 6144, 10240, 15360) that different terminals expect
when templates are uploaded or downloaded.【F:Docs/crosschex.h†L40-L42】

## Behaviour of the current .NET SDK
The managed `EnrollFingerprintCommand` hard-codes `payload[5] = 1`, so it always requests slot `1` regardless of which biometric
is required.【F:Anviz.SDK/Commands/EnrollFingerprintCommand.cs†L13-L18】 After the enrolment loop finishes, the SDK reads the
captured template from slot `0`, which is correct only when you intended to capture the first fingerprint.【F:Anviz.SDK/Commands/EnrollFingerprintCommand.cs†L24-L39】
`Finger` indices are also limited to the ten fingers, so there is no way to point the command at the face slot (10) or iris slots
(11 and 12).【F:Anviz.SDK/Utils/Fingers.cs†L40-L75】

Template transport has similar limitations. Both `GetFingerprintTemplate` and `SetFingerprintTemplate` add `1` to the supplied
finger enum before putting it on the wire, so the only values that can be addressed are `1-10`, and the managed API never exposes
the constants that would let a caller pick slot `10` for face.【F:Anviz.SDK/Commands/GetFingerprintTemplateCommand.cs†L10-L27】【F:Anviz.SDK/Commands/SetFingerprintTemplateCommand.cs†L10-L27】
The upload path also fixes the payload size to 344 bytes (6 bytes of metadata + 338 bytes of template data), which matches the
legacy 338-byte format but not the larger template lengths listed in the native header.【F:Docs/crosschex.h†L40-L42】【F:Anviz.SDK/Commands/SetFingerprintTemplateCommand.cs†L10-L16】

Finally, capability discovery relies solely on the decoded `BiometricType`. The decoder recognises only a handful of device type
strings and returns `Unknown` for anything else, even if the type code still contains the word "FACE".【F:Anviz.SDK/Utils/BiometricType.cs†L13-L48】
`DeviceCapabilities.SupportsFaceTemplates` then short-circuits face synchronisation for every `Unknown` device, so a FaceDeep 5
never receives face uploads even though `DeviceCapabilities` already has heuristics that would recognise its type string.【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L7-L45】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L98-L156】

## Conclusions
* The protocol explicitly allows slot `10` to drive the face enrolment workflow. The managed SDK needs an API surface that lets
  callers supply the desired biometric slot instead of hard-coding slot `1`.
* Template download/upload helpers should be able to address slot `10` (and possibly 11/12) and cope with template buffers larger
  than 338 bytes to stay compatible with the devices listed in the native header.
* Biometric capability detection should fall back to parsing the device type code (e.g. check for `FACE` / `FACEDEEP`) so that
  face synchronisation is not skipped whenever the native code string differs from the hard-coded list in `BiometricTypes`.

Together these gaps explain why the FaceDeep 5 launches its face enrolment UI when `EnrollFingerprint` is invoked yet the SDK
cannot persist the captured template or treat the unit as a face terminal. Addressing the slot handling and capability detection
would restore feature parity with the documented native API and improve compatibility with other Anviz devices.

## Remediation
The SDK now exposes slot-aware helpers so the same enrolment path can target face, fingerprint, or iris slots. `EnrollFingerprint`
accepts a `Finger` argument and delegates to the new `EnrollTemplateAsync` helper, while the download/upload paths take raw slot
indices and honour arbitrary template lengths.【F:Anviz.SDK/Commands/EnrollFingerprintCommand.cs†L23-L53】【F:Anviz.SDK/Commands/GetFingerprintTemplateCommand.cs†L10-L34】【F:Anviz.SDK/Commands/SetFingerprintTemplateCommand.cs†L10-L42】
Face detection no longer depends solely on the decoded `BiometricType`; the SDK falls back to the resolved face template format so
FaceDeep models correctly advertise face support even when the native type code is unfamiliar.【F:Anviz.SDK/Commands/GetDeviceTypeCommand.cs†L24-L40】【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L25-L53】
Face template APIs now resolve the device’s slot index before issuing commands, ensuring uploads and downloads target the documented
face slot (10) instead of the previously hard-coded index `1`.【F:Anviz.SDK/Commands/GetFaceTemplateCommand.cs†L10-L45】【F:Anviz.SDK/Commands/SetFaceTemplateCommand.cs†L23-L53】【F:Anviz.SDK/Users/DeviceFaceTemplateFormat.cs†L5-L23】
