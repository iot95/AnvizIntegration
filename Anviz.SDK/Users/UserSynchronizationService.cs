using Anviz.SDK.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Anviz.SDK.Users
{
    public class UserSynchronizationService
    {
        private readonly UserSynchronizationOptions options;

        public UserSynchronizationService(UserSynchronizationOptions options = null)
        {
            this.options = options ?? new UserSynchronizationOptions();
            if (this.options.MaxRetryAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxRetryAttempts), "MaxRetryAttempts must be greater than zero.");
            }
        }

        public async Task SynchronizeAsync(IEnumerable<AnvizDevice> devices, IEnumerable<UserProfile> centralUsers, CancellationToken cancellationToken = default)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            if (centralUsers == null)
            {
                throw new ArgumentNullException(nameof(centralUsers));
            }

            var centralDictionary = centralUsers.ToDictionary(u => u.User.Id);
            var synchronizationTasks = devices.Select(device => SynchronizeDeviceAsync(device, centralDictionary, cancellationToken));
            await Task.WhenAll(synchronizationTasks);
        }

        public async Task UpsertUserAsync(IEnumerable<AnvizDevice> devices, UserProfile user, CancellationToken cancellationToken = default)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var tasks = devices.Select(device => SynchronizeSingleUserAsync(device, user, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public async Task DeleteUserAsync(IEnumerable<AnvizDevice> devices, ulong userId, CancellationToken cancellationToken = default)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            var tasks = devices.Select(device => DeleteUserOnDeviceAsync(device, userId, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private async Task SynchronizeDeviceAsync(AnvizDevice device, IReadOnlyDictionary<ulong, UserProfile> centralUsers, CancellationToken cancellationToken)
        {
            await EnsureDeviceMetadataAsync(device);

            var deviceUsers = await device.GetEmployeesData();
            var deviceDictionary = deviceUsers.ToDictionary(u => u.Id);

            var deviceIds = new HashSet<ulong>(deviceDictionary.Keys);
            foreach (var deviceId in deviceIds)
            {
                if (!centralUsers.ContainsKey(deviceId))
                {
                    await DeleteUserOnDeviceAsync(device, deviceId, cancellationToken);
                    deviceDictionary.Remove(deviceId);
                }
            }

            var upsertProfiles = new List<UserProfile>();
            foreach (var centralUser in centralUsers.Values)
            {
                if (!deviceDictionary.TryGetValue(centralUser.User.Id, out var deviceUser) || !deviceUser.EquivalentTo(centralUser.User))
                {
                    upsertProfiles.Add(centralUser);
                }
            }

            if (upsertProfiles.Count > 0)
            {
                await ExecuteWithRetry(async _ => await device.SetEmployeesData(upsertProfiles.Select(profile => profile.CreateDeviceUser()).ToList()), cancellationToken);
            }

            if (DeviceCapabilities.SupportsFaceTemplates(device.DeviceBiometricType, device.DeviceTypeCode))
            {
                foreach (var profile in centralUsers.Values)
                {
                    await SynchronizeFaceTemplatesAsync(device, profile, cancellationToken);
                }
            }
        }

        private async Task SynchronizeSingleUserAsync(AnvizDevice device, UserProfile profile, CancellationToken cancellationToken)
        {
            await EnsureDeviceMetadataAsync(device);

            await ExecuteWithRetry(async _ => await device.SetEmployeesData(profile.CreateDeviceUser()), cancellationToken);

            if (DeviceCapabilities.SupportsFaceTemplates(device.DeviceBiometricType, device.DeviceTypeCode))
            {
                await SynchronizeFaceTemplatesAsync(device, profile, cancellationToken);
            }
        }

        private async Task SynchronizeFaceTemplatesAsync(AnvizDevice device, UserProfile profile, CancellationToken cancellationToken)
        {
            if (profile.FaceTemplates.Count == 0)
            {
                if (options.RemoveOrphanFaceTemplates)
                {
                    var existing = await TryGetFaceTemplateAsync(device, profile.User.Id, cancellationToken);
                    if (existing != null)
                    {
                        await ExecuteWithRetry(async _ =>
                        {
                            try
                            {
                                await device.DeleteEmployeesData(profile.User.Id);
                            }
                            catch (Exception ex) when (IsUserNotFound(ex))
                            {
                            }

                            await device.SetEmployeesData(profile.CreateDeviceUser());
                        }, cancellationToken);
                    }
                }
                return;
            }

            var faceFormat = DeviceCapabilities.GetFaceTemplateFormat(device.DeviceTypeCode);
            var currentTemplate = options.ForceFaceTemplateRewrite ? null : await TryGetFaceTemplateAsync(device, profile.User.Id, cancellationToken);

            if (currentTemplate != null && profile.FaceTemplates.Any(template => template.ContentEquals(currentTemplate)))
            {
                return;
            }

            foreach (var template in profile.FaceTemplates)
            {
                await ExecuteWithRetry(async _ => await device.SetFaceTemplate(profile.User.Id, template, faceFormat), cancellationToken);
            }
        }

        private async Task EnsureDeviceMetadataAsync(AnvizDevice device)
        {
            if (device.DeviceBiometricType == BiometricType.Unknown || string.IsNullOrWhiteSpace(device.DeviceTypeCode))
            {
                await device.GetDeviceBiometricType();
            }
        }

        private async Task<FaceTemplate> TryGetFaceTemplateAsync(AnvizDevice device, ulong userId, CancellationToken cancellationToken)
        {
            try
            {
                var template = await device.GetFaceTemplateInfo(userId);
                if (template == null || template.IsEmpty)
                {
                    return null;
                }

                return template;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsUserNotFound(ex))
                {
                    return null;
                }

                if (!options.IgnoreFaceTemplateReadErrors)
                {
                    throw;
                }
                return null;
            }
        }

        private async Task ExecuteWithRetry(Func<int, Task> operation, CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= options.MaxRetryAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await operation(attempt);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    if (attempt == options.MaxRetryAttempts)
                    {
                        throw;
                    }
                    await Task.Delay(options.RetryDelay, cancellationToken);
                }
            }
        }

        private Task DeleteUserOnDeviceAsync(AnvizDevice device, ulong userId, CancellationToken cancellationToken)
        {
            return ExecuteWithRetry(async _ =>
            {
                try
                {
                    await device.DeleteEmployeesData(userId);
                }
                catch (Exception ex) when (IsUserNotFound(ex))
                {
                }
            }, cancellationToken);
        }

        private static bool IsUserNotFound(Exception ex)
        {
            if (ex == null || string.IsNullOrEmpty(ex.Message))
            {
                return false;
            }

            return ex.Message.IndexOf("NO_USER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ex.Message.IndexOf("EMPTY", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

