using Anviz.SDK;
using Anviz.SDK.Responses;
using Anviz.SDK.Users;
using Anviz.SDK.Utils;
using Sample.Scenarios;
using System.Collections.Concurrent;

namespace Sample;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var manager = new AnvizManager
        {
            ConnectionUser = Environment.GetEnvironmentVariable("ANVIZ_CONNECTION_USER") ?? "admin",
            ConnectionPassword = Environment.GetEnvironmentVariable("ANVIZ_CONNECTION_PASSWORD") ?? "12345",
            AuthenticateConnection = true
        };

        if (args.Length > 0 && string.Equals(args[0], "server", StringComparison.OrdinalIgnoreCase))
        {
            await RunServerModeAsync(manager).ConfigureAwait(false);
        }
        else
        {
            await RunClientModeAsync(manager).ConfigureAwait(false);
        }
    }

    private static async Task RunClientModeAsync(AnvizManager manager)
    {
        var host = Environment.GetEnvironmentVariable("ANVIZ_DEVICE_HOST") ?? "10.0.0.1";
        var port = TryParse(Environment.GetEnvironmentVariable("ANVIZ_DEVICE_PORT"), 5010);
        var userId = (ulong)TryParse(Environment.GetEnvironmentVariable("ANVIZ_USER_ID"), 1001);
        var userName = Environment.GetEnvironmentVariable("ANVIZ_USER_NAME") ?? "SDK User";
        var pin = Environment.GetEnvironmentVariable("ANVIZ_USER_PIN");
        var card = Environment.GetEnvironmentVariable("ANVIZ_USER_CARD");

        var clientOptions = new ClientModeEnrollmentOptions
        {
            Host = host,
            Port = port,
            UserFactory = () => new UserInfo(userId, userName)
            {
                Password = pin,
                Card = card
            },
            FingerSlot = Finger.RightIndex,
            FingerprintVerificationCount = 2,
            FaceTemplateProvider = async (device, token) =>
            {
                var templatePath = Environment.GetEnvironmentVariable("ANVIZ_FACE_TEMPLATE_PATH");
                if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                {
                    return null;
                }

                var bytes = await File.ReadAllBytesAsync(templatePath, token).ConfigureAwait(false);
                return new FaceTemplate(bytes);
            },
            AfterEnrollment = async (device, profile, token) =>
            {
                var records = await device.DownloadRecords(true).ConfigureAwait(false);
                foreach (var record in records)
                {
                    Console.WriteLine($"[CLIENT] Downloaded record for user {record.UserCode} at {record.DateTime:yyyy-MM-dd HH:mm:ss}.");
                }

                if (records.Count > 0)
                {
                    await device.ClearNewRecords().ConfigureAwait(false);
                }
            },
            Log = message => Console.WriteLine($"[CLIENT {DateTime.Now:HH:mm:ss}] {message}")
        };

        var scenario = new ClientModeEnrollmentScenario(manager, clientOptions);
        await scenario.RunAsync().ConfigureAwait(false);
    }

    private static async Task RunServerModeAsync(AnvizManager manager)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var recordQueue = new ConcurrentQueue<Record>();

        var serverOptions = new ServerModeOptions
        {
            Port = TryParse(Environment.GetEnvironmentVariable("ANVIZ_SERVER_PORT"), 5010),
            Log = message => Console.WriteLine($"[SERVER {DateTime.Now:HH:mm:ss}] {message}"),
            OnDeviceConnected = async (device, token) =>
            {
                await device.SetDateTime(DateTime.Now).ConfigureAwait(false);
                await device.SetEmployeesData(new UserInfo(9999, "Visitor") { Password = null, Card = null }).ConfigureAwait(false);
            },
            OnRecordReceived = async (record, token) =>
            {
                recordQueue.Enqueue(record);
                await Task.CompletedTask;
            }
        };

        var scenario = new ServerModeRealtimeScenario(manager, serverOptions);

        var processor = Task.Run(async () =>
        {
            while (!cancellation.Token.IsCancellationRequested)
            {
                while (recordQueue.TryDequeue(out var record))
                {
                    Console.WriteLine($"[ACTION {DateTime.Now:HH:mm:ss}] Dispatching unlock command for {record.UserCode}.");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(false);
            }
        }, cancellation.Token);

        try
        {
            await scenario.RunAsync(cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await processor.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down.
            }
        }
    }

    private static int TryParse(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
