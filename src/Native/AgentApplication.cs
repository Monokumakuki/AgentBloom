using System.Windows.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ScreenLine {
 internal sealed class AgentSession : IDisposable {
  internal readonly AgentProjects Projects;
  internal DesktopController Controller;internal AgentHub Hub;internal AgentBoard Board;internal AgentBookmark Bookmark;
  readonly HashSet<string> knownShots=new HashSet<string>();bool loading;
  internal IEnumerable<CaptureItem> History {get{return Controller.Gallery.Collected.Where(i=>!Projects.Note(AgentProjects.ShotId(i.Path)).Archived);}}
  internal IEnumerable<CaptureItem> VisibleShots {get{return History.Where(i=>Projects.Current.Id==Projects.Inbox.Id||Projects.Note(AgentProjects.ShotId(i.Path)).Category==Projects.Current.Id);}}
  internal bool Busy;internal event Action Changed;
  internal AgentSession(string root){Projects=new AgentProjects(root);}
  internal async Task Start(){Projects.UnifyInbox();await Load();Hub=new AgentHub(this);Hub.Show();Hub.SyncVisibility(false);Bookmark=new AgentBookmark(this);Bookmark.Show();Controller.Shelf.ExposureChanged+=delegate(bool shown){Hub.SyncVisibility(shown);Notify();};AgentStartup.ConfigureDefault();if(!AgentApplication.Startup)ShowBoard();}
  async Task Load(){
   AgentFaces.Quiet=Projects.Index.Quiet;
   AgentFaces.Decoration=Projects.Index.SelectedAgent;
   var project=Projects.Inbox;
   AgentFaces.Resolve=path=>Array.IndexOf(AgentProjects.AgentNames,Projects.Note(project,AgentProjects.ShotId(path)).Agent);
   var store=new StateStore(Projects.Folder(Projects.Inbox));store.State.Images.Clear();store.Save();
   foreach(string path in store.State.Collection)knownShots.Add(AgentProjects.ShotId(path));
   Controller=new DesktopController(store,AgentApplication.Qa?Path.Combine(Projects.Root,"isolated-external-capture-watch"):WindowsNative.ScreenshotFolder(),false);
   Controller.Gallery.Changed+=GalleryChanged;
   Controller.Shelf.BasketRequested+=ShowBoard;
   Controller.CollectionRequested+=ShowBoard;
   Controller.AppearanceChanged+=Notify;
   Controller.ShortcutRequested+=ShowShortcutSettings;
   loading=true;try{await Controller.Start();}finally{loading=false;}Notify();
  }
  void GalleryChanged(){bool changed=false;foreach(var item in Controller.Gallery.Collected){string id=AgentProjects.ShotId(item.Path);var note=Projects.Note(id);if(knownShots.Add(id)&&!loading){note.Agent=Projects.Index.SelectedAgent;changed=true;}if(note.Archived&&Controller.Gallery.Contains(item.Path)){note.Archived=false;changed=true;}}if(changed)Projects.Save();Notify();}
  internal void Notify(){if(Changed!=null)Changed();}
  internal void ShowBoard(){if(AgentApplication.Capturing)return;if(Board==null){Board=new AgentBoard(this);Board.Closed+=delegate{Board=null;};}else if(Board.DrawerOpen){Board.FocusBoard();return;}Board.ShowDrawer();}
  internal void ToggleBoard(){if(Board!=null&&Board.DrawerOpen)Board.HideDrawer();else ShowBoard();}
  void ShowShortcutSettings(){if(AgentApplication.Capturing)return;ShowBoard();if(Board!=null)Board.OpenShortcutSettings();}
  internal Task Switch(string id){
   if(Busy||Projects.Index.Active==id)return Task.FromResult(0);
   if(!Projects.Index.Projects.Any(p=>p.Id==id))throw new ArgumentException("找不到这个项目。");
   string previous=Projects.Index.Active;Projects.Index.Active=id;try{Projects.Save();}catch{Projects.Index.Active=previous;throw;}Notify();return Task.FromResult(0);
  }
  internal async Task Create(string name){
   if(Busy)throw new InvalidOperationException("请先完成当前截图或导入。");
   string previous=Projects.Index.Active;var p=Projects.Create(name);Projects.Index.Active=previous;Projects.Save();await Switch(p.Id);
  }
  internal CaptureItem Find(string id){var item=Controller.Gallery.Collected.FirstOrDefault(i=>AgentProjects.ShotId(i.Path)==id);if(item==null)throw new ArgumentException("这张图片已不在当前项目。");return item;}
  internal async Task Import(){
   if(Busy)return;var dialog=new OpenFileDialog{Multiselect=true,Title="把图片收进收集箱",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"};
   if(dialog.ShowDialog()!=true)return;Busy=true;Notify();try{foreach(var file in dialog.FileNames)if(!await Controller.Captures.Import(file,null,true))throw new IOException("无法导入："+Path.GetFileName(file));}finally{Busy=false;Notify();}
  }
  internal async Task Capture(){
   if(Busy)return;Busy=true;Notify();IntPtr focus=WindowsNative.GetForegroundWindow();bool reopen=Board!=null&&Board.DrawerOpen;
   string temp=null,partial=null;CaptureItem arrival=null;Rect arrivalRegion=Rect.Empty;BitmapSource arrivalPicture=null;try{
    if(reopen)await Board.PrepareCaptureSurface();
    using(var presentation=new CapturePresentation(this)){
     var captured=await AgentRegionCapture.TakeAsync(delegate{if(reopen)Board.CollapseForCapture();presentation.AboveSelector();},delegate{presentation.RestoreBoardLayer();if(reopen)Board.ShowDrawer(true);});if(captured==null)return;
     temp=Path.Combine(Controller.Gallery.Store.Inbox,Guid.NewGuid().ToString("N")+".png");partial=temp+".partial";
     var area=captured.Bounds;
     arrivalRegion=new Rect(area.X,area.Y,area.Width,area.Height);
     byte[] png=null;BitmapSource fullImg=null;ImageContent prepared=null;
     using(captured)await Task.Run(delegate{
      using(var stream=new MemoryStream()){captured.Picture.Save(stream,System.Drawing.Imaging.ImageFormat.Png);png=stream.ToArray();}
      Directory.CreateDirectory(Path.GetDirectoryName(temp));File.WriteAllBytes(partial,png);File.Move(partial,temp);
      prepared=ImageContent.Decode(png,out fullImg);
     });
     arrivalPicture=fullImg;
     if(!await Controller.Captures.Import(temp,null,false,item=>{arrival=item;Controller.Shelf.PrepareCaptureFlight(item);},prepared))throw new IOException("截图没有保存成功，请重试。");
     if(arrival!=null){
      try{
       var payload=new DataObject();payload.SetImage(fullImg);payload.SetData("PNG",new MemoryStream(png));
       await WindowsClipboard.Write(payload);Controller.Captures.IgnoreCurrentClipboard();
      }catch(Exception error){Controller.Gallery.Store.Log(error);}
     }
    }
   }finally{
    if(partial!=null&&File.Exists(partial))File.Delete(partial);
    if(temp!=null&&File.Exists(temp)&&arrival!=null&&!FileRules.SameFile(temp,arrival.Path))File.Delete(temp);
    if(reopen&&Board!=null)Board.ShowDrawer(true);
    if(!reopen&&focus!=IntPtr.Zero)WindowsNative.SetForegroundWindow(focus);Busy=false;Notify();
   }
   // Finish clipboard and capture-layer restoration before starting the visible flight.
   if(arrival!=null){await Application.Current.Dispatcher.InvokeAsync(new Action(()=>Controller.Shelf.UpdateLayout()),DispatcherPriority.Render);Controller.Shelf.ShowNewCapture(arrival,arrivalRegion,arrivalPicture);}
  }
  internal async Task Pin(CaptureItem item){
   if(!Controller.Gallery.Contains(item.Path))Controller.Gallery.Add(item.Path,item.Image);
   Controller.Shelf.Reveal(true,true,3);await Controller.Shelf.PinFromBoard(item);
  }
  internal async Task<bool> Export(){
   if(Busy)return false;
   string folder=AgentFolderPicker.Show(Board==null?IntPtr.Zero:new WindowInteropHelper(Board).Handle);if(folder==null)return false;
    Busy=true;Notify();try{string target=await Projects.Export(Controller.Gallery,folder);if(!AgentApplication.Qa)Process.Start(new ProcessStartInfo(target){UseShellExecute=true});return true;}
    finally{Busy=false;Notify();}
  }
  internal async Task Exit(){if(!await Controller.Shelf.ConfirmInlineEditors())return;foreach(var editor in Controller.Editors.ToArray())if(!await editor.ConfirmClose())return;Application.Current.Shutdown();}
  internal void SelectAgent(string agent){if(!AgentProjects.AgentNames.Contains(agent))throw new ArgumentException("请选择已有角色。");Projects.Index.SelectedAgent=agent;Projects.Save();AgentFaces.Decoration=agent;Controller.Shelf.RefreshDecoration();Notify();}
  internal void ClearHistory(){Projects.ClearHistory(History.Select(i=>AgentProjects.ShotId(i.Path)));Controller.Gallery.UnhangWhere(i=>Projects.Note(AgentProjects.ShotId(i.Path)).Archived);Notify();}
  public void Dispose(){if(Board!=null)Board.Close();if(Hub!=null)Hub.Close();if(Bookmark!=null)Bookmark.Close();if(Controller!=null){try{Controller.Gallery.Store.State.Images.Clear();Controller.Gallery.Store.Save();}catch(Exception error){Controller.Gallery.Store.Log(error);}Controller.Dispose();}}
 }
 internal sealed class AgentKeys : IDisposable {
  readonly HwndSource source;readonly AgentSession session;bool capture,board;
  internal bool CaptureAvailable {get{return capture;}}internal bool BoardAvailable {get{return board;}}
  internal bool Requested {get;private set;}
  internal bool Editing {get;private set;}
  internal string CaptureLabel {get{return Label(session.Projects.Index.CaptureKey,session.Projects.Index.CaptureModifiers);}}
  internal static string Label(uint key,uint modifiers){string name=key==0xC0?"·":((System.Windows.Forms.Keys)key).ToString();return ((modifiers&2)!=0?"Ctrl+":"")+((modifiers&1)!=0?"Alt+":"")+((modifiers&4)!=0?"Shift+":"")+((modifiers&8)!=0?"Win+":"")+name;}
  internal string Problem {get;private set;}
  internal AgentKeys(AgentSession s){session=s;source=new HwndSource(new HwndSourceParameters("AgentBloom.Shortcuts"+(AgentApplication.Qa?".QA":"")){ParentWindow=new IntPtr(-3)});source.AddHook(Receive);Enable(!AgentApplication.QaPassive);}
  internal void Enable(bool enabled){
   Requested=enabled;
   WindowsNative.UnregisterHotKey(source.Handle,101);WindowsNative.UnregisterHotKey(source.Handle,102);capture=board=false;Problem="";
   if(enabled&&!Editing){capture=WindowsNative.RegisterHotKey(source.Handle,101,0x4000|session.Projects.Index.CaptureModifiers,session.Projects.Index.CaptureKey);board=WindowsNative.RegisterHotKey(source.Handle,102,0x4004,0xC0);if(!capture||!board)Problem="部分快捷键被其它软件占用，仍可用轮盘和画册按钮操作。";}
  }
  internal void BeginEditing(){if(Editing)return;Editing=true;Enable(Requested);session.Notify();}
  internal void EndEditing(){if(!Editing)return;Editing=false;Enable(Requested);session.Notify();}
  internal void SetCaptureBinding(uint key,uint modifiers){
   if(key<0x20||key>0xFE||(modifiers&~15u)!=0||key==0x5B||key==0x5C)throw new ArgumentException("试试 F8，或 Ctrl / Alt 加上一个按键。");
   if(key==0xC0&&modifiers==4)throw new ArgumentException("这个组合用来打开收集箱，换一个吧。");
   BeginEditing();if(!WindowsNative.RegisterHotKey(source.Handle,101,0x4000|modifiers,key))throw new InvalidOperationException("这个快捷键已被其它软件使用，换一个试试。");
   uint oldKey=session.Projects.Index.CaptureKey,oldModifiers=session.Projects.Index.CaptureModifiers;
   try{session.Projects.Index.CaptureKey=key;session.Projects.Index.CaptureModifiers=modifiers;session.Projects.Save();}
   catch{WindowsNative.UnregisterHotKey(source.Handle,101);session.Projects.Index.CaptureKey=oldKey;session.Projects.Index.CaptureModifiers=oldModifiers;throw;}
   Editing=false;capture=Requested;if(!Requested)WindowsNative.UnregisterHotKey(source.Handle,101);board=Requested&&WindowsNative.RegisterHotKey(source.Handle,102,0x4004,0xC0);Problem=Requested&&!board?"收集箱快捷键被占用，可点左侧书签。":"";session.Notify();
  }
  IntPtr Receive(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x0312){handled=true;if(w.ToInt32()==101)Application.Current.Dispatcher.BeginInvoke(new Action(async delegate{try{await session.Capture();}catch(Exception ex){MessageBox.Show(ex.Message,"AgentBloom");}}));else if(w.ToInt32()==102)session.ShowBoard();}return IntPtr.Zero;}
  public void Dispose(){Enable(false);source.Dispose();}
 }
 internal static class AgentApplication {
  internal static AgentKeys Keys;
  internal static bool Qa;
  internal static bool QaPassive;
  internal static bool Startup;
  internal static bool Capturing;
  [STAThread]internal static void Run(string[] args){
   QaPassive=Array.IndexOf(args,"--qa-passive")>=0;Qa=QaPassive||Array.IndexOf(args,"--qa")>=0;Startup=Array.IndexOf(args,"--startup")>=0;
   if(Array.IndexOf(args,"--quit")>=0){WindowsNative.RequestExit();return;}
   bool first;using(var mutex=new Mutex(true,"Local.AgentBloom.Desktop"+(Qa?".QA":""),out first)){
    if(!first){WindowsNative.ShowExisting();return;}
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};LightTheme.Install(app);
    AgentSession session=null;
    app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e){MessageBox.Show(e.Exception.Message,"AgentBloom");e.Handled=true;};
    app.Startup+=async delegate{
     try{string root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..",Qa?"qa/data":"data"));session=new AgentSession(root);await session.Start();Keys=new AgentKeys(session);session.Notify();if(Keys.Problem.Length>0)MessageBox.Show(Keys.Problem,"AgentBloom 快捷键");}
     catch(Exception ex){MessageBox.Show(ex.Message,"AgentBloom 无法启动");app.Shutdown(1);}
    };
    app.Exit+=delegate{if(Keys!=null)Keys.Dispose();if(session!=null)session.Dispose();};app.Run();
   }
  }
 }
}
