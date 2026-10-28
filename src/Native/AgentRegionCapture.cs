using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenLine {
 internal sealed class CapturedRegion:IDisposable {
  internal readonly Bitmap Picture;internal readonly Rectangle Bounds;
  internal CapturedRegion(Bitmap picture,Rectangle bounds){Picture=picture;Bounds=bounds;}
  public void Dispose(){Picture.Dispose();}
 }
 internal sealed class AgentRegionCapture:Form {
  [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  [DllImport("gdi32.dll")]static extern bool GdiFlush();
  delegate bool EnumWindow(IntPtr window,IntPtr state);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindow callback,IntPtr state);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out WindowsNative.Rectangle bounds,int size);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
  CaptureRaster desktop,dimmed;Point start,end;bool drawing,freeSelection;Rectangle hovered,pressedWindow;readonly List<Rectangle> windows;internal Rectangle Selection;
  const int EscapeId=0xA63;IntPtr escapeHandle;bool escapeRegistered,cancelled;
  readonly Pen border=new Pen(Color.FromArgb(187,167,242),2);int paintCount;bool cloaked;
  internal static bool FirstFramePrepared;internal static int PaintsBeforeReveal;
  internal static double ReadyMilliseconds,SceneReadyMilliseconds,PaintMilliseconds,PaintMaximumMilliseconds;internal static int PaintFrames;
  sealed class Frames:IDisposable{internal CaptureRaster Desktop,Dimmed;internal List<Rectangle> Windows;public void Dispose(){if(Desktop!=null)Desktop.Dispose();if(Dimmed!=null)Dimmed.Dispose();}}
  protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==EscapeId){CancelSelection();return;}if(m.Msg==0x14){m.Result=new IntPtr(1);return;}if(m.Msg!=0xF||!AgentApplication.Qa){base.WndProc(ref m);return;}var clock=Stopwatch.StartNew();base.WndProc(ref m);double ms=clock.Elapsed.TotalMilliseconds;PaintMilliseconds+=ms;PaintMaximumMilliseconds=Math.Max(PaintMaximumMilliseconds,ms);PaintFrames++;}
  protected override bool ProcessCmdKey(ref Message message,Keys keys){if(keys==Keys.Escape){CancelSelection();return true;}return base.ProcessCmdKey(ref message,keys);}
  void CancelSelection(){cancelled=true;DialogResult=DialogResult.Cancel;if(Visible)Close();}
  AgentRegionCapture(Rectangle bounds){
   windows=new List<Rectangle>();
   Text="AgentBloom · 框选截图";AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;KeyPreview=true;Cursor=Cursors.Cross;BackColor=Color.FromArgb(20,24,42);
   SetStyle(ControlStyles.UserPaint|ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
   HandleCreated+=delegate{escapeHandle=Handle;escapeRegistered=WindowsNative.RegisterHotKey(Handle,EscapeId,0x4000,0x1B);int disabled=1;DwmSetWindowAttribute(Handle,3,ref disabled,4);cloaked=DwmSetWindowAttribute(Handle,13,ref disabled,4)==0;};
   HandleDestroyed+=delegate{if(escapeRegistered){WindowsNative.UnregisterHotKey(escapeHandle,EscapeId);escapeRegistered=false;}};
   KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
   MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Right){CancelSelection();return;}if(e.Button!=MouseButtons.Left)return;start=end=e.Location;hovered=WindowAt(e.Location);pressedWindow=hovered;freeSelection=false;drawing=true;Capture=true;Invalidate();};
   MouseMove+=delegate(object sender,MouseEventArgs e){var previous=Area();bool wasFree=freeSelection;end=e.Location;if(drawing){if(Math.Abs(end.X-start.X)>=4||Math.Abs(end.Y-start.Y)>=4)freeSelection=true;}else hovered=WindowAt(end);var current=Area();if(!drawing&&previous==current)return;using(var dirty=new Region(previous)){dirty.Xor(current);if(!wasFree&&freeSelection)dirty.Union(current);AddBorder(dirty,previous);AddBorder(dirty,current);Invalidate(dirty,false);}};
   MouseUp+=delegate(object sender,MouseEventArgs e){if(!drawing||e.Button!=MouseButtons.Left)return;end=e.Location;if(Math.Abs(end.X-start.X)>=4||Math.Abs(end.Y-start.Y)>=4)freeSelection=true;Selection=Area();drawing=false;Capture=false;if(Selection.Width>3&&Selection.Height>3){DialogResult=DialogResult.OK;Close();}};
  }
  void SetFrames(Frames frames){desktop=frames.Desktop;dimmed=frames.Dimmed;windows.AddRange(frames.Windows);end=PointToClient(Cursor.Position);hovered=WindowAt(end);Invalidate();}
  protected override void Dispose(bool disposing){if(disposing){border.Dispose();}base.Dispose(disposing);}
  Rectangle Area(){return drawing?(freeSelection?Rectangle.Intersect(new Rectangle(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Abs(start.X-end.X),Math.Abs(start.Y-end.Y)),ClientRectangle):pressedWindow):hovered;}
  Rectangle WindowAt(Point point){foreach(var rectangle in windows)if(rectangle.Contains(point))return rectangle;var screen=Screen.FromPoint(new Point(point.X+Left,point.Y+Top)).Bounds;screen.Offset(-Left,-Top);return Rectangle.Intersect(screen,ClientRectangle);}
  internal static List<Rectangle> WindowBounds(Rectangle desktop){
   var result=new List<Rectangle>();uint own=(uint)Process.GetCurrentProcess().Id;
   EnumWindows(delegate(IntPtr handle,IntPtr state){uint process;GetWindowThreadProcessId(handle,out process);if(process==own||!WindowsNative.IsWindowVisible(handle)||IsIconic(handle)||handle==WindowsNative.GetShellWindow())return true;int cloaked;if(DwmGetWindowAttribute(handle,14,out cloaked,4)==0&&cloaked!=0)return true;WindowsNative.Rectangle native;if(DwmGetWindowAttribute(handle,9,out native,16)!=0&&!WindowsNative.GetWindowRect(handle,out native))return true;var rectangle=Rectangle.Intersect(Rectangle.FromLTRB(native.Left,native.Top,native.Right,native.Bottom),desktop);if(rectangle.Width>3&&rectangle.Height>3){rectangle.Offset(-desktop.Left,-desktop.Top);result.Add(rectangle);}return true;},IntPtr.Zero);return result;
  }
  static void AddBorder(Region dirty,Rectangle r){if(r.Width<=0||r.Height<=0)return;var outer=r;outer.Inflate(4,4);using(var ring=new Region(outer)){var inner=r;inner.Inflate(-4,-4);if(inner.Width>0&&inner.Height>0)ring.Exclude(inner);dirty.Union(ring);}}
  protected override void OnPaintBackground(PaintEventArgs e){}
  protected override void OnPaint(PaintEventArgs e){
   paintCount++;var r=Area();var clip=Rectangle.Intersect(e.ClipRectangle,ClientRectangle);
   if(dimmed==null)e.Graphics.Clear(BackColor);
   else{
    IntPtr target=e.Graphics.GetHdc();try{
    var bright=Rectangle.Intersect(r,clip);
    if(bright.Width<=0||bright.Height<=0)dimmed.Paint(target,clip);
    else{
     // Paint each pixel once; maximized window hover no longer draws the entire desktop twice.
     dimmed.Paint(target,Rectangle.FromLTRB(clip.Left,clip.Top,clip.Right,bright.Top));
     dimmed.Paint(target,Rectangle.FromLTRB(clip.Left,bright.Bottom,clip.Right,clip.Bottom));
     dimmed.Paint(target,Rectangle.FromLTRB(clip.Left,bright.Top,bright.Left,bright.Bottom));
     dimmed.Paint(target,Rectangle.FromLTRB(bright.Right,bright.Top,clip.Right,bright.Bottom));
     desktop.Paint(target,bright);
    }
    }finally{e.Graphics.ReleaseHdc(target);}
   }
   base.OnPaint(e);if(r.Width>0&&r.Height>0)e.Graphics.DrawRectangle(border,r.Left+1,r.Top+1,Math.Max(1,r.Width-3),Math.Max(1,r.Height-3));
  }
  void PresentPreparedFrame(){Refresh();GdiFlush();CaptureExclusion.Flush();PaintsBeforeReveal=paintCount;FirstFramePrepared=paintCount>0&&desktop!=null&&dimmed!=null&&cloaked;if(cloaked){int reveal=0;Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(Handle,13,ref reveal,4));cloaked=false;}}
  static Frames ReadDesktop(Rectangle screen){
   var previous=SetThreadDpiAwarenessContext((IntPtr)(-4));var frames=new Frames();try{CaptureExclusion.Flush();using(var original=new Bitmap(screen.Width,screen.Height,PixelFormat.Format32bppRgb))using(var shaded=new Bitmap(screen.Width,screen.Height,PixelFormat.Format32bppRgb)){
    using(var graphics=Graphics.FromImage(original))graphics.CopyFromScreen(screen.Left,screen.Top,0,0,screen.Size);
    using(var graphics=Graphics.FromImage(shaded)){graphics.CompositingMode=CompositingMode.SourceCopy;graphics.DrawImageUnscaled(original,0,0);graphics.CompositingMode=CompositingMode.SourceOver;using(var shade=new SolidBrush(Color.FromArgb(100,20,24,42)))graphics.FillRectangle(shade,0,0,screen.Width,screen.Height);}
    frames.Desktop=new CaptureRaster(original);frames.Dimmed=new CaptureRaster(shaded);
    // DWM queries can be cold after inactivity; keep them off the input dispatcher.
    frames.Windows=WindowBounds(screen);
   }return frames;}catch{frames.Dispose();throw;}finally{SetThreadDpiAwarenessContext(previous);}
  }
  internal static async Task<CapturedRegion> TakeAsync(Action selectorShown=null,Action selectorClosed=null){
   var previous=SetThreadDpiAwarenessContext((IntPtr)(-4));var preparation=Stopwatch.StartNew();ReadyMilliseconds=SceneReadyMilliseconds=PaintMilliseconds=PaintMaximumMilliseconds=0;PaintFrames=0;FirstFramePrepared=false;PaintsBeforeReveal=0;
   try{
    var screen=SystemInformation.VirtualScreen;if((long)screen.Width*screen.Height>120000000)throw new InvalidOperationException("桌面总像素过大，请暂时减少连接的屏幕后重试。");
    using(var exclusion=new CaptureExclusion(System.Windows.Application.Current.Windows.Cast<System.Windows.Window>().ToArray()))
    try{using(var picker=new AgentRegionCapture(screen)){
     var handle=picker.Handle;
     using(var frames=await Task.Run(()=>ReadDesktop(screen))){SceneReadyMilliseconds=preparation.Elapsed.TotalMilliseconds;if(picker.cancelled)return null;picker.SetFrames(frames);
      picker.Shown+=delegate{if(selectorShown!=null)selectorShown();picker.PresentPreparedFrame();ReadyMilliseconds=preparation.Elapsed.TotalMilliseconds;WindowsNative.ActivateEditor(picker.Handle);picker.Activate();picker.Focus();};
      var result=picker.ShowDialog();if(result!=DialogResult.OK)return null;var area=picker.Selection;area.Offset(screen.Left,screen.Top);return new CapturedRegion(frames.Desktop.Crop(picker.Selection),area);
     }
    }}finally{if(selectorClosed!=null)selectorClosed();}
   }finally{SetThreadDpiAwarenessContext(previous);}
  }
 }
}
