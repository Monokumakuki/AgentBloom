using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace ScreenLine {
 internal sealed class CapturePresentation:IDisposable {
  readonly Dictionary<Window,bool> input=new Dictionary<Window,bool>();readonly Dictionary<IntPtr,bool> pass=new Dictionary<IntPtr,bool>();readonly AgentSession session;
  internal CapturePresentation(AgentSession s){session=s;AgentApplication.Capturing=true;session.Controller.Shelf.BeginCaptureOverlay();foreach(var window in Application.Current.Windows.Cast<Window>().ToArray()){input[window]=window.IsHitTestVisible;window.IsHitTestVisible=false;var h=new WindowInteropHelper(window).Handle;if(h!=IntPtr.Zero){pass[h]=WindowsNative.PassesMouse(h);WindowsNative.PassMouse(h,true);}}}
  internal void AboveSelector(){if(session.Board!=null&&session.Board.IsVisible){var board=new WindowInteropHelper(session.Board).Handle;WindowsNative.PassMouse(board,true);WindowsNative.SetWindowPos(board,new IntPtr(-1),0,0,0,0,0x13);}foreach(var window in Application.Current.Windows.Cast<Window>().ToArray()){if(!window.IsVisible)continue;if(!(window is ShelfWindow||window is AgentHub||window is AgentBookmark||window is PinnedImageWindow))continue;var h=new WindowInteropHelper(window).Handle;if(h!=IntPtr.Zero){WindowsNative.PassMouse(h,true);WindowsNative.SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x13);}}}
  internal void RestoreBoardLayer(){if(session.Board!=null){var handle=new WindowInteropHelper(session.Board).Handle;if(handle!=IntPtr.Zero)WindowsNative.SetWindowPos(handle,new IntPtr(-2),0,0,0,0,0x13);}}
  public void Dispose(){RestoreBoardLayer();foreach(var pair in input)if(Application.Current.Windows.Cast<Window>().Contains(pair.Key))pair.Key.IsHitTestVisible=pair.Value;foreach(var pair in pass)WindowsNative.PassMouse(pair.Key,pair.Value);AgentApplication.Capturing=false;session.Controller.Shelf.EndCaptureOverlay();}
 }
}
