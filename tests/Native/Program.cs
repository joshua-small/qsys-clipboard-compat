using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Designer.Modules.ClipboardCompat;
using HarmonyLib;
using Forms = System.Windows.Forms;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "read") throw new ArgumentException("Pass the Designer directory and optional read mode.");
        string directory = Path.GetFullPath(args[0]);
        foreach (var (file, hash) in new[] {
            ("Q-Sys Designer.dll", "0E7150F8DC02E0159E019188780FB003CE025F5EC18C491609CF337D849BDA60"),
            ("ModuleCommon.dll", "A6D1ADDDA06BDCB1AD32B57C7480C947210C3D3AD287F5818C2EA64068BF5AEE") })
        { using var input = File.OpenRead(Path.Combine(directory, file)); if (Convert.ToHexString(SHA256.HashData(input)) != hash) throw new Exception("Unsupported host"); }
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            var path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        Run(directory, args.Length == 2);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run(string directory, bool readOnly)
    {
        int count = 0;
        void Check(bool good, string name) { if (!good) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var host = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "Q-Sys Designer.dll"));
        var common = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "QscCommon.dll"));
        var type = host.GetType("QSC.QSys.Commands.CopyControlSet", true)!;
        var native = Activator.CreateInstance(type)!;
        type.GetProperty("ClassName")!.SetValue(native, "gain");
        type.GetProperty("Controls")!.SetValue(native, new Dictionary<string, string> { ["mute"] = "0", ["gain"] = "-12.5" });
        var hooks = new ClipboardHooks(host, common);
        if (readOnly)
        {
            var persisted = ClipboardBridge.Read(ClipboardBridge.NativeFormat) ?? throw new Exception("Clipboard did not survive producer process exit");
            Console.WriteLine(JsonSerializer.Serialize(new { persistedAcrossProcess = true, payload = persisted }));
            hooks.Dispose(); return;
        }
        Check(hooks.Targets.Length == 3, "Only three command methods patched");
        ClipboardBridge.Write(ClipboardBridge.NativeFormat, native, true);
        var read = ClipboardBridge.Read(ClipboardBridge.NativeFormat);
        Check(read?.GetType() == type && (string)type.GetProperty("ClassName")!.GetValue(read)! == "gain", "Native object reconstructed");
        Check(((Dictionary<string,string>)type.GetProperty("Controls")!.GetValue(read)!).SequenceEqual((Dictionary<string,string>)type.GetProperty("Controls")!.GetValue(native)!), "All control values preserved");
        Check(ClipboardBridge.Read("other-format") is null, "Unrelated format not consumed");
        bool rejected = false; try { ClipboardBridge.Write("other-format", native, true); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Unexpected write refuses to replace clipboard");
        Check(ClipboardBridge.Read(ClipboardBridge.NativeFormat) is not null, "Rejected write preserved existing clipboard");
        foreach (var invalid in new object[] { "invalid JSON", """{"schema":1,"className":"gain","controls":{"mute":null}}""", 7 })
        {
            var data = new Forms.DataObject(); data.SetData(ClipboardBridge.JsonFormat, false, invalid); Forms.Clipboard.SetDataObject(data, true);
            Check(ClipboardBridge.Read(ClipboardBridge.NativeFormat) is null, "Invalid clipboard refused");
        }
        Forms.Clipboard.SetText("clipboard-control");
        Check(Forms.Clipboard.GetText() == "clipboard-control" && ClipboardBridge.Read(ClipboardBridge.NativeFormat) is null, "Plain text unaffected and invalidates component paste");
        var paste = host.GetType("QSC.QSys.Commands.PasteControls", true)!;
        Check(paste.GetMethod("CustomRun", Type.EmptyTypes)!.Invoke(Activator.CreateInstance(paste), null) is false, "Native execution safely refuses clipboard replaced after menu availability");
        var copy = common.GetType("QSC.Common.Clipboard", true)!;
        copy.GetMethod("SetData")!.Invoke(null, ["unrelated-test", "native-string", true]);
        Check(Forms.Clipboard.TryGetData<string>("unrelated-test", out var text) && text == "native-string", "Native unrelated clipboard format unchanged");
        bool conflict = false; try { _ = new ClipboardHooks(host, common); } catch (InvalidOperationException) { conflict = true; }
        Check(conflict, "Existing hook owner rejected");
        hooks.Dispose();
        Check(!Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)?.Owners.Contains(ClipboardHooks.Owner) == true), "All compatibility hooks removed");
        ClipboardBridge.Write(ClipboardBridge.NativeFormat, native, true);
        Console.WriteLine(JsonSerializer.Serialize(new { passed = count, runtime = Environment.Version.ToString(), physicalInputTested = false, note = "STA native-assembly transport and patch tests; no Designer process or physical Core" }));
    }
}
