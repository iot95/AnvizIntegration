using Anviz.SDK;
using Anviz.SDK.Responses;
using Anviz.SDK.Users;
using Anviz.SDK.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Scenarios;

internal sealed class ClientModeEnrollmentOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 5010;
    public required Func<UserInfo> UserFactory { get; init; }
    public bool EnrollFingerprint { get; init; } = true;
    public Finger FingerSlot { get; init; } = Finger.RightIndex;
    public int FingerprintVerificationCount { get; init; } = 2;
    public Func<AnvizDevice, CancellationToken, Task<FaceTemplate?>>? FaceTemplateProvider { get; init; }
    public Func<AnvizDevice, UserInfo, CancellationToken, Task>? AfterEnrollment { get; init; }
    public Action<string>? Log { get; init; }
}

internal sealed class ClientModeEnrollmentScenario
{
    private readonly AnvizManager _manager;
    private readonly ClientModeEnrollmentOptions _options;

    public ClientModeEnrollmentScenario(AnvizManager manager, ClientModeEnrollmentOptions options)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _options.Log?.Invoke($"Connecting to {_options.Host}:{_options.Port} (client mode)...");
        using var device = await _manager.Connect(_options.Host, _options.Port).ConfigureAwait(false);

        var sessionCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscriptions = ScenarioUtilities.AttachLogging(device, _options.Log, null, cancellationToken, sessionCompletion);

        var description = await ScenarioUtilities.DescribeDeviceAsync(device).ConfigureAwait(false);
        _options.Log?.Invoke($"Connected to {description}");

        await ScenarioUtilities.EnsureRealtimeModeAsync(device, cancellationToken, _options.Log).ConfigureAwait(false);

        var userProfile = _options.UserFactory();
        _options.Log?.Invoke($"Uploading core user profile {userProfile.Id} – {userProfile.Name}...");
        await device.SetEmployeesData(userProfile).ConfigureAwait(false);

        if (false && _options.EnrollFingerprint)
        {
            _options.Log?.Invoke($"Enrolling fingerprint for user {userProfile.Id} (verify {_options.FingerprintVerificationCount} times)...");
            var fingerprintTemplate = await device.EnrollFingerprint(userProfile.Id, _options.FingerSlot, _options.FingerprintVerificationCount).ConfigureAwait(false);
            await device.SetFingerprintTemplate(userProfile.Id, _options.FingerSlot, fingerprintTemplate).ConfigureAwait(false);
            _options.Log?.Invoke($"Fingerprint stored in slot {_options.FingerSlot}.");
        }

        if (true)//_options.FaceTemplateProvider != null)
        {
            var biometricType = await device.GetDeviceBiometricType().ConfigureAwait(false);
            if (DeviceCapabilities.SupportsFaceTemplates(biometricType, device.DeviceTypeCode))
            {
                _options.Log?.Invoke($"Device supports face templates. Requesting data from provider...");
                var faceTemplate = await device.GetFaceTemplate(844);
                //var faceTemplate = await _options.FaceTemplateProvider(device, cancellationToken).ConfigureAwait(false);
                if (faceTemplate != null)
                {
                    await device.SetFaceTemplate(userProfile.Id, faceTemplate).ConfigureAwait(false);
                    _options.Log?.Invoke("Face template uploaded successfully.");
                }
                else
                {
                    _options.Log?.Invoke("Face template provider returned no data. Skipping face upload.");
                }
            }
            else
            {
                _options.Log?.Invoke("Device does not support face templates. Skipping face upload.");
            }
        }

        if (_options.AfterEnrollment != null)
        {
            _options.Log?.Invoke("Running post-enrollment hook...");
            await _options.AfterEnrollment(device, userProfile, cancellationToken).ConfigureAwait(false);
        }

        _options.Log?.Invoke("Client mode enrollment scenario completed.");
    }
}
