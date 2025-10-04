using Anviz.SDK;
using Anviz.SDK.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Scenarios;

internal static class ScenarioUtilities
{
    public static async Task<string> DescribeDeviceAsync(AnvizDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var deviceId = await device.GetDeviceID().ConfigureAwait(false);
        var serialNumber = await device.GetDeviceSN().ConfigureAwait(false);
        var deviceType = await device.GetDeviceTypeCode().ConfigureAwait(false);
        var biometricType = await device.GetDeviceBiometricType().ConfigureAwait(false);

        return $"ID {deviceId} / SN {serialNumber} / Type {deviceType} / Biometric {biometricType}";
    }

    public static async Task EnsureRealtimeModeAsync(AnvizDevice device, CancellationToken cancellationToken, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        var advancedSettings = await device.GetAdvancedSettings().ConfigureAwait(false);
        if (advancedSettings.RealTimeMode)
        {
            return;
        }

        advancedSettings.RealTimeMode = true;
        await device.SetAdvancedSettings(advancedSettings).ConfigureAwait(false);
        log?.Invoke("Enabled real-time push mode on device.");
    }

    public static IDisposable AttachLogging(
        AnvizDevice device,
        Action<string>? log,
        Func<Record, CancellationToken, Task>? onRecord,
        CancellationToken cancellationToken,
        TaskCompletionSource<bool>? sessionCompletion = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        EventHandler? pingHandler = null;
        EventHandler<Record>? recordHandler = null;
        EventHandler<Response>? packetHandler = null;
        EventHandler<Exception>? errorHandler = null;

        if (log != null)
        {
            pingHandler = (_, _) => log("Heartbeat (ping) received from device.");
            packetHandler = (_, response) => log($"Async packet 0x{response.ResponseCode:X2} ({response.DATA.Length} bytes).");
        }

        if (onRecord != null || log != null)
        {
            recordHandler = (_, record) =>
            {
                log?.Invoke($"Real-time record: user {record.UserCode} at {record.DateTime:yyyy-MM-dd HH:mm:ss}.");

                if (onRecord != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await onRecord(record, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            log?.Invoke("Record handler cancelled.");
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"Record handler failed: {ex.Message}");
                        }
                    }, CancellationToken.None);
                }
            };
        }

        errorHandler = (_, exception) =>
        {
            log?.Invoke($"Device error signalled: {exception.Message}");
            sessionCompletion?.TrySetResult(false);
        };

        if (pingHandler != null)
        {
            device.DevicePing += pingHandler;
        }

        if (packetHandler != null)
        {
            device.ReceivedPacket += packetHandler;
        }

        if (recordHandler != null)
        {
            device.ReceivedRecord += recordHandler;
        }

        device.DeviceError += errorHandler;

        return new DelegateDisposable(() =>
        {
            if (pingHandler != null)
            {
                device.DevicePing -= pingHandler;
            }

            if (packetHandler != null)
            {
                device.ReceivedPacket -= packetHandler;
            }

            if (recordHandler != null)
            {
                device.ReceivedRecord -= recordHandler;
            }

            if (errorHandler != null)
            {
                device.DeviceError -= errorHandler;
            }
        });
    }

    private sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
