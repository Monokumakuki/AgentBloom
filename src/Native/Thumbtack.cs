using System.Windows.Controls;
namespace ScreenLine {
 internal sealed class Thumbtack : Canvas {
  public LineTheme Appearance {get;private set;}
  public Thumbtack(){Width=Height=32;IsHitTestVisible=false;}
  public void Apply(LineTheme theme,int colorIndex){Appearance=theme;Children.Clear();var face=AgentFaces.Badge(colorIndex,38);Children.Add(face);SetLeft(face,-3);SetTop(face,-3);}
 }
}