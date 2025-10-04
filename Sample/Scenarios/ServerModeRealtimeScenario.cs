using Anviz.SDK;
using Anviz.SDK.Responses;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Scenarios;

internal sealed class ServerModeOptions
{
    public int Port { get; init; } = 5010;
    public TimeSpan AcceptPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);
    public Action<string>? Log { get; init; }
    public Func<AnvizDevice, CancellationToken, Task>? OnDeviceConnected { get; init; }
    public Func<Record, CancellationToken, Task>? OnRecordReceived { get; init; }
}

internal sealed class ServerModeRealtimeScenario
{
    private readonly AnvizManager _manager;
    private readonly ServerModeOptions _options;

    public ServerModeRealtimeScenario(AnvizManager manager, ServerModeOptions options)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _manager.Listen(_options.Port);
        _options.Log?.Invoke($"Listening for device connections on port {_options.Port} (server mode)...");

        using var stopRegistration = cancellationToken.Register(_manager.StopServer);
        var activeSessions = new List<Task>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_manager.Pending())
                {
                    await Task.Delay(_options.AcceptPollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var device = await _manager.Accept().ConfigureAwait(false);
                activeSessions.Add(HandleDeviceAsync(device, cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _options.Log?.Invoke("Server cancellation requested. Stopping listener...");
        }
        finally
        {
            _manager.StopServer();
            if (activeSessions.Count > 0)
            {
                await Task.WhenAll(activeSessions).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleDeviceAsync(AnvizDevice device, CancellationToken cancellationToken)
    {
        _options.Log?.Invoke("Accepted device connection.");

        using (device)
        {
            var sessionCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscriptions = ScenarioUtilities.AttachLogging(device, _options.Log, _options.OnRecordReceived, cancellationToken, sessionCompletion);

            try
            {
                var description = await ScenarioUtilities.DescribeDeviceAsync(device).ConfigureAwait(false);
                _options.Log?.Invoke($"Session initialised for {description}");

                await ScenarioUtilities.EnsureRealtimeModeAsync(device, cancellationToken, _options.Log).ConfigureAwait(false);

                if (_options.OnDeviceConnected != null)
                {
                    await _options.OnDeviceConnected(device, cancellationToken).ConfigureAwait(false);
                }

                await sessionCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _options.Log?.Invoke("Ending device session due to cancellation.");
            }
            catch (Exception ex)
            {
                _options.Log?.Invoke($"Device session faulted: {ex.Message}");
            }
        }

        _options.Log?.Invoke("Device session closed.");
    }
}
