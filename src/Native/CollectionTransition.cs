using System;
using System.Windows;
using System.Windows.Media;
namespace ScreenLine {
 internal static class BookShape {
  internal static Geometry Geometry(double width,double height){
   double edge=Math.Max(1,width-48),y=height*.34;var shape=new GeometryGroup{FillRule=FillRule.Nonzero};shape.Children.Add(new RectangleGeometry(new Rect(0,0,edge,height),20,20));
   var figure=new PathFigure{StartPoint=new Point(edge-2,y),IsClosed=true};foreach(var point in new[]{new Point(width,y+11),new Point(width,y+158),new Point(edge+24,y+144),new Point(edge-2,y+158)})figure.Segments.Add(new LineSegment(point,true));shape.Children.Add(new PathGeometry(new[]{figure}));shape.Freeze();return shape;
  }
 }
}
