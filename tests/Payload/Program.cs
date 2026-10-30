using Designer.Modules.ClipboardCompat;
using System.Text.Json;
int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var payload = new ControlPayload("gain", new() { ["mute"] = "0", ["gain"] = "-12.5", ["label"] = "a\n\"b\" ü" });
var decoded = Payload.Decode(Payload.Encode(payload));
Check(decoded?.ClassName == "gain" && decoded.Controls.SequenceEqual(payload.Controls), "Valid data round trips without loss");
foreach (var invalid in new[] {
 "", "null", "[]", "{}", "{", """{"schema":2,"className":"gain","controls":{"mute":"0"}}""",
 """{"schema":1,"className":"gain","controls":{"mute":"0"},"$type":"System.Object"}""",
 """{"schema":1,"schema":1,"className":"gain","controls":{"mute":"0"}}""",
 """{"schema":1,"className":"gain","controls":{"mute":"0","mute":"1"}}""",
 """{"schema":1,"className":null,"controls":{"mute":"0"}}""",
 """{"schema":1,"className":"gain","controls":{"mute":null}}""",
 """{"schema":1,"className":"gain","controls":{"mute":true}}""",
 """{"schema":1,"className":"gain","controls":{}}""",
 """{"schema":"1","className":"gain","controls":{"mute":"0"}}""",
 new string('x', Payload.MaxJsonLength + 1) })
 Check(Payload.Decode(invalid) is null, "Reject invalid payload");
foreach (var bad in new[] {
 new ControlPayload("", new() { ["mute"] = "0" }),
 new ControlPayload("gain", new() { [""] = "0" }),
 new ControlPayload("gain", new() { ["mute"] = new string('x', Payload.MaxValueLength + 1) }),
 new ControlPayload("gain", Enumerable.Range(0, Payload.MaxControls + 1).ToDictionary(x => x.ToString(), _ => "0")),
 new ControlPayload("gain", Enumerable.Range(0, 100).ToDictionary(x => x.ToString(), _ => new string('x', Payload.MaxValueLength))) })
{
 bool rejected = false; try { Payload.Encode(bad); } catch (ArgumentException) { rejected = true; }
 Check(rejected, "Reject excessive or invalid output");
}
// Exercise the linked production codec at each declared boundary. These tests
// operate only on synthetic strings and dictionaries, never the OS clipboard.
void RoundTrip(ControlPayload value, string name)
{
    var actual = Payload.Decode(Payload.Encode(value));
    Check(actual is not null && actual.ClassName == value.ClassName &&
        actual.Controls.Count == value.Controls.Count &&
        value.Controls.All(pair => actual.Controls.TryGetValue(pair.Key, out var text) && text == pair.Value), name);
}
void RejectEncode(ControlPayload value, string name)
{
    bool rejected = false;
    try { Payload.Encode(value); } catch (ArgumentException) { rejected = true; }
    Check(rejected, name);
}
string Json(ControlPayload value) => JsonSerializer.Serialize(new { schema = 1, className = value.ClassName, controls = value.Controls });
void RejectBoth(ControlPayload value, string name)
{
    RejectEncode(value, name + " on encode");
    Check(Payload.Decode(Json(value)) is null, name + " on decode");
}
ControlPayload Single(string className = "gain", string key = "mute", string value = "0") =>
    new(className, new(StringComparer.Ordinal) { [key] = value });

RoundTrip(Single(className: new string('c', 256)), "Class name at 256 characters");
RoundTrip(Single(key: new string('k', 512)), "Control name at 512 characters");
RoundTrip(Single(value: new string('v', Payload.MaxValueLength)), "Value at maximum length");
RoundTrip(Single(value: ""), "Empty control value is preserved");
RoundTrip(new("gain", Enumerable.Range(0, Payload.MaxControls).ToDictionary(i => $"c{i}", _ => "0")),
    "Exactly maximum control count");
RoundTrip(new("Gain", new(StringComparer.Ordinal) { ["mute"] = "0", ["Mute"] = "1", ["label"] = "音 🎚\r\n\t\\\"" }),
    "Ordinal key distinctions, class case, and Unicode values are preserved");

