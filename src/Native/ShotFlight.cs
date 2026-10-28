using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ScreenLine
{
    internal sealed class ShotFlight : Window
    {
        readonly ShotCard card;
        readonly Rect screenOrigin;
        readonly Canvas stage = new Canvas(), frame = new Canvas();
        readonly FlightPaper border = new FlightPaper();
        readonly ClothesClip clip = new ClothesClip();
        readonly RotateTransform turn = new RotateTransform();
        readonly TranslateTransform travel=new TranslateTransform(),clipPosition=new TranslateTransform();
        readonly SolidColorBrush paper=new SolidColorBrush(Colors.White);
        readonly DropShadowEffect shadow = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4 };
        readonly Stopwatch clock = new Stopwatch();
        readonly Action landed;
        readonly Func<Rect> basketTarget;
        Rect origin;
        bool completed, arrived;
        readonly bool framedOrigin;
        int renderedFrames;
        TimeSpan lastRendering=TimeSpan.MinValue;
        internal readonly TaskCompletionSource<bool> PresentedFrame=new TaskCompletionSource<bool>();
        internal static Rect LastOrigin=Rect.Empty;
        internal static double LastMaxFrameGap;double previousFrame;
        internal static int LastTravelFrames;double previousWidth=-1;
        public bool Falling { get; private set; }
        public bool Collecting { get { return basketTarget != null; } }

        public ShotFlight(Forms.Screen screen, ShotCard card, Rect origin, bool falling, Action landed, BitmapSource image = null, Func<Rect> basketTarget = null, bool framedOrigin = false)
        {
            this.card = card;
            this.framedOrigin=framedOrigin;
            Title="AgentBloom · 图片飞挂";
            ApplyTheme(card.Appearance);
            screenOrigin = origin;
            if(AgentApplication.Qa){LastOrigin=origin;LastMaxFrameGap=0;LastTravelFrames=0;}
            this.landed = landed;
            this.basketTarget = basketTarget;
            Falling = falling;
            double scale = WindowsNative.MonitorScale(screen);
            // The flight never descends below its source; avoid a full-screen layered surface.
            int surfaceHeight=falling||basketTarget!=null?screen.Bounds.Height:(int)Math.Min(screen.Bounds.Height,Math.Ceiling(Math.Max(360*scale,origin.Bottom-screen.Bounds.Top+64*scale)));
            Width = screen.Bounds.Width / scale;
            Height = surfaceHeight / scale;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            IsHitTestVisible = false;
            Content = stage;
            stage.Children.Add(frame);
            var motion=new TransformGroup();motion.Children.Add(turn);motion.Children.Add(travel);frame.RenderTransform=motion;
            border.Picture = image ?? card.Item.Image.Thumbnail;
            System.Windows.Automation.AutomationProperties.SetName(border,"飞挂中的截图");
            RenderOptions.SetBitmapScalingMode(border,BitmapScalingMode.LowQuality);
            border.Effect = shadow;
            frame.Children.Add(border);
            frame.Children.Add(clip);
            clip.Opacity=0;clip.RenderTransform=clipPosition;
            SourceInitialized += delegate
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                WindowsNative.PrepareOverlay(handle);
                WindowsNative.PassMouse(handle, true);
                WindowsNative.SetWindowPos(handle, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, surfaceHeight, 0x10);
                this.origin = Local(screenOrigin);
                Render(null, EventArgs.Empty);
            };
            Loaded+=delegate{clock.Start();CompositionTarget.Rendering+=Render;};
            Closed += delegate
            {
                CompositionTarget.Rendering -= Render;
                PresentedFrame.TrySetResult(false);
                Complete();
            };
        }

        Rect Local(Rect rectangle)
        {
            return new Rect(PointFromScreen(rectangle.TopLeft), PointFromScreen(rectangle.BottomRight));
        }

        public void ApplyTheme(LineTheme theme) { clip.Apply(theme, card.ColorIndex); }

        void Complete()
        {
            if (completed) return;
            completed = true;
            if (Collecting && !arrived) return;
            landed();
        }

        void Render(object sender, EventArgs args)
        {
            if(args is RenderingEventArgs){var time=((RenderingEventArgs)args).RenderingTime;if(time==lastRendering)return;lastRendering=time;}
            if(AgentApplication.Qa&&args is RenderingEventArgs){double now=((RenderingEventArgs)args).RenderingTime.TotalMilliseconds;if(previousFrame>0)LastMaxFrameGap=Math.Max(LastMaxFrameGap,now-previousFrame);previousFrame=now;}
            if(args is RenderingEventArgs&&++renderedFrames>=2)PresentedFrame.TrySetResult(true);
            if (!Falling && !Collecting && card.IsDisposed) { Close(); return; }
            double seconds = clock.Elapsed.TotalSeconds;
            if (Collecting) { RenderCollection(Math.Min(1, seconds / 0.64)); return; }
            double duration=framedOrigin?0.58:0.65,fadeDuration=framedOrigin?0.14:0.16;
            double progress = Math.Min(1, seconds / (Falling ? 0.55 : duration));
            // 五次曲线让起飞与抵达的速度、加速度都平滑归零。
            double ease = progress * progress * progress * (10 + progress * (-15 + 6 * progress));
            Rect target = Falling ? origin : Local(card.PhotoRect());
            double width = origin.Width + (target.Width - origin.Width) * ease;
            if(AgentApplication.Qa&&args is RenderingEventArgs&&Math.Abs(width-previousWidth)>.1){LastTravelFrames++;previousWidth=width;}
            double height = origin.Height + (target.Height - origin.Height) * ease;
            double x = origin.X + origin.Width / 2 + (target.X + target.Width / 2 - origin.X - origin.Width / 2) * ease;
            double y = origin.Y + (target.Y - origin.Y) * ease - 30 * Math.Sin(Math.PI * ease);
            double chrome = Math.Max(0, Math.Min(1, (ease - 0.3) / 0.6));
            chrome = chrome * chrome * (3 - 2 * chrome);
            turn.Angle = card.Tilt * ease;
            if (Falling)
            {
                width = origin.Width; height = origin.Height;
                x = origin.X + width / 2;
                y = origin.Y + 520 * progress * progress;
                turn.Angle = card.Tilt + (card.Tilt < 0 ? -32 : 32) * progress * progress;
                chrome = 1;
                Opacity = 1 - progress * progress;
            }
            travel.X=x-width/2;travel.Y=y;
            turn.CenterX = width / 2;
            turn.CenterY = -12;
            double paperChrome=framedOrigin?1:chrome, paperEase=framedOrigin||Falling?1:ease;
            paper.Color=Color.FromArgb((byte)(245 * paperChrome),255,255,255);
            border.Draw(paper,width,height,paperChrome,12*paperEase,6*paperEase,7*paperEase);
            shadow.Opacity = 0.2 * paperChrome;
            border.Effect=paperChrome>.005?shadow:null;
            clip.Opacity = chrome;
            clipPosition.X=width/2-8;clipPosition.Y=-20;
            if (progress < 1) return;
            Complete();
            if (Falling) { Close(); return; }
            Opacity = Math.Max(0, 1 - (seconds - duration) / fadeDuration);
            if (seconds >= duration+fadeDuration) Close();
        }

        void RenderCollection(double progress)
        {
            Rect mouth = Local(basketTarget());
            double ease = progress * progress * progress * (10 + progress * (-15 + 6 * progress));
            double smallWidth = Math.Min(22, Math.Max(10, origin.Width * 0.15));
            double smallHeight = smallWidth * origin.Height / origin.Width;
            double width = origin.Width + (smallWidth - origin.Width) * ease;
            double height = origin.Height + (smallHeight - origin.Height) * ease;
            double startX = origin.X + origin.Width / 2, endX = mouth.X + mouth.Width / 2;
            double x = startX + (endX - startX) * ease;
            double lift = Math.Min(38, Math.Max(10, origin.Y - 10));
            double y = origin.Y + origin.Height / 2 + (mouth.Y + smallHeight / 2 + 4 - origin.Y - origin.Height / 2) * ease
                - lift * Math.Sin(Math.PI * ease);
            double top = y - height / 2;
            travel.X=x-width/2;travel.Y=top;
            turn.CenterX = width / 2; turn.CenterY = height / 2;
            turn.Angle = card.Tilt * (1 - ease) + Math.Sign(endX - startX) * 10 * Math.Sin(Math.PI * ease);
            paper.Color=Colors.White;
            border.Draw(paper,width,height,1,Math.Min(9,width*.09),Math.Min(6,width*.04),Math.Min(7,width*.07));
            shadow.Opacity = 0.16 * (1 - ease); clip.Opacity = 0;
            Opacity = 1 - 0.8 * Math.Max(0, (progress - 0.82) / 0.18);
            frame.Clip = null;
            // 图片跨过篮口后逐渐被篮身遮住，而不是在篮子前方淡出。
            if (progress > 0.68 && Math.Abs(x - endX) < mouth.Width * 0.6)
                frame.Clip = new RectangleGeometry(new Rect(0, 0, width, Math.Max(0, Math.Min(height, mouth.Y - top))));
            if (progress < 1) return;
            arrived = true; Complete(); Close();
        }
    }

    // A changing photo rectangle is drawing data, not a new WPF layout each frame.
    internal sealed class FlightPaper : FrameworkElement
    {
        readonly DrawingVisual drawing = new DrawingVisual();
        readonly RectangleGeometry photoClip = new RectangleGeometry();
        readonly Pen edge = new Pen(new SolidColorBrush(Color.FromRgb(220,225,233)),1);
        internal ImageSource Picture;
        internal FlightPaper(){AddVisualChild(drawing);}
        protected override int VisualChildrenCount {get{return 1;}}
        protected override Visual GetVisualChild(int index){if(index!=0)throw new ArgumentOutOfRangeException("index");return drawing;}
        internal void Draw(Brush paper,double width,double height,double stroke,double radius,double margin,double photoRadius)
        {
            edge.Thickness=stroke;
            var inner=new Rect(margin+stroke,margin+stroke,Math.Max(1,width-2*(margin+stroke)),Math.Max(1,height-2*(margin+stroke)));
            photoClip.Rect=inner;photoClip.RadiusX=photoClip.RadiusY=photoRadius;
            using(var context=drawing.RenderOpen()){
                // Border paints its stroke inside its bounds; match that footprint.
                context.DrawRoundedRectangle(paper,stroke>0?edge:null,new Rect(stroke/2,stroke/2,Math.Max(0,width-stroke),Math.Max(0,height-stroke)),Math.Max(0,radius-stroke/2),Math.Max(0,radius-stroke/2));
                context.PushClip(photoClip);context.DrawImage(Picture,inner);context.Pop();
            }
        }
    }
}
