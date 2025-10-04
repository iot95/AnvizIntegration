# Anviz SDK Usage Guide

This document describes how to use the Anviz .NET SDK to integrate Anviz terminals such as the C2 KA and FaceDeep 5 with your applications. The library targets **.NET Standard 2.0**, making it compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and modern .NET releases.

> The guide covers the public surface of the SDK as it exists in this repository, including connection helpers, device management, biometric enrollment, and the multi-device user synchronization utilities.

## Table of contents

- [Getting the library](#getting-the-library)
- [Library architecture](#library-architecture)
- [Connecting to devices](#connecting-to-devices)
  - [Authenticating connections](#authenticating-connections)
  - [Device events](#device-events)
- [Device metadata and configuration](#device-metadata-and-configuration)
  - [Device identity and capabilities](#device-identity-and-capabilities)
  - [Date and time management](#date-and-time-management)
  - [Network parameters](#network-parameters)
  - [General settings](#general-settings)
  - [Schedules and time zones](#schedules-and-time-zones)
  - [Maintenance commands](#maintenance-commands)
- [Records and live attendance data](#records-and-live-attendance-data)
- [User management](#user-management)
  - [Working with `UserInfo`](#working-with-userinfo)
  - [Creating, updating, and deleting users](#creating-updating-and-deleting-users)
  - [Card and password credentials](#card-and-password-credentials)
- [Fingerprint templates](#fingerprint-templates)
- [Face templates](#face-templates)
  - [Understanding device face formats](#understanding-device-face-formats)
  - [Reading face templates](#reading-face-templates)
  - [Writing face templates](#writing-face-templates)
- [Synchronizing users across multiple devices](#synchronizing-users-across-multiple-devices)
  - [Constructing user profiles](#constructing-user-profiles)
  - [Running full synchronization](#running-full-synchronization)
  - [On-demand upsert or deletion](#on-demand-upsert-or-deletion)
  - [Synchronization options](#synchronization-options)
- [Error handling and retries](#error-handling-and-retries)
- [Sample application](#sample-application)
- [Troubleshooting tips](#troubleshooting-tips)

## Getting the library

### NuGet

The package is published as [`Anviz.SDK`](https://www.nuget.org/packages/Anviz.SDK). Install it into your project with the .NET CLI:

```bash
dotnet add package Anviz.SDK
```

### Building locally

Clone the repository and build the library and sample using the .NET SDK:

```bash
# Restore and build the SDK
 dotnet build Anviz.SDK/Anviz.SDK.csproj

# Build the sample console application
 dotnet build Sample/Sample.csproj
```

The SDK targets `netstandard2.0`, so any .NET implementation supporting that target can consume the library directly.【F:Anviz.SDK/Anviz.SDK.csproj†L1-L12】

## Library architecture

The SDK centres around two top-level types:

- `AnvizManager` encapsulates connection management. It can actively connect to a device (client mode) or accept incoming device connections (server mode).【F:Anviz.SDK/AnvizManager.cs†L7-L59】
- `AnvizDevice` represents an open session with a device. The class exposes asynchronous methods that wrap every supported protocol command. Instances also raise events for pings, packets, attendance records, and connection errors.【F:Anviz.SDK/AnvizDevice.cs†L1-L44】

Under the hood, `AnvizDevice` sends protocol commands via an internal `AnvizStream` that maintains a background receiver thread and enforces command/response correlation and timeouts.【F:Anviz.SDK/AnvizStream.cs†L1-L86】【F:Anviz.SDK/AnvizStream.cs†L88-L132】

Commands live in `Anviz.SDK.Commands` and cover actions like retrieving device information, managing records, and uploading biometric data. Command responses are deserialized into the POCO classes under `Anviz.SDK.Responses` (for example, `UserInfo`, `Record`, `BasicSettings`, and `TcpParameters`).

Utility namespaces provide helpers for encoding biometrics (`Anviz.SDK.Utils.Fingers`), handling device capabilities (`Anviz.SDK.Utils.DeviceCapabilities`), and managing higher level models such as face templates and user synchronization (`Anviz.SDK.Users`).

## Connecting to devices

### Client mode

To connect to a terminal from an application acting as a client:

```csharp
var manager = new AnvizManager();
using var device = await manager.Connect("192.168.1.100", 5010);
```

`Connect` opens a `TcpClient` to the specified host and port (default `5010`) and negotiates the session, including optional connection password authentication.【F:Anviz.SDK/AnvizManager.cs†L15-L59】

### Server/listener mode

Devices can also be configured to push connections to your server. Use `Listen` to start a `TcpListener` and `Accept` to await incoming sessions:

```csharp
var manager = new AnvizManager();
manager.Listen(); // binds to 0.0.0.0:5010 by default
using var device = await manager.Accept();
```

You can provide a custom `IPEndPoint` to `Listen(IPEndPoint)` if you need a specific interface or port.【F:Anviz.SDK/AnvizManager.cs†L21-L44】 Call `StopServer` when you want to stop listening.

### Authenticating connections

Set `ConnectionUser`, `ConnectionPassword`, and `AuthenticateConnection = true` on `AnvizManager` before connecting if your devices enforce the Anviz connection password. The manager automatically calls `SetConnectionPassword` after connecting when authentication is enabled.【F:Anviz.SDK/AnvizManager.cs†L9-L59】

### Device events

Each `AnvizDevice` exposes the following events for real-time integration scenarios:

- `DevicePing` fires when the device sends a ping (`0x7F`) and the SDK automatically responds with a `Pong`.【F:Anviz.SDK/AnvizDevice.cs†L16-L33】【F:Anviz.SDK/AnvizStream.cs†L52-L85】
- `ReceivedPacket` emits any asynchronous response that does not match an outstanding command.
- `ReceivedRecord` emits `Record` objects for real-time attendance pushes (`0xDF`).
- `DeviceError` fires when the underlying stream encounters a read error or disconnect.

Subscribe to these events immediately after connecting to react to device activity.

## Device metadata and configuration

### Device identity and capabilities

Use the following methods on `AnvizDevice` to learn about the connected terminal:

- `GetDeviceSN()` returns the serial number.
- `GetDeviceID()` / `SetDeviceID(ulong id)` reads or changes the device identifier.
- `GetDeviceTypeCode()` gives you the device model code (for example `FACEDEEP5`).
- `GetDeviceBiometricType()` detects whether the terminal is finger, face, or iris-based.【F:Anviz.SDK/Commands/GetDeviceTypeCommand.cs†L1-L34】【F:Anviz.SDK/Utils/BiometricType.cs†L1-L37】

`AnvizDevice` caches the biometric type and type code so that features such as face template synchronization can adapt to device capabilities.【F:Anviz.SDK/AnvizDevice.cs†L11-L14】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L71-L116】 The helper `DeviceCapabilities.GetFaceTemplateFormat` inspects the type code to determine the proper face template payload layout for FacePass/FaceDeep devices.【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L1-L42】

### Date and time management

Synchronize device time with `GetDateTime()` and `SetDateTime(DateTime)`. The `SetDateTime` command also updates the internal RTC so that log timestamps remain accurate.【F:Sample/Program.cs†L21-L61】

### Network parameters

Retrieve and update TCP/IP settings with `GetTcpParameters()` and `SetTCPParameters(TcpParameters)`. The response object exposes IP address, subnet, gateway, MAC address, and TCP mode values.【F:Sample/Program.cs†L61-L88】

### General settings

- `GetBasicSettings()` / `SetBasicSettings(BasicSettings)` cover options such as firmware version, device password, speaker volume, date format, and 24-hour display.
- `GetAdvancedSettings()` / `SetAdvancedSettings(AdvancedSettings)` control fingerprint precision, repeat attendance delay, and real-time push mode.【F:Sample/Program.cs†L61-L107】

### Schedules and time zones

The SDK includes helpers for schedule bells and time zone definitions:

- `GetScheduledBells()` / `SetScheduledBell(ScheduledBell)` manage the device bell schedule.
- `GetTimeZoneInfo()` / `SetTimeZoneInfo(AnvizTimeZone[])` retrieve or update time zone segments.

### Maintenance commands

Common maintenance operations include:

- `ClearNewRecords()` or `ClearRecords()` to purge attendance logs.
- `RebootDevice()` to restart the terminal.
- `ResetToFactorySettings()` to return the device to factory defaults.
- `UnlockDoor()` to trigger the relay remotely.

Each method returns a `Task` that resolves when the device acknowledges the command.

## Records and live attendance data

Call `DownloadRecords(bool onlyNew)` to bulk-fetch attendance logs. Pass `true` to receive only new records since the last download, or `false` for the entire history. Iterate through the returned `Record` objects for user code, timestamp, status, and verification method details.【F:Sample/Program.cs†L107-L138】

Real-time attendance pushes raise the `ReceivedRecord` event; the sample application shows how to keep the connection alive and handle incoming pushes for five minutes after synchronizing state.【F:Sample/Program.cs†L23-L140】 Remember to call `ClearNewRecords()` after processing a batch so that the device’s incremental log cursor advances.

## User management

### Working with `UserInfo`

`UserInfo` represents the employee metadata stored on the device: numeric identifier, password, card number, display name, department, group, access mode, and enrolled fingerprints.【F:Anviz.SDK/Responses/UserInfo.cs†L1-L96】 It exposes constructors for parsing data from the device, cloning existing users, or creating a new record with defaults (department 1, group 1, attendance mode 6).【F:Anviz.SDK/Responses/UserInfo.cs†L41-L96】

`UserInfo.CloneForDevice()` produces a sanitized copy of a user suitable for upload, trimming names to the 10-character limit imposed by firmware and preserving other attributes.【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L1-L35】【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L37-L59】 The helper `EquivalentTo` performs value-based comparisons that tolerate differences in whitespace and nullability, which is especially helpful when diffing central data against device state during synchronization.【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L18-L63】

### Creating, updating, and deleting users

The key methods for maintaining user data are:

- `GetEmployeesData()` to download the full list of `UserInfo` records.
- `SetEmployeesData(UserInfo user)` to add or update a single user.
- `SetEmployeesData(List<UserInfo> users)` to bulk upload several entries at once.
- `DeleteEmployeesData(ulong userId)` to remove a user from the device.

When enrolling new users, you can call `EnrollFingerprint(userId, fingerIndex)` to instruct the device to scan a fingerprint interactively. Afterwards, `SetFingerprintTemplate` lets you upload a captured template to another finger slot or to other devices.【F:Sample/Program.cs†L109-L132】

### Card and password credentials

Populate the `Password` or `Card` properties on `UserInfo` before uploading. Leave them `null` to clear credentials, which the SDK serializes as `0xFF` placeholders as required by the protocol.【F:Anviz.SDK/Responses/UserInfo.cs†L57-L87】 The `Utils.Fingers` helper provides the `Finger` enumeration and methods for encoding/decoding the enrolled fingerprint bit mask.【F:Anviz.SDK/Utils/Fingers.cs†L7-L65】

## Fingerprint templates

Use the following methods to manage fingerprint biometrics programmatically:

- `GetFingerprintTemplate(ulong userId, Finger finger)` returns the raw template bytes for a specific finger slot.
- `SetFingerprintTemplate(ulong userId, Finger finger, byte[] template)` writes a template captured elsewhere.

Templates can be stored or replicated across devices, enabling centralized enrollment workflows for fingerprint-based hardware such as the C2 KA.【F:Sample/Program.cs†L118-L132】 Ensure that the `Finger` enum value matches the slot you intend to populate.

## Face templates

Face-based devices (FacePass and FaceDeep families) exchange face templates as binary payloads. The SDK provides a strongly-typed wrapper and format detection so that you can reliably transfer templates without triggering `RET ERROR` responses.

### Understanding device face formats

`DeviceCapabilities.SupportsFaceTemplates` checks whether the connected device supports face biometrics based on its reported biometric type.【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L34-L37】 For supported devices, `DeviceCapabilities.GetFaceTemplateFormat` returns a `DeviceFaceTemplateFormat` describing the expected payload size and padding rules for the current model (FacePass 7 and FaceDeep 5 use a 15,360-byte template layout).【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L1-L33】【F:Anviz.SDK/Users/DeviceFaceTemplateFormat.cs†L1-L22】

### Reading face templates

`GetFaceTemplateInfo(ulong userId)` retrieves and parses the template into a `FaceTemplate` object, trimming trailing zero padding and exposing a SHA-256-based `ContentHash` so you can detect changes efficiently.【F:Anviz.SDK/Commands/GetFaceTemplateCommand.cs†L1-L27】【F:Anviz.SDK/Users/FaceTemplate.cs†L1-L63】 Use the `IsEmpty` property to determine whether the device currently stores a template for that user.

If you need the raw payload exactly as sent by the device, call `GetFaceTemplate` instead—it returns the unprocessed byte array.【F:Anviz.SDK/Commands/GetFaceTemplateCommand.cs†L19-L26】

### Writing face templates

Create a `FaceTemplate` from raw template bytes and upload it with `SetFaceTemplate`:

```csharp
var template = new FaceTemplate(faceBytes);
await device.SetFaceTemplate(userId, template);
```

`FaceTemplate.ToDevicePayload` pads or trims the template according to the selected format and encodes the header required by the protocol (employee ID and face index). Attempting to upload a template larger than the device capacity throws an exception before the command is sent, preventing confusing firmware errors.【F:Anviz.SDK/Users/FaceTemplate.cs†L33-L61】 You can also pass a custom `DeviceFaceTemplateFormat` if you need to target a non-standard device revision.【F:Anviz.SDK/Commands/SetFaceTemplateCommand.cs†L1-L33】

## Synchronizing users across multiple devices

The `Anviz.SDK.Users` namespace contains higher-level primitives for keeping a central user database synchronized with many terminals.

### Constructing user profiles

`UserProfile` pairs a `UserInfo` with zero or more face templates. Adding a template clones the underlying data and de-duplicates by face index so that updates overwrite previous entries safely.【F:Anviz.SDK/Users/UserProfile.cs†L1-L52】 Use `ClearFaceTemplates()` when you want to remove all associated face biometrics from a user profile.

### Running full synchronization

`UserSynchronizationService.SynchronizeAsync` accepts a collection of `AnvizDevice` instances and a collection of central `UserProfile` records. It downloads the current employees from each device, deletes users that no longer exist centrally, uploads missing or changed users, and then synchronizes face templates when the device supports them.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L1-L120】 Face templates are only rewritten when the central template differs from the device copy, minimizing unnecessary transfers.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L129-L173】

### On-demand upsert or deletion

- `UpsertUserAsync` propagates a single `UserProfile` to every device, updating both demographic data and face templates as required.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L50-L72】
- `DeleteUserAsync` removes the specified employee ID from all devices, swallowing “user not found” errors so that the operation remains idempotent.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L74-L111】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L185-L206】

### Synchronization options

Customize behaviour by supplying `UserSynchronizationOptions` to the service constructor:

- `MaxRetryAttempts` and `RetryDelay` control exponential retry semantics for device operations.
- `ForceFaceTemplateRewrite` re-uploads templates even if the content hash matches.
- `RemoveOrphanFaceTemplates` deletes face templates from devices when the central profile no longer has any associated faces.
- `IgnoreFaceTemplateReadErrors` allows synchronization to continue when reading a face template fails on a single device.【F:Anviz.SDK/Users/UserSynchronizationOptions.cs†L1-L12】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L122-L204】

All operations execute concurrently across devices and honour the provided `CancellationToken` so you can cancel long-running synchronizations gracefully.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L23-L121】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L151-L204】

## Error handling and retries

Most SDK methods throw exceptions when the device disconnects or returns an error code. For low-level reliability, `AnvizStream` enforces a 20-second timeout on every command; if a response does not arrive in time, the SDK closes the connection and throws an exception so that you can reconnect.【F:Anviz.SDK/AnvizStream.cs†L12-L85】【F:Anviz.SDK/AnvizStream.cs†L88-L132】

`UserSynchronizationService` wraps mutating commands in a retry loop via `ExecuteWithRetry`, giving transient network hiccups a chance to recover before surfacing an error. Supply a cancellation token to abort retries from calling code when needed.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L175-L206】

## Sample application

The `Sample` project demonstrates a full lifecycle against a single device: it listens for a connection, authenticates, reads device metadata, synchronizes time and network settings, enumerates users and fingerprints, enrols a new user, downloads attendance records, and keeps the session open to receive real-time events.【F:Sample/Program.cs†L1-L145】 Use it as a starting point for your own diagnostics or integration tests.

## Troubleshooting tips

- **RET ERROR when uploading face templates:** Ensure that you construct a `FaceTemplate` and let the SDK apply device-specific padding rules. Uploading raw bytes without padding is the most common cause of firmware errors on FaceDeep-class devices.【F:Anviz.SDK/Users/FaceTemplate.cs†L33-L61】
- **Unexpected name truncation:** Device firmware limits display names to 10 characters. `UserInfo.CloneForDevice()` and the synchronization service automatically trim names, but you should enforce the same constraint in your UI to avoid surprises.【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L1-L35】
- **Transient connection drops:** Wrap long operations in retry logic or leverage the built-in retry settings of `UserSynchronizationService` to handle intermittent network issues gracefully.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L175-L206】
- **Detecting device capabilities:** Always call `GetDeviceBiometricType()` after connecting. The cached `DeviceBiometricType` informs higher-level features about whether finger or face operations are supported.【F:Anviz.SDK/AnvizDevice.cs†L11-L44】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L71-L120】

With these tools, you can build resilient provisioning and attendance solutions that keep user data synchronized across large fleets of Anviz terminals.
