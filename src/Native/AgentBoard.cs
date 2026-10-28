using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ScreenLine {
 internal sealed class AgentBoard : Window {
  readonly AgentSession session;readonly WebView2CompositionControl view=new WebView2CompositionControl();readonly System.Windows.Controls.Grid surface=new System.Windows.Controls.Grid();readonly TranslateTransform slide=new TranslateTransform();readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();bool ready,closing,wantOpen,animating;int drawerVersion;
  readonly TaskCompletionSource<bool> pageReady=new TaskCompletionSource<bool>();bool sendQueued;
  readonly Dictionary<string,Task<byte[]>> thumbnailCache=new Dictionary<string,Task<byte[]>>();
  readonly Queue<string> thumbnailOrder=new Queue<string>();
  int drawerFrames,drawerTravelFrames;double drawerLastTime,drawerLastOffset,drawerFirstOffset,drawerMaxGap,drawerStarted,drawerMilliseconds;
  internal bool DrawerOpen {get{return wantOpen;}}
  internal void FocusBoard(){if(!IsVisible)return;Activate();WindowsNative.ActivateEditor(new System.Windows.Interop.WindowInteropHelper(this).Handle);}
  internal async void OpenShortcutSettings(){try{await pageReady.Task;if(!closing&&ready)await view.ExecuteScriptAsync("openShortcutSettings()");}catch(Exception error){session.Controller.Gallery.Store.Log(error);}}
  bool captureClosing;double captureStarted,lastCaptureFoldMs;
  readonly System.Windows.Controls.Image captureStill=new System.Windows.Controls.Image{Stretch=Stretch.Fill,Visibility=Visibility.Collapsed,IsHitTestVisible=false};bool releasingStill,holdCaptureStill;int stillVersion;
  internal AgentBoard(AgentSession s){
   session=s;Title="AgentBloom · 收集箱";Width=Math.Min(1280,SystemParameters.WorkArea.Width-56);Height=Math.Min(900,SystemParameters.WorkArea.Height-48);MinWidth=800;MinHeight=580;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResizeWithGrip;AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.Manual;Top=SystemParameters.WorkArea.Top+24;Left=SystemParameters.WorkArea.Left+20;ShowActivated=false;
   view.DefaultBackgroundColor=System.Drawing.Color.Transparent;view.UseLayoutRounding=false;surface.Children.Add(view);surface.Children.Add(captureStill);surface.RenderTransform=slide;Content=surface;SizeChanged+=delegate{surface.Clip=BookShape.Geometry(ActualWidth,ActualHeight);};
   Loaded+=async delegate{try{if(view.CoreWebView2==null)await Initialize();}catch(Exception ex){surface.Children.Clear();surface.Children.Add(new System.Windows.Controls.TextBlock{Text="收藏集暂时无法打开："+ex.Message+"\n截图与书签仍可使用。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(30)});pageReady.TrySetResult(true);}};
   PreviewKeyDown+=async delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key!=System.Windows.Input.Key.Escape||session.Busy||!wantOpen||view.IsKeyboardFocusWithin)return;e.Handled=true;try{if(ready)await view.ExecuteScriptAsync("dismissCollection()");else HideDrawer();}catch(Exception error){session.Controller.Gallery.Store.Log(error);}};
   session.Changed+=QueueSend;Closed+=delegate{closing=true;++drawerVersion;CompositionTarget.Rendering-=MeasureFrame;pageReady.TrySetResult(false);session.Changed-=QueueSend;view.Dispose();};
  }
  double HiddenX {get{return SystemParameters.WorkArea.Left-Left-Width-12;}}
  internal async void ShowDrawer(bool afterCapture=false){
   if(afterCapture)holdCaptureStill=false;
   captureClosing=false;
   if(wantOpen&&IsVisible){if(!animating)FocusBoard();if(afterCapture&&!animating)ReleaseCaptureSurface();return;}
   int version=++drawerVersion;bool wasHidden=!IsVisible;wantOpen=true;animating=true;surface.IsHitTestVisible=false;
   try{
    if(wasHidden){slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=HiddenX;Show();}
    WindowsNative.PassMouse(new System.Windows.Interop.WindowInteropHelper(this).Handle,false);
    Send();
    // Do not spend the opening animation while Chromium/layout is waking up offscreen.
    await pageReady.Task;if(closing||version!=drawerVersion)return;
    FocusBoard();
    await Dispatcher.InvokeAsync(new Action(UpdateLayout),System.Windows.Threading.DispatcherPriority.Render);
    await NextFrame();if(closing||version!=drawerVersion)return;
    AnimateTo(0,afterCapture?620:280,version);
   }catch(Exception error){session.Controller.Gallery.Store.Log(error);if(!closing&&version==drawerVersion){animating=false;slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=0;surface.IsHitTestVisible=true;}}
  }
  internal void HideDrawer(){CloseDrawer(260);}
  internal void CollapseForCapture(){captureClosing=true;captureStarted=clock.Elapsed.TotalMilliseconds;CloseDrawer(620);}
  internal async Task PrepareCaptureSurface(){
   ++stillVersion;holdCaptureStill=true;releasingStill=false;captureStill.BeginAnimation(OpacityProperty,null);captureStill.Opacity=1;
   if(captureStill.Visibility==Visibility.Visible||!ready||view.CoreWebView2==null)return;
   using(var stream=new MemoryStream()){await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);stream.Position=0;var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];bitmap.Freeze();captureStill.Source=bitmap;captureStill.BeginAnimation(OpacityProperty,null);captureStill.Opacity=1;captureStill.Visibility=Visibility.Visible;}
  }
  async void ReleaseCaptureSurface(){if(holdCaptureStill||releasingStill||captureStill.Visibility!=Visibility.Visible)return;int version=stillVersion;releasingStill=true;try{await view.ExecuteScriptAsync("window.dispatchEvent(new Event('resize'))");await Task.Delay(100);if(closing||version!=stillVersion||holdCaptureStill)return;var fade=AgentHub.Motion(0,140);fade.Completed+=delegate{if(version!=stillVersion)return;captureStill.Visibility=Visibility.Collapsed;captureStill.Source=null;releasingStill=false;if(AgentApplication.Qa)Send();};captureStill.BeginAnimation(OpacityProperty,fade);}catch(Exception error){session.Controller.Gallery.Store.Log(error);if(version==stillVersion){captureStill.Visibility=Visibility.Collapsed;captureStill.Source=null;releasingStill=false;}}}
  static async Task NextFrame(){var completion=new TaskCompletionSource<bool>();EventHandler handler=delegate{completion.TrySetResult(true);};CompositionTarget.Rendering+=handler;try{await Task.WhenAny(completion.Task,Task.Delay(32));}finally{CompositionTarget.Rendering-=handler;}}
  async void CloseShortcutDialog(){try{if(ready&&!closing)await view.ExecuteScriptAsync("document.getElementById('shortcut-dialog').close()");}catch(Exception error){session.Controller.Gallery.Store.Log(error);}}
  void CloseDrawer(int milliseconds){if(AgentApplication.Keys!=null&&AgentApplication.Keys.Editing){AgentApplication.Keys.EndEditing();CloseShortcutDialog();}if(!wantOpen&&!animating)return;int version=++drawerVersion;wantOpen=false;surface.IsHitTestVisible=false;WindowsNative.PassMouse(new System.Windows.Interop.WindowInteropHelper(this).Handle,true);AnimateTo(HiddenX,milliseconds,version);}
  void AnimateTo(double target,int milliseconds,int version){
   double from=slide.X;slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=from;animating=true;
   CompositionTarget.Rendering-=MeasureFrame;drawerFrames=drawerTravelFrames=0;drawerLastTime=drawerMaxGap=0;drawerFirstOffset=drawerLastOffset=from;drawerStarted=clock.Elapsed.TotalMilliseconds;
   if(AgentApplication.Qa)CompositionTarget.Rendering+=MeasureFrame;
   var animation=new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(AgentFaces.Quiet||!SystemParameters.ClientAreaAnimation?1:milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};
   animation.Completed+=delegate{if(closing||version!=drawerVersion)return;CompositionTarget.Rendering-=MeasureFrame;drawerMilliseconds=clock.Elapsed.TotalMilliseconds-drawerStarted;slide.BeginAnimation(TranslateTransform.XProperty,null);slide.X=target;animating=false;if(wantOpen){surface.IsHitTestVisible=true;ReleaseCaptureSurface();}else{Hide();if(captureClosing){lastCaptureFoldMs=clock.Elapsed.TotalMilliseconds-captureStarted;captureClosing=false;}}if(AgentApplication.Qa)Send();};
   slide.BeginAnimation(TranslateTransform.XProperty,animation);if(AgentApplication.Qa)Send();
  }
  void MeasureFrame(object sender,EventArgs args){double now=((RenderingEventArgs)args).RenderingTime.TotalMilliseconds;if(now==drawerLastTime)return;if(drawerLastTime>0)drawerMaxGap=Math.Max(drawerMaxGap,now-drawerLastTime);else drawerFirstOffset=slide.X;if(Math.Abs(slide.X-drawerLastOffset)>.5)drawerTravelFrames++;drawerFrames++;drawerLastTime=now;drawerLastOffset=slide.X;}
  async Task Initialize(){
   var options=new CoreWebView2EnvironmentOptions();
   if(AgentApplication.Qa)options.AdditionalBrowserArguments="--remote-debugging-port=9357 --remote-debugging-address=127.0.0.1";
   var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(session.Projects.Root,"webview"),options);if(closing)return;
   await view.EnsureCoreWebView2Async(environment);if(closing)return;
   view.CoreWebView2.SetVirtualHostNameToFolderMapping("agentbloom.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
   view.CoreWebView2.SetVirtualHostNameToFolderMapping("agentbloom.assets",session.Projects.Root,CoreWebView2HostResourceAccessKind.Allow);
   view.CoreWebView2.AddWebResourceRequestedFilter("https://agentbloom.thumbs/*",CoreWebView2WebResourceContext.Image);
   view.CoreWebView2.WebResourceRequested+=ThumbnailRequested;
   view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;view.CoreWebView2.Settings.IsStatusBarEnabled=false;
   view.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e){if(!e.Uri.StartsWith("https://agentbloom.local/",StringComparison.Ordinal))e.Cancel=true;};
   view.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};
   view.CoreWebView2.WebMessageReceived+=async delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e){
    if(!e.Source.StartsWith("https://agentbloom.local/",StringComparison.Ordinal))return;
    var message=session.Projects.Json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);if(message==null||!message.ContainsKey("action"))return;
    string action=Convert.ToString(message["action"]);
    if(action=="drag"){WindowsNative.DragWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);return;}
    // Native modal UI must run after the WebView callback has returned.
    await Task.Yield();
    try{
     string id=message.ContainsKey("id")?Convert.ToString(message["id"]):"";
     if(session.Busy&&action!="ready"){Toast("正在完成当前操作，请稍候。");return;}
     switch(action){
      case "ready":ready=true;pageReady.TrySetResult(true);Send();break;
      case "exit":await session.Exit();break;
      case "hide":HideDrawer();break;
      case "minimize":HideDrawer();break;
      case "startup":AgentStartup.SetEnabled(Convert.ToBoolean(message["enabled"]));Send();break;
      case "drag":WindowsNative.DragWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);break;
      case "theme":session.SelectAgent(Convert.ToString(message["agent"]));break;
      case "category":session.Find(id);if(message.ContainsKey("text")){string text=Convert.ToString(message["text"]);if(text.Length>10000)throw new ArgumentException("备注最多 10000 字。");session.Projects.Note(id).Text=text;}session.Projects.SetCategory(id,Convert.ToString(message["category"]));Send();break;
      case "clear-history":if(!session.Projects.Index.SkipClearConfirm&&(!message.ContainsKey("confirmed")||!Convert.ToBoolean(message["confirmed"])))throw new InvalidOperationException("先和小伙伴确认一下要不要打扫吧。");if(message.ContainsKey("skipConfirm")){session.Projects.Index.SkipClearConfirm=Convert.ToBoolean(message["skipConfirm"]);session.Projects.Save();}if(!await session.Controller.Shelf.ConfirmInlineEditors())break;session.ClearHistory();Toast("未收藏的小截图已收好，星标小收藏都还在。");break;
      case "restore-history":session.Projects.RestoreHistory();session.Notify();Toast("已恢复清理的截图");break;
      case "capture":await session.Capture();break;
      case "import":await session.Import();break;
      case "switch":await session.Switch(id);break;
      case "create":await session.Create(Convert.ToString(message["name"]));break;
      case "copy":await session.Controller.CopyImage(session.Find(id));Toast("已复制 ✿");break;
      case "appearance":session.Controller.ShowAppearance();break;
      case "edit":session.Controller.Act(ShotAction.Edit,session.Find(id));break;
      case "pin":await session.Pin(session.Find(id));break;
      case "hang":session.Controller.Captures.Hang(session.Find(id));break;
      case "open":session.Controller.Act(ShotAction.Open,session.Find(id));break;
      case "remove":session.Controller.Act(ShotAction.Remove,session.Find(id));break;
      case "save":session.Controller.Act(ShotAction.Save,session.Find(id));break;
      case "note":session.Find(id);string note=Convert.ToString(message["text"]);if(note.Length>10000)throw new ArgumentException("备注最多 10000 字。");session.Projects.Note(id).Text=note;session.Projects.Save();Toast("备注已保存");Send();break;
      case "favorite":session.Find(id);var meta=session.Projects.Note(id);meta.Favorite=!meta.Favorite;session.Projects.Save();Send();break;
      case "agent":session.Find(id);session.Projects.SetAgent(id,Convert.ToString(message["agent"]));session.Controller.Shelf.ApplyTheme(session.Controller.Shelf.Appearance);Send();break;
      case "decoration":session.Projects.SetDecoration(Convert.ToString(message["decoration"]));AgentFaces.Decoration=session.Projects.Current.Decoration;session.Controller.Shelf.ApplyTheme(session.Controller.Shelf.Appearance);Send();break;
      case "export":if(await session.Export())Toast("项目副本已导出");break;
      case "quiet":session.Projects.Index.Quiet=!session.Projects.Index.Quiet;AgentFaces.Quiet=session.Projects.Index.Quiet;session.Projects.Save();session.Controller.Shelf.ApplyTheme(session.Controller.Shelf.Appearance);Send();break;
      case "keys":if(AgentApplication.Keys!=null){AgentApplication.Keys.Enable(Convert.ToBoolean(message["enabled"]));Toast(AgentApplication.Keys.Problem.Length>0?AgentApplication.Keys.Problem:Convert.ToBoolean(message["enabled"])?"快捷键已启用":"快捷键已暂停，可正常输入反引号");Send();}break;
      case "shortcut-begin":if(AgentApplication.Keys!=null){AgentApplication.Keys.BeginEditing();Send();}break;
      case "shortcut-end":if(AgentApplication.Keys!=null){AgentApplication.Keys.EndEditing();Send();}break;
      case "shortcut-save":if(AgentApplication.Keys!=null){AgentApplication.Keys.SetCaptureBinding(Convert.ToUInt32(message["key"]),Convert.ToUInt32(message["modifiers"]));Send();Toast("截图快捷键已保存 ✿");}break;
     }
    }catch(Exception ex){Toast(ex.Message);}
   };
   view.Source=new Uri("https://agentbloom.local/index.html");
  }
  async void ThumbnailRequested(object sender,CoreWebView2WebResourceRequestedEventArgs e){
   var uri=new Uri(e.Request.Uri);if(uri.Host!="agentbloom.thumbs")return;
   var deferral=e.GetDeferral();try{
    string key=Path.GetFileNameWithoutExtension(uri.AbsolutePath).Replace('-','+').Replace('_','/');Task<byte[]> pending;
    if(!thumbnailCache.TryGetValue(key,out pending)){
     var item=session.Controller.Gallery.Collected.FirstOrDefault(shot=>shot.Image.Fingerprint==key);
     if(item==null){e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Not Found","");return;}
     var image=item.Image.Thumbnail;
     pending=Task.Run(delegate{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}});
     thumbnailCache.Add(key,pending);thumbnailOrder.Enqueue(key);
     while(thumbnailOrder.Count>192)thumbnailCache.Remove(thumbnailOrder.Dequeue());
    }
    byte[] bytes=await pending;if(closing)return;
    e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(bytes,false),200,"OK","Content-Type: image/png\r\nCache-Control: private, max-age=31536000, immutable\r\n");
   }catch(Exception error){if(!closing)session.Controller.Gallery.Store.Log(error);}
   finally{deferral.Complete();}
  }
  void QueueSend(){if(sendQueued||closing)return;sendQueued=true;Dispatcher.BeginInvoke(new Action(delegate{sendQueued=false;Send();}),System.Windows.Threading.DispatcherPriority.Background);}
  internal void Send(){
   if(!ready||closing||view.CoreWebView2==null||session.Controller==null)return;
   if(!IsVisible&&!AgentApplication.Qa)return;
   var root=new Uri(session.Projects.Root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar);
   var hungPaths=new HashSet<string>(session.Controller.Gallery.Items.Select(i=>Path.GetFullPath(i.Path)),StringComparer.OrdinalIgnoreCase);
   var allShots=session.History.OrderByDescending(i=>i.Written).Select((item,index)=>{
    string id=AgentProjects.ShotId(item.Path);var note=session.Projects.Note(id);
    string relative=root.MakeRelativeUri(new Uri(item.Path)).ToString();
    return new{id=id,name=Path.GetFileName(item.Path),uri="https://agentbloom.assets/"+relative+"?v="+item.Written.Ticks,thumbnail="https://agentbloom.thumbs/"+item.Image.Fingerprint.Replace('+','-').Replace('/','_')+".png",width=item.Image.PixelWidth,height=item.Image.PixelHeight,agent=note.Agent,note=note.Text,favorite=note.Favorite,category=note.Category,hung=hungPaths.Contains(Path.GetFullPath(item.Path)),date=item.Written.ToLocalTime().ToString("MM月dd日 HH:mm")};
   }).ToArray();
   var shots=allShots.Where(s=>session.Projects.Current.Id==session.Projects.Inbox.Id||s.category==session.Projects.Current.Id).ToArray();
   var keys=AgentApplication.Keys;
   view.CoreWebView2.PostWebMessageAsJson(session.Projects.Json.Serialize(new{type="state",qa=AgentApplication.Qa,shortcutEditing=keys!=null&&keys.Editing,captureKey=session.Projects.Index.CaptureKey,captureModifiers=session.Projects.Index.CaptureModifiers,captureKeyLabel=keys==null?"·":keys.CaptureLabel,palette=session.Controller.Shelf.Appearance.Id,paletteAccent=session.Controller.Shelf.Appearance.RopeColor,paletteWash=session.Controller.Shelf.Appearance.RopeHighlight,drawerFrames=drawerFrames,drawerTravelFrames=drawerTravelFrames,drawerFirstOffset=drawerFirstOffset,drawerMaxGapMs=drawerMaxGap,drawerMs=drawerMilliseconds,captureStillVisible=captureStill.Visibility==Visibility.Visible,captureFoldMs=lastCaptureFoldMs,drawerOpen=wantOpen,drawerVisible=IsVisible,drawerAnimating=animating,drawerOffset=slide.X,shelfShown=session.Controller.Shelf.Revealed,projects=session.Projects.Index.Projects.Select(p=>new{id=p.Id,name=p.Name,color=p.Color}),active=session.Projects.Index.Active,inbox=session.Projects.Inbox.Id,selectedAgent=session.Projects.Index.SelectedAgent,decoration=session.Projects.Index.SelectedAgent,skipClearConfirm=session.Projects.Index.SkipClearConfirm,canRestore=session.Projects.Index.LastClear!=null&&session.Projects.Index.LastClear.Count>0,captureFirstFramePrepared=AgentRegionCapture.FirstFramePrepared,capturePaintsBeforeReveal=AgentRegionCapture.PaintsBeforeReveal,captureReadyMs=AgentRegionCapture.ReadyMilliseconds,captureSceneReadyMs=AgentRegionCapture.SceneReadyMilliseconds,flightOrigin=ShotFlight.LastOrigin.IsEmpty?null:new{left=ShotFlight.LastOrigin.X,top=ShotFlight.LastOrigin.Y,width=ShotFlight.LastOrigin.Width,height=ShotFlight.LastOrigin.Height},flightMaxFrameGapMs=ShotFlight.LastMaxFrameGap,flightTravelFrames=ShotFlight.LastTravelFrames,capturePaintMaxMs=AgentRegionCapture.PaintMaximumMilliseconds,capturePaintMs=AgentRegionCapture.PaintFrames==0?0:AgentRegionCapture.PaintMilliseconds/AgentRegionCapture.PaintFrames,startup=AgentStartup.Enabled,quiet=session.Projects.Index.Quiet,busy=session.Busy,shots=shots,hanging=allShots.Where(s=>s.hung).ToArray(),keysRequested=keys==null||keys.Requested,captureShortcut=keys!=null&&keys.CaptureAvailable,boardShortcut=keys!=null&&keys.BoardAvailable}));
  }
  void Toast(string text){if(!closing&&view.CoreWebView2!=null)view.CoreWebView2.PostWebMessageAsJson(session.Projects.Json.Serialize(new{type="toast",text=text}));}
 }
}
