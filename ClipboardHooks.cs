using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Designer.Modules.ClipboardCompat;

public sealed class ClipboardHooks : IDisposable
{
    public const string Owner = "qsys.mods.clipboard-compat";
    private readonly Harmony harmony = new(Owner);
    private readonly MethodInfo[] targets;
    private static Type? nativePayload;
    private static MethodInfo? nativeWrite, nativeRead;
    private static MethodInfo? copyMoveNext, pasteCan, pasteRun;

    public ClipboardHooks(Assembly designer, Assembly common)
    {
        Type T(string name) => designer.GetType("QSC.QSys.Commands." + name, true)!;
        var clipboard = common.GetType("QSC.Common.Clipboard", true)!;
        nativePayload = T("CopyControlSet");
        ClipboardBridge.Bind(nativePayload);
        nativeWrite = clipboard.GetMethod("SetData", [typeof(string), typeof(object), typeof(bool)])!;
        nativeRead = clipboard.GetMethods().Single(m => m.Name == "GetData" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(nativePayload);
        var copy = T("CopyControls").GetMethod("_Run", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!;
        var state = copy.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("Native Copy All async state machine changed.");
        copyMoveNext = state.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
        pasteCan = T("PasteControls").GetMethod("Can", Type.EmptyTypes)!;
        pasteRun = T("PasteControls").GetMethod("CustomRun", Type.EmptyTypes)!;
        targets = [copyMoveNext, pasteCan, pasteRun];
        foreach (var target in targets)
        {
            if (Harmony.GetPatchInfo(target)?.Owners.Any() == true)
                throw new InvalidOperationException("Another patch owns " + target.DeclaringType?.FullName + "." + target.Name);
            var expected = target == copyMoveNext ? nativeWrite : nativeRead;
            if (PatchProcessor.GetOriginalInstructions(target).Count(i => i.Calls(expected)) != 1)
                throw new InvalidOperationException("Native clipboard call shape changed: " + target.Name);
        }
        try
        {
            foreach (var target in targets)
            {
                try { harmony.Patch(target,
                    prefix: target == pasteRun ? new HarmonyMethod(typeof(ClipboardHooks), nameof(BeforePaste)) : null,
                    transpiler: new HarmonyMethod(typeof(ClipboardHooks), nameof(ReplaceTransport)),
                    finalizer: target == pasteRun ? new HarmonyMethod(typeof(ClipboardHooks), nameof(AfterPaste)) : null); }
                catch (Exception ex) { throw new InvalidOperationException("Could not patch " + target.DeclaringType?.FullName + "." + target.Name, ex); }
            }
        }
        catch { Dispose(); throw; }
    }
    public string[] Targets => targets.Select(m => m.DeclaringType!.FullName + "." + m.Name).ToArray();
    public void Dispose() => harmony.UnpatchAll(Owner);

    private static bool BeforePaste(ref bool __result, out object? __state)
    {
        __state = ClipboardBridge.PasteSnapshot;
        ClipboardBridge.PasteSnapshot = ClipboardBridge.Read(ClipboardBridge.NativeFormat);
        if (ClipboardBridge.PasteSnapshot is not null) return true;
        __result = false; return false;
    }
    private static void AfterPaste(object? __state) => ClipboardBridge.PasteSnapshot = __state;
    private static IEnumerable<CodeInstruction> ReplaceTransport(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(nativeWrite))
            {
                instruction.operand = typeof(ClipboardBridge).GetMethod(nameof(ClipboardBridge.Write));
                yield return instruction;
            }
            else if (instruction.Calls(nativeRead))
            {
                instruction.operand = typeof(ClipboardBridge).GetMethod(__originalMethod == pasteRun ? nameof(ClipboardBridge.ReadForPaste) : nameof(ClipboardBridge.Read));
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Castclass, nativePayload);
            }
            else yield return instruction;
        }
    }
}
