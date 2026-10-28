using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScreenLine {
 internal static class AgentFaces {
  internal static bool Quiet;
  internal static string Decoration="flowers";
  static readonly BitmapSource[] images=new BitmapSource[7];
  internal static readonly string[] Names=AgentProjects.AgentNames;
  internal static Func<string,int> Resolve;
  internal static int ForPath(string path){return Resolve==null?0:Math.Max(0,Resolve(path));}
  internal static Border Badge(int index,double size){
   string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui","assets","agents",Names[Math.Abs(index)%Names.Length]+".jpg");
   int slot=Math.Abs(index)%Names.Length;if(images[slot]==null){var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(path);image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=128;image.EndInit();image.Freeze();images[slot]=image;}
   return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size/2),BorderBrush=Brushes.White,BorderThickness=new Thickness(2),Background=new ImageBrush(images[slot]){Stretch=Stretch.UniformToFill},ToolTip=Names[slot],CacheMode=new BitmapCache(),Effect=new DropShadowEffect{BlurRadius=9,ShadowDepth=2,Opacity=.15}};
  }
 }
}
