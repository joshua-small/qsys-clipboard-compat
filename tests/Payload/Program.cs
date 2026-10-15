using Designer.Modules.ClipboardCompat;
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
Console.WriteLine($"Passed {checks} clipboard payload checks.");
