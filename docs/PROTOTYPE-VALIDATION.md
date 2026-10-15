# Clipboard Compatibility prototype

Temporary Copy All from Component / Paste All to Component repair for the inspected Q-SYS Designer 10.5.0 build. The local prototype is implemented and accepted for two Gain components in Emulate. It has not been published, approved in the mod catalog, or installed in the regular Designer application.

The native custom-object clipboard read fails on .NET 10.0.13 in the retained mod-free diagnostic. Microsoft documents the .NET 10 clipboard serialization change and supports explicit string serialization with typed reads: [clipboard migration guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/migration/clipboard-dataobject-net10), [TryGetData](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.clipboard.trygetdata?view=windowsdesktop-10.0). This module transports the native component control data as bounded JSON in a private string clipboard format.

## Scope and safeguards

- Mods Core owns initialization, the status command and command registration. Better Shortcuts is optional; it was absent from this acceptance run. This module adds no default shortcuts, preference files or settings watchers.
- Core checks the exact Designer and ModuleCommon hashes before the initializer. The module verifies the native payload shape and each replacement call site before patching. Existing Harmony patches on any target cause initialization to fail; partial installation is rolled back.
- Only three native methods are patched: CopyControls' async implementation, PasteControls.Can and PasteControls.CustomRun. Native command identity, selection/type eligibility and application of values stay with Q-SYS. There is no global clipboard patch or BinaryFormatter enablement.
- The private format is `QsysMods.ComponentControls.Json.v1`. The schema contains only an integer version, component class name and string-to-string control dictionary. Reads reject unknown or duplicate fields, invalid types, empty control sets and oversized data. Limits are 1,048,576 JSON characters, 4,096 controls, 256 class-name characters, 512 control-name characters and 16,384 characters per value.
- Paste execution takes one validated snapshot, so a clipboard change after menu availability cannot pass a null payload into the native handler. An unreadable or invalid payload disables/refuses paste. A rejected write leaves the previous clipboard intact.
- Copy All replaces the clipboard as usual. Ordinary text and unrelated native clipboard formats are not intercepted. Component data is shared only with another process running this compatibility protocol; legacy native component payloads are not imported. Copy again after enabling or removing the module.

Q-SYS documents Copy All/Paste All for Run/Emulate and compatible component types, with exclusions and no Undo for the pasted values: [Q-SYS shortcuts](https://help.qsys.com/Content/Q-Sys_Designer/Q-Sys_Designer_Shortcuts.htm). This module preserves the native command path. Acceptance here covers Gain in Emulate only; other components, a physical Core and hardware effects remain untested.

## Build and test

Use .NET SDK 10.0.112 and a separate artifacts directory. On this workstation, build through the Windows-to-WSL wrapper; run WPF/clipboard/Designer tests with native Windows .NET. Do not mix their build output with a different platform's output.

The project accepts `DesignerDir` and `CoreProject` overrides. Its current default Core path points to the retained sibling acceptance checkout; pass the path to `Core/Qmm.Shared.ModsCore.csproj` when building elsewhere. Acceptance used Core source `7052d2a0e78752353e2f1b568703397575cada59`, whose product implementation is included in merged Core PR #11.

```sh
# WSL, with absolute Linux paths in these variables:
dotnet run --project tests/Payload/PayloadTests.csproj -c Release --artifacts-path "$PAYLOAD_BUILD"
dotnet build tests/Native/NativeTests.csproj -c Release --artifacts-path "$NATIVE_BUILD" -p:DesignerDir="$DESIGNER_DIR" -p:CoreProject="$CORE_PROJECT"
dotnet build tests/Designer/Probe.csproj -c Release --artifacts-path "$NATIVE_BUILD" -p:DesignerDir="$DESIGNER_DIR" -p:CoreProject="$CORE_PROJECT"
```

Run `NativeTests.dll <Designer-directory>` in a native Windows STA process. It intentionally replaces the Windows clipboard with synthetic test values. The optional second argument `read` only reads the existing compatibility payload, allowing a producer-exit persistence check.

The Designer fixture requires an already prepared disposable installation, the lab/profile marker configuration, and the Core ProfileHook build. `tests/Designer/Start-Lab.ps1` verifies the exact host, confines archival moves to that lab, rejects running lab processes and preserves previous lab contents. It redirects the native profile before creating two synthetic Gains and entering local Emulate. Never use this fixture to prepare the regular installation.

No vendor assemblies are included in this source. Build references remain local. The product DLL depends on the matching Mods Core runtime, loader and host Harmony installation; the lab fixture is a separate test assembly and must not be distributed as the feature.

## Acceptance on 2026-10-15

- 21 payload validation checks passed.
- 14 native Windows transport/Harmony checks passed on .NET 10.0.13, including malformed/type-mismatched payloads, clipboard replacement, unrelated formats, hook collision and complete unpatching.
- Release builds completed with zero warnings/errors.
- The disposable Designer's actual native commands copied an unmuted Gain, enabled paste on a muted Gain and produced an unmuted destination on native copy-back. Wrong component class and plain-text clipboard content disabled paste.
- Windows input-helper mouse clicks then exercised the visible native menu: Copy All enabled Paste All on the other Gain; clicking Paste All turned off the destination's red Mute button. This was helper-driven mouse input, not owner-entered input. The native command fixture and the mouse check are separate evidence.
- After closing the disposable Designer, a separate native process read the Gain clipboard payload successfully. It retained bypass, invert, mute and gain strings.
- Personal Prefs.xml, HardLink.json, HardLinkNames.json and mod preferences matched their same-start SHA-256 hashes. Custom bindings were preserved. The disposable process was stopped after testing; the regular installation was not modified.

Exact accepted product DLL SHA-256:

```text
9563A8CC530D001921C9EEB7CCFF9883CAC82E0B33A32BDF9962083D2B2BEE02
```

Accepted host hashes:

```text
Q-Sys Designer.dll  0E7150F8DC02E0159E019188780FB003CE025F5EC18C491609CF337D849BDA60
ModuleCommon.dll   A6D1ADDDA06BDCB1AD32B57C7480C947210C3D3AD287F5818C2EA64068BF5AEE
```

Raw JSON receipts and preference hashes remain local in `../receipts`, `../personal-before.json` and `../personal-after.json`. The original native defect remains tracked in [Core #4](https://github.com/joshua-small/qsys-mods-core/issues/4) and [Better Shortcuts #2](https://github.com/joshua-small/qsys-better-shortcuts/issues/2). A full unmodified Designer GUI reproduction and a vendor defect reference are still outstanding. Success with this module does not establish a vendor fix.

## Disable and next delivery step

Close Designer before changing installed modules. Remove only this module's private `Designer.Modules.ClipboardCompat.dll` through its eventual installation mechanism, then restart and copy again. Keep the shared Core/runtime/Harmony files. Removal restores the original native behavior, including the original clipboard failure on the tested runtime. The status command is available through Core's Mods menu while the module is active.

This source and the accepted DLL are a reviewable local result. Public repository identity, catalog approval, a `.qdsmod` package and regular installation remain separate delivery work. No public package or supported cross-version claim is made by this prototype.
