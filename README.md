# Clipboard Compatibility

A temporary Q-SYS Designer mod that restores native **Copy All from Component / Paste All to Component** when the component clipboard payload cannot be read on .NET 10.

Use the existing component context-menu commands in Emulate or Run. The mod transports component control values through a bounded JSON clipboard format and preserves Q-SYS's native selection, component-type checks and application of values. It adds no shortcuts or preferences and does not change custom bindings. Clipboard payloads work between Designer instances using this mod; copy again after enabling or disabling it.

## Compatibility and installation

- Exact inspected Q-SYS Designer 10.5.0 build; unexpected Designer/ModuleCommon hashes are rejected before hooks are registered.
- Q-SYS Mods Core 0.1.5 or later in the 0.1 series; Q-SYS Mod Manager 0.8 or later.
- Better Shortcuts is optional. The mod registers only a status command with Core.
- Install the `clipboard-compat` package through Q-SYS Mod Manager, preview the change, and apply it with Designer closed. The private module is `Designer.Modules.ClipboardCompat.dll`; shared Core and Harmony files stay managed by their existing packages.
- Native acceptance covers two Gain components in Emulate. Other component types and physical Core behavior remain untested. Q-SYS's documented exclusions and lack of Undo for pasted control settings still apply.

Disable or remove Clipboard Compatibility in Q-SYS Mod Manager while Designer is closed, then restart Designer and copy again. No preference migration or shortcut reset is needed.

## Retirement

This is a compatibility bridge, intended to be deprecated when QSC supplies a verified native fix. Test the two-Gain reproducer with this mod disabled on the vendor-fixed build before declaring the workaround unnecessary. Do not broaden the exact-build guard automatically. Mark the mod/release deprecated and explain removal in the catalog description and release notes, while retaining old releases and catalog approvals for historical profile restoration. Do not delete published packages or silently remove the mod from users' profiles.

## Implementation and evidence

Only three native command methods are patched. Reads validate a versioned string-only JSON schema and reject invalid, unexpected or oversized data. Paste execution snapshots valid data once to handle clipboard replacement between menu availability and execution. No BinaryFormatter runtime switch or global clipboard patch is used.

[Prototype validation](docs/PROTOTYPE-VALIDATION.md) records 21 payload checks, 14 native transport/Harmony checks and successful native-command plus mouse-driven Gain acceptance. The original defect and follow-up are tracked in [Core #4](https://github.com/joshua-small/qsys-mods-core/issues/4). A compatibility result is separate from a vendor fix.

Build with .NET SDK 10.0.112 using local vendor references and a separate artifacts directory:

```text
dotnet build Designer.Modules.ClipboardCompat.csproj -c Release --artifacts-path <output> -p:DesignerDir=<Designer-directory> -p:CoreProject=<Mods-Core>/Core/Qmm.Shared.ModsCore.csproj
dotnet run --project tests/Payload/PayloadTests.csproj -c Release --artifacts-path <separate-output>
```

The payload test project links the production `Payload.cs` and runs on Linux or
Windows without Designer, vendor assemblies, Mods Core, or clipboard access.
It checks exact and exceeded size limits, ordinal names, Unicode round trips,
duplicate and escaped keys, malformed schemas, and rejected value types.
Run it from the repository root with the output directory outside the source
tree. These checks establish codec behavior only; they do not verify native
clipboard transport, Designer commands, or a vendor fix.

Windows native/Designer tests and their profile-isolated launcher are under `tests/`. The native transport tests replace the clipboard with synthetic values. No test fixture, vendor binary, startup hook or Core runtime is included in this mod's package.

References: [Q-SYS's native copy/paste behavior](https://help.qsys.com/Content/Q-Sys_Designer/Q-Sys_Designer_Shortcuts.htm), [Microsoft's .NET 10 clipboard migration guidance](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/migration/clipboard-dataobject-net10), [package contract](https://github.com/joshua-small/qsys-mod-catalog/blob/main/PACKAGE-CONTRACT.md).
