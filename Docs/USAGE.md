# Anviz SDK Usage Guide

This guide walks through the public API that ships with the Anviz .NET SDK in this repository. It focuses on day-to-day device operations, biometric management, and the multi-device synchronisation helpers that make it easier to keep Anviz C2 KA, FacePass, and FaceDeep-class terminals aligned.

## 1. Getting started

### Install from NuGet

The SDK is published as [`Anviz.SDK`](https://www.nuget.org/packages/Anviz.SDK). Add it to your project by running:

```bash
dotnet add package Anviz.SDK
```

### Build the sources locally

Clone the repository and build the library and sample application with the .NET SDK:

```bash
# Restore and build the SDK project
dotnet build Anviz.SDK/Anviz.SDK.csproj

# Build the sample console application
dotnet build Sample/Sample.csproj
```

The SDK targets .NET Standard 2.0, so it can be consumed from .NET Framework 4.6.1 or later and every modern .NET release.【F:Anviz.SDK/Anviz.SDK.csproj†L1-L12】

## 2. Library overview

* `AnvizManager` owns TCP client/server plumbing. It supports outbound connections via `Connect`, listener mode via `Listen`/`Accept`, and optional connection-password authentication.【F:Anviz.SDK/AnvizManager.cs†L7-L61】
* `AnvizDevice` represents an open session. It tracks the device ID, biometric type, and model code, and surfaces events for pings, live attendance records, and connection faults.【F:Anviz.SDK/AnvizDevice.cs†L9-L43】
* `AnvizStream` handles protocol I/O on a background thread, enforces a 20 second timeout for every command, answers device pings automatically, and raises events for asynchronous packets.【F:Anviz.SDK/AnvizStream.cs†L12-L112】

Higher-level namespaces include:

* `Anviz.SDK.Commands` – typed wrappers for the underlying protocol commands.
* `Anviz.SDK.Responses` – POCOs for deserialised responses, such as `UserInfo`, `Record`, `BasicSettings`, and `TcpParameters`.
* `Anviz.SDK.Users` – utilities for face template handling and synchronising a central user roster across many terminals.
* `Anviz.SDK.Utils` – helpers for encoding values, working with fingerprints, and normalising user information.

## 3. Connecting to devices

### Client (polling) mode

Create an `AnvizManager` and call `Connect` to open an outbound TCP connection (default port 5010). The manager returns an `AnvizDevice` instance that you should dispose when finished.【F:Anviz.SDK/AnvizManager.cs†L15-L61】

```csharp
var manager = new AnvizManager();
using var device = await manager.Connect("192.168.1.120", 5010);
```

### Listener (push) mode

If terminals are configured to connect to you, call `Listen` (optionally with a custom `IPEndPoint`) and then `Accept` to wait for an incoming socket. The sample application demonstrates the full pattern, including wiring up real-time events.【F:Anviz.SDK/AnvizManager.cs†L22-L50】【F:Sample/Program.cs†L11-L108】

### Authenticating the link

Set `ConnectionUser`, `ConnectionPassword`, and `AuthenticateConnection = true` before obtaining a device when the terminal enforces the Anviz connection password. `AnvizManager` will automatically send `SetConnectionPassword` during device initialisation.【F:Anviz.SDK/AnvizManager.cs†L11-L61】

## 4. Device insight and configuration

After connecting, use the following methods on `AnvizDevice`:

* Identity: `GetDeviceSN`, `GetDeviceID`, `SetDeviceID`, `GetDeviceTypeCode`, and `GetDeviceBiometricType`. The biometric type and type code are cached so downstream features (for example face template syncing) know what the device supports.【F:Anviz.SDK/Commands/GetDeviceTypeCommand.cs†L7-L35】【F:Anviz.SDK/AnvizDevice.cs†L11-L19】
* Time: `GetDateTime` / `SetDateTime` keep the real-time clock in sync.【F:Sample/Program.cs†L38-L45】
* Network: `GetTcpParameters` / `SetTCPParameters` read and update IP addressing, gateway, and TCP mode.【F:Sample/Program.cs†L46-L51】
* Settings: `GetBasicSettings` / `SetBasicSettings` and `GetAdvancedSettings` / `SetAdvancedSettings` adjust firmware options such as speaker volume, date formats, and real-time push mode.【F:Sample/Program.cs†L52-L74】
* Maintenance: commands exist to reboot, reset to factory defaults, unlock the door relay, clear attendance records, and clear user data (see `Sample/Program.cs` for examples).【F:Sample/Program.cs†L67-L107】
* Scheduling: `GetScheduledBells`/`SetScheduledBell` and `GetTimeZoneInfo`/`SetTimeZoneInfo` manage bell tables and access schedules.【F:Anviz.SDK/Commands/GetScheduledBellsCommand.cs†L7-L31】【F:Anviz.SDK/Commands/GetTimeZoneInfoCommand.cs†L7-L33】

To quickly inspect a device, run the sample console and follow the prompts. It connects, prints metadata, synchronises time, lists users, and downloads attendance data.【F:Sample/Program.cs†L11-L108】

## 5. Attendance data

* Bulk history: `DownloadRecords(true)` retrieves new attendance events since the last clear, while `DownloadRecords(false)` downloads the full log.【F:Sample/Program.cs†L98-L103】
* Real-time pushes: subscribe to `ReceivedRecord` to react to live punches without polling.【F:Anviz.SDK/AnvizDevice.cs†L16-L33】
* Housekeeping: call `ClearNewRecords` after processing incremental downloads so the device advances its cursor.【F:Sample/Program.cs†L98-L104】

## 6. User data management

### Understanding `UserInfo`

`UserInfo` encapsulates the demographic details stored on a terminal: employee ID, optional password and card, display name, department, group, access mode, and enrolled finger flags.【F:Anviz.SDK/Responses/UserInfo.cs†L10-L53】 It offers constructors for cloning and for creating new users with sane defaults (department 1, group 1, mode 6).【F:Anviz.SDK/Responses/UserInfo.cs†L56-L113】

`UserInfo.CloneForDevice()` trims names to the firmware’s 10-character display limit, while `EquivalentTo` performs a tolerant comparison ideal for detecting changes during synchronisation.【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L9-L65】

### CRUD operations

`AnvizDevice` exposes high-level helpers for user lifecycle management:

* `GetEmployeesData()` returns every `UserInfo` on the device.
* `SetEmployeesData(UserInfo)` adds or updates a single user.
* `SetEmployeesData(List<UserInfo>)` performs a bulk upload.
* `DeleteEmployeesData(ulong id)` removes a user.
* `SetRecords(Record)` inserts a manual record entry if you need to seed data.【F:Sample/Program.cs†L75-L103】

When enrolling interactively, call `EnrollFingerprint(userId, fingerIndex)`; the result can be saved locally or pushed to other terminals with `SetFingerprintTemplate`.【F:Sample/Program.cs†L92-L97】【F:Anviz.SDK/Commands/SetFingerprintTemplateCommand.cs†L23-L28】

### Passwords, cards, and fingerprints

Populate the `Password` and `Card` properties on `UserInfo` to configure credentials; leave them null to clear them, which the SDK serialises as `0xFF` placeholders per protocol requirements.【F:Anviz.SDK/Responses/UserInfo.cs†L25-L93】 The `Fingers` helper encodes enrolled fingers and converts masks into the strongly typed `Finger` enumeration.【F:Anviz.SDK/Responses/UserInfo.cs†L17-L50】

## 7. Fingerprint template management

Programmatic template transfer is straight-forward:

* `GetFingerprintTemplate(ulong userId, Finger finger)` downloads raw template bytes from a slot.【F:Anviz.SDK/Commands/GetFingerprintTemplateCommand.cs†L22-L28】
* `SetFingerprintTemplate(ulong userId, Finger finger, byte[] template)` uploads a template captured elsewhere.【F:Anviz.SDK/Commands/SetFingerprintTemplateCommand.cs†L23-L28】

Templates can be forwarded to additional devices after a single enrolment, ensuring consistent finger credentials across a fleet.【F:Sample/Program.cs†L83-L97】

## 8. Face templates

Face-based terminals require correctly sized payloads. The SDK includes tooling to avoid firmware errors such as `RET ERROR` when uploading templates.

* `DeviceCapabilities.SupportsFaceTemplates` and `GetFaceTemplateFormat` inspect the model code and return the appropriate template descriptor for FacePass and FaceDeep devices.【F:Anviz.SDK/Utils/DeviceCapabilities.cs†L7-L45】
* `FaceTemplate` wraps template bytes, trims trailing padding, computes a SHA-256 hash for quick change detection, and converts the data back into the device payload format (with automatic header and padding handling).【F:Anviz.SDK/Users/FaceTemplate.cs†L8-L114】
* `GetFaceTemplateInfo` returns a `FaceTemplate`, while `GetFaceTemplate` returns the raw payload unchanged.【F:Anviz.SDK/Commands/GetFaceTemplateCommand.cs†L8-L35】
* `SetFaceTemplate` accepts either a `FaceTemplate` or raw bytes and pads/trims according to the resolved descriptor so uploads succeed on FaceDeep-class hardware.【F:Anviz.SDK/Commands/SetFaceTemplateCommand.cs†L9-L45】

## 9. Multi-device synchronisation

The `Anviz.SDK.Users` namespace provides a resilient synchronisation pipeline for environments with a central user database.

* `UserProfile` couples a `UserInfo` with zero or more `FaceTemplate` instances and deduplicates face indices when you add templates.【F:Anviz.SDK/Users/UserProfile.cs†L8-L61】
* `UserSynchronizationOptions` controls retry counts, delay between attempts, face-template rewrite behaviour, and whether orphaned face data should be removed.【F:Anviz.SDK/Users/UserSynchronizationOptions.cs†L5-L12】
* `UserSynchronizationService` orchestrates full-device diffs, concurrent propagation of single-user updates, and idempotent deletes. It fetches device rosters, compares them with the central dictionary, bulk uploads changes, and pushes face templates when the device supports them.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L10-L206】

High-level entry points include:

* `SynchronizeAsync(devices, centralProfiles)` – downloads device users, removes orphans, updates stale entries, and syncs face templates across every terminal.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L23-L155】
* `UpsertUserAsync(devices, profile)` – pushes a single profile (user + faces) everywhere.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L40-L156】
* `DeleteUserAsync(devices, userId)` – removes a user on all devices while swallowing "user not found" responses so retries stay idempotent.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L56-L235】

All device interactions run concurrently and accept a `CancellationToken` for graceful cancellation during large deployments.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L23-L220】

## 10. Error handling and resilience

* Transport-level reliability comes from `AnvizStream`, which sets socket send/receive timeouts to 20 seconds and aborts the connection if a response does not arrive, preventing stuck commands.【F:Anviz.SDK/AnvizStream.cs†L12-L112】
* `UserSynchronizationService` wraps every mutating command in a retry loop with configurable attempts and delay. Supply a cancellation token to halt retries from caller code when necessary.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L198-L220】
* Firmware errors such as "NO_USER" or "EMPTY" are detected and treated as benign during deletions, keeping synchronisation idempotent.【F:Anviz.SDK/Users/UserSynchronizationService.cs†L223-L246】

## 11. Sample console application

The `Sample` project strings these concepts together: it authenticates a device, prints metadata, synchronises the clock, lists users, downloads attendance, performs a fingerprint enrolment, and waits for live punches. Use it as a diagnostic starting point or a reference implementation.【F:Sample/Program.cs†L11-L107】

## 12. Troubleshooting tips

* **Face uploads return `RET ERROR`:** Construct a `FaceTemplate` and let `SetFaceTemplate` pad/trim the payload instead of sending raw bytes directly.【F:Anviz.SDK/Users/FaceTemplate.cs†L47-L63】【F:Anviz.SDK/Commands/SetFaceTemplateCommand.cs†L21-L43】
* **Names appear truncated:** Device firmware limits names to 10 characters. `CloneForDevice` enforces this automatically; ensure upstream systems honour the same limit.【F:Anviz.SDK/Utils/UserInfoExtensions.cs†L9-L65】
* **Intermittent connection drops:** Expect to reconnect if the 20 second timeout in `AnvizStream` elapses. For bulk operations, rely on the retry policy exposed by `UserSynchronizationService`.【F:Anviz.SDK/AnvizStream.cs†L12-L112】【F:Anviz.SDK/Users/UserSynchronizationService.cs†L198-L220】

With these tools you can provision, audit, and synchronise fleets of Anviz devices from a central .NET application.