RejectBoth(Single(className: new string('c', 257)), "Class name above maximum");
RejectBoth(Single(key: new string('k', 513)), "Control name above maximum");
RejectBoth(Single(value: new string('v', Payload.MaxValueLength + 1)), "Value above maximum");
RejectBoth(new("gain", Enumerable.Range(0, Payload.MaxControls + 1).ToDictionary(i => $"c{i}", _ => "0")),
    "Control count above maximum");
foreach (var name in new[] { "", " \t\r\n", "nul\0name" })
{
    RejectBoth(Single(className: name), $"Invalid class name {JsonSerializer.Serialize(name)}");
    RejectBoth(Single(key: name), $"Invalid control name {JsonSerializer.Serialize(name)}");
}
RejectBoth(Single(value: "nul\0value"), "Embedded NUL in value");
RejectBoth(Single(value: null!), "Null value");
RejectBoth(new("gain", null!), "Null controls dictionary");

// ASCII value data lets the independent serialized fixture hit the total JSON
// limit exactly without exceeding the per-value limit.
var totalLimit = new ControlPayload("gain",
    Enumerable.Range(0, 64).ToDictionary(i => $"c{i}", _ => new string('v', Payload.MaxValueLength)));
int excess = Json(totalLimit).Length - Payload.MaxJsonLength;
Check(excess > 0 && excess < Payload.MaxValueLength, "Total-length fixture has an adjustable overflow");
totalLimit.Controls["c0"] = new string('v', Payload.MaxValueLength - excess);
string atLimit = Json(totalLimit);
Check(atLimit.Length == Payload.MaxJsonLength, "Fixture is exactly at the total JSON limit");
Check(Payload.Encode(totalLimit).Length == Payload.MaxJsonLength, "Encode permits the exact total JSON limit");
RoundTrip(totalLimit, "Exact total JSON limit round trips");
totalLimit.Controls["c0"] += "v";
Check(Json(totalLimit).Length == Payload.MaxJsonLength + 1, "Fixture is one character above the total JSON limit");
RejectBoth(totalLimit, "Total JSON length above maximum");

Check(Payload.Decode(null) is null, "Null JSON is rejected");
foreach (var (json, name) in new (string, string)[] {
    ("""{"schema":1,"className":"gain","controls":{"mute":"0"},"class\u004eame":"gain"}""", "Escaped duplicate root field"),
    ("""{"schema":1,"className":"gain","controls":{"mute":"0","m\u0075te":"1"}}""", "Escaped duplicate control key"),
    ("""{"schema":1,"className":"gain","controls":{"mute":0}}""", "Numeric control value"),
    ("""{"schema":1,"className":"gain","controls":{"mute":[]}}""", "Array control value"),
    ("""{"schema":1,"className":"gain","controls":{"mute":{}}}""", "Object control value"),
    ("""{"schema":1,"className":42,"controls":{"mute":"0"}}""", "Numeric class name"),
    ("""{"schema":1,"className":"gain","controls":[]}""", "Array controls"),
    ("""{"schema":null,"className":"gain","controls":{"mute":"0"}}""", "Null schema"),
    ("""{"schema":2147483648,"className":"gain","controls":{"mute":"0"}}""", "Schema outside Int32"),
    ("""{"schema":1,"controls":{"mute":"0"}}""", "Missing class name"),
    ("""{"schema":1,"className":"gain"}""", "Missing controls"),
    ("""{"className":"gain","controls":{"mute":"0"}}""", "Missing schema"),
    ("""{"Schema":1,"className":"gain","controls":{"mute":"0"}}""", "Wrong root field case"),
    ("""{"schema":1,"className":"gain","controls":{"mute":{"a":{"b":{"c":"0"}}}}}""", "Excessive nesting"),
    ("""{"schema":1,"className":"gain","controls":{"mute":"0",}}""", "Trailing comma"),
    ("""{"schema":1,/*comment*/"className":"gain","controls":{"mute":"0"}}""", "JSON comment"),
    ("""{"schema":1,"className":"gain","controls":{"mute":"0"}} {}""", "Trailing JSON document"),
})
    Check(Payload.Decode(json) is null, name + " is rejected");
Check(Payload.Decode("""{"controls":{"mute":"0"},"className":"gain","schema":1}""")?.Controls["mute"] == "0",
    "Root field order does not matter");
Console.WriteLine($"Passed {checks} clipboard payload checks.");
