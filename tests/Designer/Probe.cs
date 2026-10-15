using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Designer.Modules.ClipboardCompat;
using QsysMods.Core;

namespace Designer.Modules.ClipboardLab;

public sealed class Probe : ModModule
{
    private static readonly string Output = Path.Combine(AppContext.BaseDirectory, "clipboard-evidence");
    private static object? Get(object? value, string name) => value?.GetType().GetProperty(name, Host.Instance)?.GetValue(value) ?? value?.GetType().GetField(name, Host.Instance)?.GetValue(value);
    private static object? Call(object value, string name, params object?[] args) => value.GetType().GetMethods(Host.Instance).Single(m =>
        m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(x=>x)).Invoke(value,args);
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Visual(VisualTreeHelper.GetChild(root,i))) yield return child; }
    private static object[] Items(object? value) => value is IEnumerable list ? list.Cast<object>().ToArray() : [];
    private static async Task Until(Func<bool> ready)
    { for(int i=0;i<80;i++) { if(ready()) return; await Task.Delay(250); } throw new TimeoutException("Clipboard lab timed out."); }
    private static void Save(string name, object value) => File.WriteAllText(Path.Combine(Output,name+".json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented=true }));
    private static RoutedCommand Command(string name) => (RoutedCommand)Host.Type("QSC.QSys.Commands."+name).GetField("Command",Host.Static)!.GetValue(null)!;
    private static void Select(object graphic)
    { var s=HostSelection.GlobalSelection; Host.Call(s,"Clear"); Call(s,"Add",graphic,false); Host.Call(s,"OnSelectionChanged"); }
    public Probe()
    {
        if (!Host.IsLab || !File.Exists(Path.Combine(AppContext.BaseDirectory,".settings-live-lab.json"))) return;
        Mods.Register("clipboard-lab", main =>
        {
            if (!((string)Host.Type("QSC.Common.SettingsManager").GetProperty("Path",Host.Static)!.GetValue(null)!).StartsWith(Path.Combine(AppContext.BaseDirectory,"profile"),StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Profile not isolated");
            Directory.CreateDirectory(Output);
            main.Dispatcher.BeginInvoke(new Action(async () => {
                try { await Until(()=>Mods.IsReady("clipboard-compat") && main.IsEnabled); await Task.Delay(2000); await Run(main); }
                catch(Exception e) { Save("result", new { passed=false,error=e.ToString() }); }
            }), DispatcherPriority.ContextIdle);
        });
    }
    private static async Task Run(Window main)
    {
        if (!main.Title.StartsWith("untitled",StringComparison.OrdinalIgnoreCase)) throw new Exception("Requires new synthetic document");
        var manager=Host.Type("QSC.QSys.App").GetField("Manager",Host.Static)!.GetValue(null)!;
        var core=Get(Get(manager,"Document"),"Core")!; core.GetType().GetProperty("Name")!.SetValue(core,"");
        var canvas=Visual(main).OfType<FrameworkElement>().Single(e=>e.IsVisible&&e.GetType().FullName=="QSC.QSys.Graphic.GraphicCanvas");
        var creator=Activator.CreateInstance(Host.Type("QSC.QSys.Drag.ComponentCreator`1").MakeGenericType(Host.Type("QSC.QSys.Design.Component")))!;
        creator.GetType().GetProperty("ClassName")!.SetValue(creator,"gain");
        var op=Call(manager,"StartOperation");
        for(int i=0;i<2;i++)
        {
            var model=Call(creator,"Create",new Point(200+i*300,180))!;
            model.GetType().GetProperty("CodeName")!.SetValue(model,i==0?"ClipboardSource":"ClipboardTarget");
            Call(Get(Get(canvas,"Root"),"CurrentLayer")!,"AddChild",model); Host.Call(model,"Attach");
        }
        Call(manager,"EndOperation",op);
        await Task.Delay(300);
        var graphics=Items(Get(canvas,"AllGraphicObjects")).Where(g=>Get(Get(g,"obj"),"CodeName") is string n&&n.StartsWith("Clipboard")).OrderBy(g=>(string)Get(Get(g,"obj"),"CodeName")!).ToArray();
        if(graphics.Length!=2) throw new Exception("Two Gain fixtures required");
        var connection=Host.Type("QSC.QSys.ControlConnection.Manager");
        Command("Emulate").Execute(null,main);
        await Until(()=>connection.GetProperty("IsEmulator",Host.Static)!.GetValue(null) is true && Get(connection.GetProperty("Status",Host.Static)!.GetValue(null),"IsConnected") is true);
        await Task.Delay(500);
        void Mute(int i,string value)
        {
            var id=Get(Get(graphics[i],"obj"),"Id")!;
            var controlId=connection.GetMethod("GetControlID",Host.Static,null,[id.GetType(),typeof(string)],null)!.Invoke(null,[id,"mute"]);
            connection.GetMethod("SetControlValue",Host.Static,null,[typeof(string),typeof(float)],null)!.Invoke(null,[controlId,value=="1"?1f:0f]);
        }
        async Task<object> Copy(int i)
        {
            System.Windows.Clipboard.SetText("clipboard-lab-reset");
            Select(graphics[i]); canvas.Focus();
            if(!Command("CopyControls").CanExecute(null,main)) throw new Exception("Native Copy All unavailable");
            Command("CopyControls").Execute(null,main);
            await Until(()=>ClipboardBridge.Read(ClipboardBridge.NativeFormat) is not null);
            return ClipboardBridge.Read(ClipboardBridge.NativeFormat)!;
        }
        Mute(0,"0"); Mute(1,"1"); await Task.Delay(300);
        var targetBefore=await Copy(1);
        var source=await Copy(0);
        string MuteValue(object value)=>(string)((Dictionary<string,string>)Get(value,"Controls")!)["mute"];
        Save("fixture-values",new {source,targetBefore});
        if(MuteValue(source)==MuteValue(targetBefore))throw new Exception("Distinct fixture values not confirmed");
        Select(graphics[1]); canvas.Focus(); CommandManager.InvalidateRequerySuggested();
        bool enabled=Command("PasteControls").CanExecute(null,main);
        if(!enabled)throw new Exception("Native Paste All remains disabled");
        Command("PasteControls").Execute(null,main); await Task.Delay(300);
        var after=await Copy(1);
        if(MuteValue(after)!=MuteValue(source))throw new Exception("Native Paste All did not transfer Mute");
        // Native same-class filtering is retained.
        var wrong=Activator.CreateInstance(source.GetType())!;
        source.GetType().GetProperty("ClassName")!.SetValue(wrong,"not-a-gain");
        source.GetType().GetProperty("Controls")!.SetValue(wrong,new Dictionary<string,string>{{"mute","1"}});
        ClipboardBridge.Write(ClipboardBridge.NativeFormat,wrong,true);
        if(Command("PasteControls").CanExecute(null,main))throw new Exception("Wrong component class accepted");
        System.Windows.Clipboard.SetText("clipboard-lab-reset");
        if(Command("PasteControls").CanExecute(null,main))throw new Exception("Text clipboard accepted");
        Mute(0,"0"); Mute(1,"1"); await Task.Delay(300);
        Host.Call(HostSelection.GlobalSelection,"Clear"); Host.Call(HostSelection.GlobalSelection,"OnSelectionChanged");
        Save("result",new { passed=true,physicalInputTested=false,nativeCopy=true,nativePasteEnabled=enabled,sourceMute=MuteValue(source),targetBefore=MuteValue(targetBefore),targetAfter=MuteValue(after),wrongClassRejected=true,textClipboardRejected=true,hostState=HostSelection.ReadHostState(),readyForMouse=true });
        var timer=new DispatcherTimer(DispatcherPriority.ContextIdle){Interval=TimeSpan.FromMilliseconds(500)};
        timer.Tick+=(_,_)=>{
            try {
                var roots=PresentationSource.CurrentSources.Cast<PresentationSource>().Select(s=>s.RootVisual).OfType<DependencyObject>();
                Save("state",new { time=DateTime.UtcNow,hostState=HostSelection.ReadHostState(),clipboard=ClipboardBridge.Read(ClipboardBridge.NativeFormat),
                    selected=Items(HostSelection.GlobalSelection).Select(g=>Get(Get(g,"obj"),"CodeName")),
                    buttons=roots.SelectMany(Visual).OfType<ButtonBase>().Where(b=>b.Command is RoutedCommand c&&c.Name.Contains("Component Controls")).Select(b=>new { command=((RoutedCommand)b.Command).Name,b.IsEnabled,b.IsVisible,tooltip=b.ToolTip?.ToString() }) });
            }catch(Exception e){Save("observer-error",new {error=e.ToString()});}
        }; timer.Start();
    }
}
