using System.Text.Json;

namespace Designer.Modules.ClipboardCompat;

public sealed record ControlPayload(string ClassName, Dictionary<string, string> Controls);

public static class Payload
{
    public const int MaxJsonLength = 1_048_576, MaxControls = 4096, MaxValueLength = 16384;
    public static string Encode(ControlPayload value)
    {
        Validate(value);
        string json = JsonSerializer.Serialize(new { schema = 1, className = value.ClassName, controls = value.Controls });
        if (json.Length > MaxJsonLength) throw new ArgumentException("Component control data exceeds the clipboard limit.");
        return json;
    }
    public static ControlPayload? Decode(string? json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaxJsonLength) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!fields.Add(property.Name) || property.Name is not ("schema" or "className" or "controls")) return null;
            if (fields.Count != 3 || !root.GetProperty("schema").TryGetInt32(out int schema) || schema != 1) return null;
            var controls = root.GetProperty("controls");
            if (controls.ValueKind != JsonValueKind.Object) return null;
            var result = new ControlPayload(root.GetProperty("className").GetString()!, new(StringComparer.Ordinal));
            foreach (var control in controls.EnumerateObject())
            {
                if (result.Controls.Count >= MaxControls || control.Value.ValueKind != JsonValueKind.String ||
                    !result.Controls.TryAdd(control.Name, control.Value.GetString()!)) return null;
            }
            Validate(result);
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }
    private static void Validate(ControlPayload value)
    {
        if (!ValidName(value.ClassName, 256) || value.Controls is null || value.Controls.Count is < 1 or > MaxControls)
            throw new ArgumentException("Invalid component control payload.");
        foreach (var pair in value.Controls)
            if (!ValidName(pair.Key, 512) || pair.Value is null || pair.Value.Length > MaxValueLength || pair.Value.Contains('\0'))
                throw new ArgumentException("Invalid control name or value.");
    }
    private static bool ValidName(string? text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Contains('\0');
}
