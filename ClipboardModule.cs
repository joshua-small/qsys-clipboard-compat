using System.Reflection;
using System.Windows;
using QsysMods.Core;

namespace Designer.Modules.ClipboardCompat;

public sealed class ClipboardModule : ModModule
{
    private ClipboardHooks? hooks;
    public ClipboardModule() => Mods.Register("clipboard-compat", main =>
    {
        // Core verifies exact Designer + ModuleCommon hashes before this initializer.
        // Only these three native command methods are patched; no global clipboard hooks.
        hooks = new(Host.Type("QSC.QSys.Commands.CopyControlSet").Assembly, Host.Type("QSC.Common.Clipboard").Assembly);
        try { Commands.Register(new("clipboard-compat/status", "Clipboard Compatibility status…", "Clipboard Compatibility",
            () => System.Windows.MessageBox.Show(main,
                "Temporary Copy All / Paste All compatibility is active for Designer 10.5.0.\n\nTo disable: close Designer, remove this module through its installation mechanism, then reopen Designer. Copy again after enabling or disabling. Native commands and shortcut bindings are unchanged.",
                "Clipboard Compatibility"), () => true, InMenu: true)); }
        catch { hooks.Dispose(); throw; }
        Host.Receipt("clipboard-compat", "ready", new { version = GetType().Assembly.GetName().Version?.ToString(), patched = hooks.Targets });
    });
}
