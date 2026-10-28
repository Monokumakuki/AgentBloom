using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
namespace ScreenLine {
 internal static class RopeLine {
  public static void Apply(Path rope,LineTheme theme){
   rope.IsHitTestVisible=false;rope.StrokeThickness=13;rope.StrokeStartLineCap=rope.StrokeEndLineCap=PenLineCap.Round;
   if(AgentFaces.Decoration=="floating"){rope.Stroke=Brushes.Transparent;return;}
   if(System.Array.IndexOf(AgentFaces.Names,AgentFaces.Decoration)>=0){rope.Stroke=AgentPattern(AgentFaces.Decoration,theme);return;}
   if(AgentFaces.Decoration=="stars"){
    var stars=new DrawingGroup();stars.Children.Add(new GeometryDrawing(null,new Pen(LightTheme.Brush("#C5BFDC"),.8),new LineGeometry(new Point(0,6.5),new Point(76,6.5))));
    stars.Children.Add(new GeometryDrawing(LightTheme.Brush("#E0C58D"),null,Geometry.Parse("M25,0 L27,4.5 32,6.5 27,8.5 25,13 23,8.5 18,6.5 23,4.5 Z")));
    stars.Children.Add(new GeometryDrawing(null,new Pen(LightTheme.Brush("#C4BADD"),1),new EllipseGeometry(new Point(58,6.5),3,3)));
    var starBrush=new DrawingBrush(stars){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,ViewboxUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,76,13),Viewbox=new Rect(0,0,76,13),Stretch=Stretch.Fill};starBrush.Freeze();rope.Stroke=starBrush;return;
   }
   var garden=new DrawingGroup();garden.Children.Add(new GeometryDrawing(null,new Pen(LightTheme.Brush("#AABAA9"),1.4),new LineGeometry(new Point(0,6.5),new Point(76,6.5))));
   garden.Children.Add(new GeometryDrawing(LightTheme.Brush("#BBCDB9"),null,Geometry.Parse("M18,6 Q22,0 28,2 Q28,7 18,6 M54,7 Q58,12 64,10 Q62,5 54,7")));
   var pink=LightTheme.Brush("#DBB6CC");foreach(var point in new[]{new Point(39,3),new Point(42,5),new Point(41,9),new Point(37,9),new Point(36,5)})garden.Children.Add(new GeometryDrawing(pink,null,new EllipseGeometry(point,2.2,2.2)));
   garden.Children.Add(new GeometryDrawing(LightTheme.Brush("#FFFAE9"),null,new EllipseGeometry(new Point(39,6),1.6,1.6)));
   var brush=new DrawingBrush(garden){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,ViewboxUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,76,13),Viewbox=new Rect(0,0,76,13),Stretch=Stretch.Fill};brush.Freeze();rope.Stroke=brush;
  }
  static DrawingBrush AgentPattern(string agent,LineTheme theme){
   var art=new DrawingGroup();string color=theme.RopeColor;
   var ink=LightTheme.Brush(color);var pen=new Pen(ink,1);
   art.Children.Add(new GeometryDrawing(null,new Pen(ink,.7),Geometry.Parse(agent=="deepseek"?"M0,6.5 Q10,1 19,6.5 T38,6.5 T57,6.5 T76,6.5":"M0,6.5 L76,6.5")));
   if(agent=="gemini"){
    art.Children.Add(new GeometryDrawing(ink,null,Geometry.Parse("M25,0 L28,4 34,6.5 28,9 25,13 22,9 16,6.5 22,4 Z")));
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#EAD5F1"),null,Geometry.Parse("M25,0 L25,13 22,9 16,6.5 22,4 Z")));
    art.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(58,6.5),3,3)));
   }else if(agent=="grok"){
    art.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(33,6.5),12,4.5)));
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#DDC38E"),null,new EllipseGeometry(new Point(43,3),2.4,2.4)));
    art.Children.Add(new GeometryDrawing(ink,null,Geometry.Parse("M61,2 L62,5 66,6.5 62,8 61,11 60,8 56,6.5 60,5 Z")));
   }else if(agent=="claude"){
    foreach(var point in new[]{new Point(36,3),new Point(40,5),new Point(39,9),new Point(34,9),new Point(33,5)})art.Children.Add(new GeometryDrawing(LightTheme.Brush("#E5B58F"),null,new EllipseGeometry(point,2.5,2.5)));
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#FFF0D5"),null,new EllipseGeometry(new Point(36.5,6.3),1.8,1.8)));
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#C4C3A7"),null,Geometry.Parse("M58,6 Q62,1 67,2 Q67,8 58,6")));
   }else if(agent=="deepseek"){
    art.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(28,5.5),4,4)));
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#B5CFDC"),null,new EllipseGeometry(new Point(53,8),2.5,2.5)));
   }else if(agent=="qwen"){
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#D5C3E5"),null,Geometry.Parse("M33,2 Q39,-1 45,2 L43,8 Q39,10 35,8 Z")));
    art.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M39,8 V13 M36,2 H42 M35,5 H43 M60,3 L63,6 60,9 57,6 Z")));
   }else if(agent=="gpt"){
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#ACC7B3"),null,Geometry.Parse("M27,7 Q30,-1 39,1 Q41,9 27,7 M45,6 Q50,13 57,11 Q56,5 45,6")));
    art.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M27,7 L37,3 M45,6 L54,10")));
   }else{
    art.Children.Add(new GeometryDrawing(LightTheme.Brush("#B4BBDD"),null,Geometry.Parse("M30,0 C19,0 19,13 30,13 C25,10 25,3 30,0 Z")));
    art.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M56,3 V10 M56,3 L63,1 V8")));
    art.Children.Add(new GeometryDrawing(ink,null,new EllipseGeometry(new Point(54.5,10),2,1.4)));
    art.Children.Add(new GeometryDrawing(ink,null,new EllipseGeometry(new Point(61.5,8),2,1.4)));
   }
   var brush=new DrawingBrush(art){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,ViewboxUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,76,13),Viewbox=new Rect(0,0,76,13),Stretch=Stretch.Fill};brush.Freeze();return brush;
  }
 }
}
