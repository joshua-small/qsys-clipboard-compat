# Release candidate validation, 2026-10-15

Product source is unchanged from the accepted clipboard implementation in `ae3e9ae`. Commit `25b1c5c` adds standalone packaging, a portable Core project override, presentation and retirement guidance. The candidate was built from that clean source with .NET SDK 10.0.112 and tested on Windows .NET 10.0.13.

- Payload validation: 21 checks pass.
- Native Windows clipboard/Harmony validation: 14 checks pass.
- All Release builds: zero warnings/errors.
- Catalog tooling pinned at `23d5bf4ee17bfc6bf467a97156846cd48de6b450` accepts the new exact layout, preserves all historical approvals, validates repository-owned content and verifies the finished archive.
- The exact packaged DLL then passed native Copy All/Paste All in a disposable, profile-redirected Designer with two Gains in Emulate. Copy-back proved destination Mute changed from muted to unmuted. Wrong component class and plain text disabled paste. Better Shortcuts was absent.

The release-candidate run uses native commands programmatically; the earlier prototype's mouse acceptance is recorded separately in `PROTOTYPE-VALIDATION.md`. No new claim of physical Core or other component acceptance is made.

```text
Private module SHA-256:
3b275548525474f5aa891f7a9e77511ead0221be7e4acb1e5b147564dc27fc81

clipboard-compat-0.1.0.qdsmod SHA-256:
a474df87d02f3af5af640b062f525900a792227723c5f9682bcc35b2286717be
```

The archive contains one private module and a schema-3 manifest requiring Mods Core >=0.1.5 and <0.2.0. It does not contain Core, Harmony, vendor assemblies, the test fixture or startup hooks. Raw native receipts and profile-preservation evidence remain local.
