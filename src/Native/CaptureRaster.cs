using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenLine {
 // One immutable GDI surface per prepared desktop image; no per-paint bitmap conversion.
 internal sealed class CaptureRaster:IDisposable {
  [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr item);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll",SetLastError=true)]static extern bool BitBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,uint operation);
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
  IntPtr dc,bitmap,previous;
  internal CaptureRaster(Bitmap source){
   try{
    bitmap=source.GetHbitmap();IntPtr screen=GetDC(IntPtr.Zero);
    if(screen==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    try{dc=CreateCompatibleDC(screen);}finally{ReleaseDC(IntPtr.Zero,screen);}
    if(dc==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    previous=SelectObject(dc,bitmap);if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
   }catch{Dispose();throw;}
  }
  internal void Paint(IntPtr target,Rectangle area){
   if(area.Width>0&&area.Height>0&&!BitBlt(target,area.X,area.Y,area.Width,area.Height,dc,area.X,area.Y,0x00CC0020))throw new Win32Exception(Marshal.GetLastWin32Error());
  }
  internal Bitmap Crop(Rectangle area){
   var result=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppRgb);
   try{using(var graphics=Graphics.FromImage(result)){IntPtr target=graphics.GetHdc();try{if(!BitBlt(target,0,0,area.Width,area.Height,dc,area.X,area.Y,0x00CC0020))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{graphics.ReleaseHdc(target);}}return result;}
   catch{result.Dispose();throw;}
  }
  public void Dispose(){if(dc!=IntPtr.Zero){if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1))SelectObject(dc,previous);DeleteDC(dc);dc=IntPtr.Zero;}if(bitmap!=IntPtr.Zero){DeleteObject(bitmap);bitmap=IntPtr.Zero;}}
 }
}
