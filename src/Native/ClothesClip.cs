using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ScreenLine {
 internal sealed class ClothesClip : Canvas {
  readonly RotateTransform nod=new RotateTransform(0,8,18);int index=-1;bool active=true;Border face;
  public LineTheme Appearance {get;private set;}
  public ClothesClip():this(LineTheme.All[0],0){}
  public ClothesClip(LineTheme theme,int colorIndex){Width=16;Height=30;IsHitTestVisible=false;Apply(theme,colorIndex);}
  public void Apply(LineTheme theme,int colorIndex){
   Appearance=theme;if(index!=colorIndex||face==null){index=colorIndex;Children.Clear();face=AgentFaces.Badge(index,40);face.RenderTransform=nod;Children.Add(face);SetLeft(face,-12);SetTop(face,-5);}face.BorderBrush=LightTheme.Brush(theme.RopeHighlight);Animate();
  }
  internal void SetActive(bool value){if(active==value)return;active=value;Animate();}
  void Animate(){
   nod.BeginAnimation(RotateTransform.AngleProperty,null);nod.Angle=0;
   if(active&&!AgentFaces.Quiet&&SystemParameters.ClientAreaAnimation){var motion=new DoubleAnimation(-1.2,1.2,TimeSpan.FromSeconds(3.2+(index%7)*.37)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,BeginTime=TimeSpan.FromMilliseconds(index*173)};nod.BeginAnimation(RotateTransform.AngleProperty,motion);}
  }
  public void Open(){nod.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,-9,TimeSpan.FromMilliseconds(160)){AutoReverse=true});}
  public void Reset(){Apply(Appearance,index);}
 }
}
