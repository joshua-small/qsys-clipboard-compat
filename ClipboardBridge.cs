using System.Reflection;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace Designer.Modules.ClipboardCompat;

public static class ClipboardBridge
{
    public const string NativeFormat = "QSys.CopyControls";
    public const string JsonFormat = "QsysMods.ComponentControls.Json.v1";
    private static Type? payloadType;
    private static PropertyInfo? className, controls;
    [ThreadStatic] internal static object? PasteSnapshot;
    public static object? ReadForPaste(string key) => key == NativeFormat ? PasteSnapshot : null;

    internal static void Bind(Type type)
    {
        var c = type.GetProperty("ClassName");
        var v = type.GetProperty("Controls");
        if (type.FullName != "QSC.QSys.Commands.CopyControlSet" || c?.PropertyType != typeof(string) ||
            v?.PropertyType != typeof(Dictionary<string, string>) || type.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException("Unexpected native component clipboard contract.");
        payloadType = type; className = c; controls = v;
    }
    public static void Write(string key, object value, bool copy)
    {
        if (key != NativeFormat || value.GetType() != payloadType || !copy)
            throw new InvalidOperationException("Unexpected native component clipboard write.");
        string json = Payload.Encode(new((string)className!.GetValue(value)!, (Dictionary<string, string>)controls!.GetValue(value)!));
        // A plain string under a private format requires no arbitrary-object deserializer.
        // Persistent copy supports another patched Designer process and process exit.
        var data = new Forms.DataObject();
        data.SetData(JsonFormat, false, json);
        Forms.Clipboard.SetDataObject(data, true, 10, 20);
    }
    public static object? Read(string key)
    {
        if (key != NativeFormat || payloadType is null) return null;
        try
        {
            if (!Forms.Clipboard.TryGetData<string>(JsonFormat, out var json) || Payload.Decode(json) is not { } value) return null;
            var native = Activator.CreateInstance(payloadType)!;
            className!.SetValue(native, value.ClassName);
            controls!.SetValue(native, value.Controls);
            return native;
        }
        catch (Exception ex) when (ex is ExternalException or System.Runtime.Serialization.SerializationException or
            NotSupportedException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        { return null; }
    }
}
