using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScreenLine {
 internal sealed class InlineAnnotation:Canvas {
  internal static bool WarnBeforeDiscard;
  internal static readonly FontFamily AnnotationFont=LoadAnnotationFont();
  static FontFamily LoadAnnotationFont(){var folder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui","assets","fonts");if(Directory.Exists(folder)){var families=Fonts.GetFontFamilies(folder+System.IO.Path.DirectorySeparatorChar).ToArray();var family=families.FirstOrDefault(f=>f.FamilyNames.Values.Any(n=>n.Contains("波波")))??families.FirstOrDefault();if(family!=null)return family;}return new FontFamily("荆南波波黑");}
  readonly BitmapSource original;readonly List<UIElement> marks=new List<UIElement>();readonly Stack<UIElement> redo=new Stack<UIElement>();
  readonly Func<BitmapSource,Task> save;readonly Action<bool> finished;readonly AnnotationTools tools;
  readonly Window ownerWindow;readonly string itemPath;
  string tool="pen";Brush color=LightTheme.Brush("#A082CB");double stroke=3;Point start;Shape draft;Polyline pen;TextBox text;bool saving,ending;
  internal bool Dirty {get{return marks.Count>0||(text!=null&&!string.IsNullOrWhiteSpace(text.Text));}}

  internal InlineAnnotation(Window owner,string path,BitmapSource bitmap,Func<BitmapSource,Task> saveImage,Action<bool> end,Func<Rect> getPhotoRect=null,Action<Vector> moveImage=null){
   ownerWindow=owner;itemPath=path;original=bitmap;save=saveImage;finished=end;Background=Brushes.Transparent;ClipToBounds=true;Cursor=Cursors.Pen;Focusable=true;
   tools=new AnnotationTools(owner,Select,Undo,Redo,Save,Copy,Cancel,getPhotoRect,moveImage);tools.Closed+=delegate{if(!saving&&IsVisible)CommitText();};
   MouseLeftButtonDown+=Down;MouseMove+=Move;MouseLeftButtonUp+=Up;LostMouseCapture+=delegate{FinishStroke();};
   Loaded+=delegate{tools.Show();tools.Position();Focus();BeginAnimation(OpacityProperty,AgentHub.Motion(1,130));};
   owner.LocationChanged+=Position;owner.SizeChanged+=Position;Unloaded+=delegate{owner.LocationChanged-=Position;owner.SizeChanged-=Position;tools.Close();};
   PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(text!=null)return;if(e.Key==Key.Escape){Cancel();e.Handled=true;}else if((Keyboard.Modifiers&ModifierKeys.Control)!=0){if(e.Key==Key.Z){Undo();e.Handled=true;}else if(e.Key==Key.Y){Redo();e.Handled=true;}else if(e.Key==Key.S){Save();e.Handled=true;}}};
  }
  void Position(object sender,EventArgs e){if(tools.IsVisible)tools.Position();}
  void Select(string value){CommitText();if(value.StartsWith("#")){color=LightTheme.Brush(value);return;}if(value=="thin"||value=="thick"){stroke=value=="thin"?3:6;return;}tool=value;Cursor=tool=="text"?Cursors.IBeam:Cursors.Pen;Focus();}
  void Down(object sender,MouseButtonEventArgs e){if(saving||e.OriginalSource is TextBox)return;e.Handled=true;CommitText();start=e.GetPosition(this);
   if(tool=="text"){text=new TextBox{MinWidth=40,MaxWidth=Math.Max(40,ActualWidth-start.X),FontSize=16,FontFamily=AnnotationFont,Foreground=color,Background=LightTheme.Brush("#EEFFFFFF"),BorderBrush=color,BorderThickness=new Thickness(1),Padding=new Thickness(2),AcceptsReturn=true};SetLeft(text,start.X);SetTop(text,start.Y);Children.Add(text);var pendingText=text;pendingText.Loaded+=delegate{if(Children.Contains(pendingText)){WindowsNative.ActivateEditor(new System.Windows.Interop.WindowInteropHelper(ownerWindow).Handle);pendingText.Focus();Keyboard.Focus(pendingText);}};System.Windows.Automation.AutomationProperties.SetAutomationId(text,"inline-text-input");text.KeyDown+=delegate(object s,KeyEventArgs key){if(key.Key==Key.Escape){Children.Remove(text);text=null;key.Handled=true;}else if(key.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){CommitText();key.Handled=true;Focus();}};text.Focus();return;}
   if(tool=="pen"){pen=new Polyline{Stroke=color,StrokeThickness=stroke,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};pen.Points.Add(start);draft=pen;}
   else if(tool=="rect")draft=new Rectangle{Stroke=color,StrokeThickness=stroke,RadiusX=3,RadiusY=3};
   else if(tool=="ellipse")draft=new Ellipse{Stroke=color,StrokeThickness=stroke};
   else if(tool=="arrow")draft=new System.Windows.Shapes.Path{Stroke=color,StrokeThickness=stroke,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
   else draft=new Rectangle{Fill=LightTheme.Brush("#55B49AD4"),Stroke=color,StrokeThickness=1};
   Children.Add(draft);CaptureMouse();UpdateDraft(start);
  }
  Point Bounded(Point point){return new Point(Math.Max(0,Math.Min(ActualWidth,point.X)),Math.Max(0,Math.Min(ActualHeight,point.Y)));}
  void Move(object sender,MouseEventArgs e){if(draft==null)return;UpdateDraft(Bounded(e.GetPosition(this)));e.Handled=true;}
  void UpdateDraft(Point point){
   if(pen!=null){pen.Points.Add(point);return;}var r=new Rect(start,point);
   var path=draft as System.Windows.Shapes.Path;if(path!=null){Vector direction=point-start;if(direction.Length<.1)return;direction.Normalize();var side=new Vector(-direction.Y,direction.X);double cap=Math.Min(13,(point-start).Length*.4);var geometry=new PathGeometry();geometry.Figures.Add(new PathFigure{StartPoint=start,Segments={new LineSegment(point,true)}});geometry.Figures.Add(new PathFigure{StartPoint=point-direction*cap+side*cap*.55,Segments={new LineSegment(point,true),new LineSegment(point-direction*cap-side*cap*.55,true)}});path.Data=geometry;return;}
   SetLeft(draft,r.Left);SetTop(draft,r.Top);draft.Width=r.Width;draft.Height=r.Height;
  }
  void Up(object sender,MouseButtonEventArgs e){if(draft==null)return;UpdateDraft(Bounded(e.GetPosition(this)));FinishStroke();ReleaseMouseCapture();e.Handled=true;}
  void FinishStroke(){if(draft==null)return;UIElement mark=draft;
   if(tool=="mosaic"){var rectangle=new Rect(GetLeft(draft),GetTop(draft),draft.Width,draft.Height);Children.Remove(draft);if(rectangle.Width>=2&&rectangle.Height>=2){var composed=Flatten();int x=(int)(rectangle.X/ActualWidth*original.PixelWidth),y=(int)(rectangle.Y/ActualHeight*original.PixelHeight),w=Math.Max(1,(int)(rectangle.Width/ActualWidth*original.PixelWidth)),h=Math.Max(1,(int)(rectangle.Height/ActualHeight*original.PixelHeight));w=Math.Min(w,original.PixelWidth-x);h=Math.Min(h,original.PixelHeight-y);var crop=new CroppedBitmap(composed,new Int32Rect(x,y,w,h));var small=new TransformedBitmap(crop,new ScaleTransform(Math.Max(1,w/12)/((double)w),Math.Max(1,h/12)/((double)h)));small.Freeze();var block=new Image{Source=small,Stretch=Stretch.Fill,Width=rectangle.Width,Height=rectangle.Height};RenderOptions.SetBitmapScalingMode(block,BitmapScalingMode.NearestNeighbor);SetLeft(block,rectangle.Left);SetTop(block,rectangle.Top);Children.Add(block);mark=block;}else mark=null;}
   if(mark!=null){marks.Add(mark);redo.Clear();}draft=null;pen=null;tools.History(marks.Count>0,redo.Count>0);
  }
  void CommitText(){if(text==null)return;var input=text;text=null;Children.Remove(input);if(string.IsNullOrWhiteSpace(input.Text))return;var label=new TextBlock{Text=input.Text,FontFamily=input.FontFamily,FontWeight=input.FontWeight,FontStyle=input.FontStyle,FontSize=input.FontSize,Foreground=input.Foreground,TextWrapping=TextWrapping.Wrap,MaxWidth=input.MaxWidth};SetLeft(label,GetLeft(input)+3);SetTop(label,GetTop(input)+3);Children.Add(label);marks.Add(label);redo.Clear();tools.History(true,false);}
  internal void Undo(){CommitText();if(marks.Count==0||saving)return;var mark=marks[marks.Count-1];marks.RemoveAt(marks.Count-1);Children.Remove(mark);redo.Push(mark);tools.History(marks.Count>0,true);}
  internal void Redo(){if(redo.Count==0||saving)return;var mark=redo.Pop();marks.Add(mark);Children.Add(mark);tools.History(true,redo.Count>0);}
  internal BitmapSource Flatten(){CommitText();UpdateLayout();var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){var full=new Rect(0,0,original.PixelWidth,original.PixelHeight);draw.DrawImage(original,full);if(marks.Count>0){var brush=new VisualBrush(this){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,ActualWidth,ActualHeight),Stretch=Stretch.Fill};draw.DrawRectangle(brush,null,full);}}var output=new RenderTargetBitmap(original.PixelWidth,original.PixelHeight,96,96,PixelFormats.Pbgra32);output.Render(visual);output.Freeze();return output;}
  internal static Task SaveImage(string path,BitmapSource annotated){return Task.Run(delegate{string backup=path+".original";if(!File.Exists(backup))File.Copy(path,backup,false);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(annotated));using(var stream=new MemoryStream()){encoder.Save(stream);ImageEditFiles.Save(path,stream.ToArray());}});}
  internal async Task<bool> SaveAsync(){if(saving)return false;CommitText();saving=true;tools.Busy(true);try{if(Dirty)await save(Flatten());await End(true);return true;}catch(Exception error){tools.Status("保存没完成，标注还在："+error.Message);return false;}finally{saving=false;if(tools.IsVisible)tools.Busy(false);}}
  async void Save(){await SaveAsync();}
  async void Copy(){if(saving)return;try{var image=Flatten();var data=new DataObject();data.SetImage(image);var bytes=await Task.Run(delegate{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}});data.SetData("PNG",new MemoryStream(bytes));await WindowsClipboard.Write(data);tools.Status("已复制，带上小标注啦 ✿");}catch(Exception error){tools.Status(error.Message);}}
  async void Cancel(){await ConfirmEnd();}
  internal async Task<bool> ConfirmEnd(){
   if(saving)return false;if(ending)return true;
   if(Dirty&&WarnBeforeDiscard&&!string.IsNullOrEmpty(itemPath)){
    var dialog=new SaveChangesDialog(ownerWindow,itemPath);
    if(dialog.ShowDialog()==true){
     if(dialog.Choice==MessageBoxResult.Yes)return await SaveAsync();
     if(dialog.Choice==MessageBoxResult.Cancel)return false;
    }else return false;
   }
   await End(false);return true;
  }
  async Task End(bool saved){if(ending)return;ending=true;tools.Fold();BeginAnimation(OpacityProperty,AgentHub.Motion(0,110));await Task.Delay(AgentFaces.Quiet?1:110);Children.Clear();marks.Clear();text=null;finished(saved);}
 }

 internal sealed class AnnotationTools:Window {
  readonly Window ownerWindow;readonly Func<Rect> getBounds;readonly StackPanel panel=new StackPanel();readonly Button undo,redo;readonly TextBlock status=new TextBlock{FontSize=10,Foreground=LightTheme.Brush("#9989AF"),Margin=new Thickness(7,4,5,0)};
  internal AnnotationTools(Window owner,Action<string> choose,Action back,Action forward,Action done,Action copy,Action cancel,Func<Rect> boundsProvider=null,Action<Vector> moveImage=null){
   ownerWindow=owner;getBounds=boundsProvider;Owner=owner;Title="AgentBloom · 就地标注";Width=520;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
   var handle=new Button{Style=LightTheme.ButtonStyle,Height=24,Background=LightTheme.Brush("#F1EBF9"),Padding=new Thickness(8,3,8,3),Margin=new Thickness(1,0,1,5),Cursor=Cursors.SizeAll,Content=new TextBlock{Text="⋮⋮  拖动图片",FontSize=11,Foreground=LightTheme.Brush("#8B78A8"),TextAlignment=TextAlignment.Center}};System.Windows.Automation.AutomationProperties.SetAutomationId(handle,"inline-drag-handle");handle.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;var ownerHandle=new System.Windows.Interop.WindowInteropHelper(ownerWindow).Handle;WindowsNative.Rectangle before,after;WindowsNative.GetWindowRect(ownerHandle,out before);WindowsNative.DragWindow(ownerHandle);if(moveImage!=null&&WindowsNative.GetWindowRect(ownerHandle,out after))moveImage(new Vector(after.Left-before.Left,after.Top-before.Top));};panel.Children.Add(handle);
   var first=new WrapPanel();string[] labels={"画笔","矩形","椭圆","箭头","文字","马赛克"},values={"pen","rect","ellipse","arrow","text","mosaic"};for(int i=0;i<labels.Length;i++){string tool=values[i];var button=Add(first,labels[i],delegate{choose(tool);},"tool-"+tool);if(i==0)button.Background=LightTheme.Brush("#ECE5F5");button.Click+=delegate{foreach(var child in first.Children)if(child is Button)((Button)child).Background=Brushes.Transparent;button.Background=LightTheme.Brush("#ECE5F5");};}panel.Children.Add(first);
   var second=new WrapPanel{Margin=new Thickness(0,5,0,0)};foreach(string value in new[]{"#A082CB","#EA879C","#E9BA62","#79ADAE","#283343"}){string selected=value;var color=Add(second,"●",delegate{choose(selected);},"color-"+value.Substring(1));color.Foreground=LightTheme.Brush(value);color.FontSize=18;color.MinWidth=28;color.Width=28;color.Padding=new Thickness(0);}
   Add(second,"细",delegate{choose("thin");},"stroke-thin");Add(second,"粗",delegate{choose("thick");},"stroke-thick");undo=Add(second,"↶",back,"inline-undo");redo=Add(second,"↷",forward,"inline-redo");Add(second,"复制",copy,"inline-copy");Add(second,"取消",cancel,"inline-cancel");var save=Add(second,"完成 ✓",done,"inline-save");save.Background=LightTheme.Brush("#E7DDF3");panel.Children.Add(second);panel.Children.Add(status);status.Text="就地画一画 · Ctrl+Z 撤销 · Esc 取消";
   var surface=new Border{Background=LightTheme.Brush("#FFFDFCFF"),CornerRadius=new CornerRadius(14),BorderBrush=LightTheme.Brush("#D8CDE7"),BorderThickness=new Thickness(1),Padding=new Thickness(9),Child=panel,Margin=new Thickness(8)};surface.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=3,Opacity=.15};Content=surface;
   SourceInitialized+=delegate{WindowsNative.PrepareOverlay(new System.Windows.Interop.WindowInteropHelper(this).Handle);};Loaded+=delegate{Opacity=0;BeginAnimation(OpacityProperty,AgentHub.Motion(1,130));};History(false,false);
  }
  static Button Add(Panel parent,string text,Action action,string id){var button=new Button{Content=text,FontSize=12,MinWidth=40,Height=30,Margin=new Thickness(1),Padding=new Thickness(7,2,7,2),Style=LightTheme.ButtonStyle,Focusable=false};System.Windows.Automation.AutomationProperties.SetAutomationId(button,id);System.Windows.Automation.AutomationProperties.SetName(button,text);button.Click+=delegate{action();};parent.Children.Add(button);return button;}
  internal void Position(){
   if(!ownerWindow.IsVisible)return;
   Rect rect=getBounds!=null?getBounds():new Rect(ownerWindow.Left,ownerWindow.Top,ownerWindow.ActualWidth,ownerWindow.ActualHeight);
   var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(rect.X+rect.Width/2),(int)(rect.Y+rect.Height/2)));double dpi=WindowsNative.MonitorScale(screen);
   Left=Math.Max(screen.WorkingArea.Left/dpi,Math.Min((screen.WorkingArea.Right/dpi)-Width,rect.X/dpi));
   double height=ActualHeight>0?ActualHeight:112;Top=rect.Bottom/dpi+4;
   if((Top+height)*dpi>screen.WorkingArea.Bottom)Top=Math.Max(screen.WorkingArea.Top/dpi,rect.Y/dpi-height-4);
  }
  internal void History(bool back,bool forward){undo.IsEnabled=back;redo.IsEnabled=forward;}
  internal void Busy(bool value){panel.IsEnabled=!value;status.Text=value?"正在收好小标注…":"就地画一画 · Ctrl+Z 撤销 · Esc 取消";}
  internal void Status(string message){status.Text=message;}
  internal void Fold(){BeginAnimation(OpacityProperty,AgentHub.Motion(0,100));}
 }

 internal sealed class InPlaceEditorWindow:Window {
  readonly CaptureItem item;readonly Func<string,Task> onSaved;readonly Border frame=new Border();readonly Image photo=new Image();InlineAnnotation inline;bool finishing,checking;
  InPlaceEditorWindow(CaptureItem captureItem,Rect origin,Func<string,Task> savedCallback){
   item=captureItem;onSaved=savedCallback;
   Title="就地标注 · "+System.IO.Path.GetFileName(captureItem.Path);
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=true;Topmost=true;
   var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(origin.X+origin.Width/2),(int)origin.Y));double dpi=WindowsNative.MonitorScale(screen);
   BitmapSource bitmap=PinnedImageWindow.ReadPicture(captureItem.Path);photo.Source=bitmap;photo.Stretch=Stretch.Uniform;
   double maxW=Math.Min(800,screen.WorkingArea.Width/dpi*0.8),maxH=Math.Min(600,screen.WorkingArea.Height/dpi*0.8);
   double scale=Math.Min(1,Math.Min(maxW/bitmap.PixelWidth,maxH/bitmap.PixelHeight));
   double imgW=bitmap.PixelWidth*scale,imgH=bitmap.PixelHeight*scale;
   Width=imgW+28;Height=imgH+28;
   Left=Math.Max(screen.WorkingArea.Left/dpi+10,Math.Min(screen.WorkingArea.Right/dpi-Width-10,origin.X/dpi-14));
   Top=Math.Max(screen.WorkingArea.Top/dpi+10,Math.Min(screen.WorkingArea.Bottom/dpi-Height-120,origin.Y/dpi-14));
   frame.Margin=new Thickness(14);frame.Padding=new Thickness(2);frame.Background=Brushes.White;frame.BorderBrush=LightTheme.Brush("#DCE1E9");frame.BorderThickness=new Thickness(1);frame.CornerRadius=new CornerRadius(12);
   frame.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=5,Opacity=.22};
   var layer=new Grid();photo.Clip=new RectangleGeometry(new Rect(0,0,imgW,imgH),7,7);layer.Children.Add(photo);frame.Child=layer;Content=frame;
   SourceInitialized+=delegate{var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;WindowsNative.PrepareOverlay(handle);WindowsNative.AllowActivation(handle);};
   Closing+=async delegate(object sender,System.ComponentModel.CancelEventArgs e){if(finishing||inline==null)return;e.Cancel=true;if(checking)return;checking=true;bool allowed=await inline.ConfirmEnd();checking=false;if(allowed){finishing=true;Close();}};
   Loaded+=delegate{
    Opacity=0;BeginAnimation(OpacityProperty,AgentHub.Motion(1,120));
    inline=new InlineAnnotation(this,item.Path,bitmap,async annotated=>{
     await InlineAnnotation.SaveImage(item.Path,annotated);
     if(onSaved!=null)await onSaved(item.Path);
    },done=>{
     finishing=true;inline=null;var fade=AgentHub.Motion(0,100);fade.Completed+=delegate{Close();};BeginAnimation(OpacityProperty,fade);
    },()=>new Rect(photo.PointToScreen(new Point()),photo.PointToScreen(new Point(photo.ActualWidth,photo.ActualHeight))));
    layer.Children.Add(inline);
   };
  }
  internal static bool FocusExisting(string path){var current=Application.Current.Windows.OfType<InPlaceEditorWindow>().FirstOrDefault(w=>FileRules.SameFile(w.item.Path,path));if(current==null)return false;current.Activate();return true;}
  internal static async Task<bool> ConfirmAll(){foreach(var editor in Application.Current.Windows.OfType<InPlaceEditorWindow>().ToArray())if(editor.inline!=null&&!await editor.inline.ConfirmEnd())return false;return true;}
  internal static void Open(CaptureItem item,Rect origin,Func<string,Task> savedCallback=null){if(FocusExisting(item.Path))return;var window=new InPlaceEditorWindow(item,origin,savedCallback);window.Show();}
 }
}
