using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ScreenLine {
 internal sealed class AgentHub:Window {
  readonly AgentSession session;readonly Canvas root=new Canvas(),ring=new Canvas();readonly RotateTransform rotation=new RotateTransform();
  readonly TranslateTransform entrance=new TranslateTransform(-260,0);readonly List<RotateTransform> counters=new List<RotateTransform>();readonly List<Border> badges=new List<Border>();readonly List<TranslateTransform> bobs=new List<TranslateTransform>();bool exposed;bool? quietState;string displayedAgent;IntPtr windowHandle;
  internal AgentHub(AgentSession s){
   session=s;Title="AgentBloom Agent 转轮";Width=260;Height=260;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
   Content=root;root.RenderTransform=entrance;root.Children.Add(ring);ring.RenderTransform=rotation;Canvas.SetLeft(ring,18);Canvas.SetTop(ring,18);
   var orbitHit=new Ellipse{Width=326,Height=326,Stroke=LightTheme.Brush("#01FFFFFF"),StrokeThickness=42};Canvas.SetLeft(orbitHit,-163);Canvas.SetTop(orbitHit,-163);ring.Children.Add(orbitHit);
   var orbit=OrbitArtwork();Canvas.SetLeft(orbit,-183);Canvas.SetTop(orbit,-183);ring.Children.Add(orbit);
   for(int i=0;i<AgentFaces.Names.Length;i++){
    int index=i;string agent=AgentFaces.Names[i];double a=(45+i*360.0/7)*Math.PI/180;
    var badge=AgentFaces.Badge(i,43);badges.Add(badge);var bob=new TranslateTransform();bobs.Add(bob);badge.RenderTransform=bob;var counter=new RotateTransform();counters.Add(counter);var scale=new ScaleTransform(1,1);var transform=new TransformGroup();transform.Children.Add(counter);transform.Children.Add(scale);
    var button=new Button{Width=70,Height=70,Content=badge,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,RenderTransform=transform,RenderTransformOrigin=new Point(.5,.5),ToolTip=agent+" · 点击换挂饰",FocusVisualStyle=null};
    button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent' CornerRadius='25'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
    System.Windows.Automation.AutomationProperties.SetAutomationId(button,"agentbloom-theme-"+agent);
    Canvas.SetLeft(button,163*Math.Cos(a)-35);Canvas.SetTop(button,163*Math.Sin(a)-35);ring.Children.Add(button);
    button.MouseEnter+=delegate{scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(1.14,160));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(1.14,160));};
    button.MouseLeave+=delegate{scale.BeginAnimation(ScaleTransform.ScaleXProperty,Motion(1,180));scale.BeginAnimation(ScaleTransform.ScaleYProperty,Motion(1,180));};
    var playfulTurn=new RotateTransform();var faceMotion=new TransformGroup();faceMotion.Children.Add(bob);faceMotion.Children.Add(playfulTurn);badge.RenderTransform=faceMotion;badge.RenderTransformOrigin=new Point(.5,.5);
    button.Click+=delegate{if(session.Busy)return;session.SelectAgent(agent);if(!AgentFaces.Quiet){playfulTurn.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,45,TimeSpan.FromMilliseconds(160)){AutoReverse=true,EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}Burst(button.TranslatePoint(new Point(35,35),root),index);};
   }
   root.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){e.Handled=true;Turn(e.Delta);};
   var menu=new ContextMenu();var capture=new MenuItem{Header="截一张    ·"};capture.Click+=async delegate{await session.Capture();};menu.Items.Add(capture);var quit=new MenuItem{Header="退出 AgentBloom"};quit.Click+=async delegate{await session.Exit();};menu.Items.Add(quit);root.ContextMenu=menu;
   SourceInitialized+=delegate{windowHandle=new WindowInteropHelper(this).Handle;WindowsNative.PrepareOverlay(windowHandle);HwndSource.FromHwnd(windowHandle).AddHook(MouseMessage);};session.Controller.Shelf.PointerSampled+=Pointer;session.Changed+=Refresh;Closed+=delegate{session.Controller.Shelf.PointerSampled-=Pointer;session.Changed-=Refresh;};Refresh();
  }
  internal static Image OrbitArtwork(){
   // One frozen vector drawing travels with the wheel; the hollow stays click-through.
   var drawing=new DrawingGroup();var center=new Point(183,183);
   using(var ink=drawing.Open()){
    ink.DrawEllipse(null,new Pen(LightTheme.Brush("#14A598C5"),24),new Point(183,185),163,163);
    var pearl=new LinearGradientBrush();pearl.StartPoint=new Point(0,0);pearl.EndPoint=new Point(1,1);
    pearl.GradientStops.Add(new GradientStop(Color.FromRgb(255,255,255),0));
    pearl.GradientStops.Add(new GradientStop(Color.FromRgb(237,230,249),.48));
    pearl.GradientStops.Add(new GradientStop(Color.FromRgb(252,249,255),.72));
    pearl.GradientStops.Add(new GradientStop(Color.FromRgb(224,214,242),1));
    ink.DrawEllipse(null,new Pen(pearl,17),center,163,163);
    ink.DrawEllipse(null,new Pen(LightTheme.Brush("#D1C3E6"),.9),center,172,172);
    ink.DrawEllipse(null,new Pen(LightTheme.Brush("#FFFFFF"),1.4),center,169.8,169.8);
    ink.DrawEllipse(null,new Pen(LightTheme.Brush("#C2AFDA"),.8),center,153.8,153.8);
    ink.DrawEllipse(null,new Pen(LightTheme.Brush("#FFFFFF"),.8),center,155.8,155.8);
    for(int i=0;i<7;i++){
     double angle=45+(i+.5)*360.0/7;
     ink.PushTransform(new RotateTransform(angle,183,183));
     ink.DrawEllipse(LightTheme.Brush("#AC96CB"),null,new Point(346,183),2.2,2.2);
     ink.PushTransform(new RotateTransform(-24,346,177));
     ink.DrawEllipse(LightTheme.Brush("#C5B5DF"),null,new Point(346,177),1.5,3.5);ink.Pop();
     ink.PushTransform(new RotateTransform(24,346,189));
     ink.DrawEllipse(LightTheme.Brush("#E4CADA"),null,new Point(346,189),1.5,3.5);ink.Pop();
     ink.DrawEllipse(Brushes.White,null,new Point(345.4,182.4),.7,.7);
     ink.Pop();
    }
   }
   drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();
   return new Image{Width=366,Height=366,Source=image,Stretch=Stretch.None,IsHitTestVisible=false};
  }
  IntPtr MouseMessage(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled){if(message==0x20A&&exposed&&!AgentApplication.Capturing){Turn((short)((w.ToInt64()>>16)&0xffff));handled=true;}return IntPtr.Zero;}
  void Turn(int delta){if(delta==0)return;double target=rotation.Angle+(delta>0?-360.0/7:360.0/7);rotation.BeginAnimation(RotateTransform.AngleProperty,Motion(target,350));foreach(var counter in counters)counter.BeginAnimation(RotateTransform.AngleProperty,Motion(-target,350));}
  void Pointer(WindowsNative.Point cursor){if(windowHandle==IntPtr.Zero)return;if(!exposed||AgentApplication.Capturing){WindowsNative.PassMouse(windowHandle,true);return;}var local=root.PointFromScreen(new Point(cursor.X,cursor.Y));var hit=root.InputHitTest(local);WindowsNative.PassMouse(windowHandle,hit==null||hit is Canvas);}
  internal static DoubleAnimation Motion(double target,int milliseconds){return new DoubleAnimation(target,TimeSpan.FromMilliseconds(AgentFaces.Quiet||!SystemParameters.ClientAreaAnimation?1:milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};}
  internal void SyncVisibility(bool shown){
   exposed=shown;System.Windows.Automation.AutomationProperties.SetItemStatus(this,shown?"expanded":"collapsed");var handle=new WindowInteropHelper(this).Handle;WindowsNative.PassMouse(handle,!shown);
   if(shown){
     var display=session.Controller.Shelf.Display;double dpi=WindowsNative.MonitorScale(display);
     WindowsNative.SetWindowPos(handle,new IntPtr(-1),display.WorkingArea.Left,display.WorkingArea.Top,(int)(260*dpi),(int)(260*dpi),0x10);
   }
   var animX=Motion(shown?0:-260,shown?170:130);
   var animO=Motion(shown?1:0,shown?170:130);
   Refresh();
   entrance.BeginAnimation(TranslateTransform.XProperty,animX);
   BeginAnimation(OpacityProperty,animO);
  }
  void Refresh(){if(displayedAgent!=session.Projects.Index.SelectedAgent){displayedAgent=session.Projects.Index.SelectedAgent;for(int i=0;i<badges.Count;i++){badges[i].BorderBrush=LightTheme.Brush(AgentFaces.Names[i]==displayedAgent?"#A695CE":"#FFFFFF");badges[i].BorderThickness=new Thickness(AgentFaces.Names[i]==displayedAgent?3:2);}}bool quiet=AgentFaces.Quiet||!exposed;if(quietState==quiet)return;quietState=quiet;for(int i=0;i<bobs.Count;i++){bobs[i].BeginAnimation(TranslateTransform.YProperty,null);bobs[i].Y=0;if(!quiet&&SystemParameters.ClientAreaAnimation)bobs[i].BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-2-i%3,TimeSpan.FromSeconds(2+i*.23)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,BeginTime=TimeSpan.FromSeconds(i*.6)});}}
  void Burst(Point center,int index){
   if(AgentFaces.Quiet)return;string[] colors={"#B2A0E0","#D5BB87","#E6AB7D","#91BACA","#C4ABDA","#9BBAA4","#A6B0D9"};
   string[] glyphs={"✧","◌","✿","○","◇","❧","☾"};for(int i=0;i<10;i++){double a=i*Math.PI/5;var star=new TextBlock{Text=glyphs[index],FontSize=12+i%3*3,Foreground=LightTheme.Brush(colors[index]),IsHitTestVisible=false};var drift=new TranslateTransform();star.RenderTransform=drift;Canvas.SetLeft(star,center.X-8);Canvas.SetTop(star,center.Y-8);root.Children.Add(star);drift.BeginAnimation(TranslateTransform.XProperty,Motion(32*Math.Cos(a),480));drift.BeginAnimation(TranslateTransform.YProperty,Motion(32*Math.Sin(a),480));var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(520));fade.Completed+=delegate{root.Children.Remove(star);};star.BeginAnimation(OpacityProperty,fade);}
  }
 }
 internal sealed class AgentBookmark:Window {
  readonly TranslateTransform peek=new TranslateTransform(-23,0);
  internal static double SavedTop(double? position,Rect area,double height){double travel=Math.Max(0,area.Height-height);return area.Top+(position.HasValue&&!double.IsNaN(position.Value)&&!double.IsInfinity(position.Value)?Math.Max(0,Math.Min(1,position.Value))*travel:Math.Min(travel,area.Height*.38));}
  internal AgentBookmark(AgentSession session){
   Title="AgentBloom 收集箱书签";Width=96;Height=170;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
   Left=SystemParameters.WorkArea.Left;Top=SavedTop(session.Projects.Index.BookmarkPosition,SystemParameters.WorkArea,Height);
   var root=new Canvas{RenderTransform=peek};var hit=new Grid{Background=LightTheme.Brush("#01FFFFFF"),Cursor=Cursors.Hand};hit.Children.Add(root);Content=hit;var tab=new System.Windows.Shapes.Path{Data=Geometry.Parse("M0 8 Q0 0 8 0 H41 Q50 0 50 10 V111 L26 100 0 111 Z"),Fill=LightTheme.Brush("#F3EFFA"),Stroke=LightTheme.Brush("#C9BDDF"),StrokeThickness=1,Effect=new DropShadowEffect{BlurRadius=12,ShadowDepth=2,Opacity=.15},Cursor=Cursors.Hand};Canvas.SetTop(tab,10);root.Children.Add(tab);
   var label=new TextBlock{Text="收\n集",FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=LightTheme.Brush("#8B7AAE"),LineHeight=25,TextAlignment=TextAlignment.Center,IsHitTestVisible=false};Canvas.SetLeft(label,23);Canvas.SetTop(label,37);root.Children.Add(label);
   hit.MouseEnter+=delegate{peek.BeginAnimation(TranslateTransform.XProperty,AgentHub.Motion(-3,160));};hit.MouseLeave+=delegate{peek.BeginAnimation(TranslateTransform.XProperty,AgentHub.Motion(-23,210));};
   bool pressed=false,dragged=false;double startTop=0,scale=1;WindowsNative.Point start=new WindowsNative.Point();
   hit.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;if(!hit.CaptureMouse())return;pressed=true;dragged=false;start=WindowsNative.Cursor();startTop=Top;var source=PresentationSource.FromVisual(this);scale=source==null?1:source.CompositionTarget.TransformToDevice.M22;};
   hit.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){if(!pressed)return;var cursor=WindowsNative.Cursor();double dy=(cursor.Y-start.Y)/scale;if(!dragged&&Math.Abs(dy)<SystemParameters.MinimumVerticalDragDistance&&Math.Abs((cursor.X-start.X)/scale)<SystemParameters.MinimumHorizontalDragDistance)return;dragged=true;e.Handled=true;var area=SystemParameters.WorkArea;Top=Math.Max(area.Top,Math.Min(Math.Max(area.Top,area.Bottom-ActualHeight),startTop+dy));};
   hit.PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(!pressed)return;e.Handled=true;bool moved=dragged;pressed=false;hit.ReleaseMouseCapture();if(!moved){session.ToggleBoard();return;}var area=SystemParameters.WorkArea;double travel=Math.Max(0,area.Height-ActualHeight);session.Projects.Index.BookmarkPosition=travel==0?0:Math.Max(0,Math.Min(1,(Top-area.Top)/travel));try{session.Projects.Save();}catch(Exception error){session.Controller.Gallery.Store.Log(error);}};
   hit.LostMouseCapture+=delegate{pressed=false;dragged=false;};
   var menu=new ContextMenu();var quit=new MenuItem{Header="退出 AgentBloom"};quit.Click+=async delegate{await session.Exit();};menu.Items.Add(quit);hit.ContextMenu=menu;
   SourceInitialized+=delegate{if(CaptureExclusion.Current!=null)CaptureExclusion.Current.Add(new WindowInteropHelper(this).Handle);};
   System.Windows.Automation.AutomationProperties.SetName(root,"打开收集箱");
  }
 }
}
