using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenLine {
 // The display stays unchanged; DWM omits these owned windows from the pixel copy.
 internal sealed class CaptureExclusion:IDisposable {
  [DllImport("user32.dll",SetLastError=true)]static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
  [DllImport("user32.dll",SetLastError=true)]static extern bool GetWindowDisplayAffinity(IntPtr window,out uint affinity);
  [DllImport("dwmapi.dll")]static extern int DwmFlush();
  readonly Dictionary<IntPtr,uint> previous=new Dictionary<IntPtr,uint>();
  internal static CaptureExclusion Current;
  internal CaptureExclusion(IEnumerable<Window> windows){
   Current=this;try{foreach(var window in windows){if(window.IsVisible)Add(new WindowInteropHelper(window).Handle);}}
   catch{Dispose();throw;}
  }
  internal void Add(IntPtr handle){if(handle==IntPtr.Zero||previous.ContainsKey(handle))return;uint affinity;if(!GetWindowDisplayAffinity(handle,out affinity))affinity=0;previous[handle]=affinity;if(!SetWindowDisplayAffinity(handle,0x11))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"当前窗口无法排除截图，请查看日志。");}
  internal static void Flush(){DwmFlush();}
  public void Dispose(){if(Current==this)Current=null;foreach(var pair in previous)SetWindowDisplayAffinity(pair.Key,pair.Value);previous.Clear();}
 }
}
