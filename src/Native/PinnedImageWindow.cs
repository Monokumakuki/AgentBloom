using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ScreenLine
{
    internal sealed class PinnedImageWindow : Window
    {
        public readonly CaptureItem Item;
        internal readonly Border Frame = new Border();
        internal readonly Button PinButton = new Button();
        readonly Thumbtack pin = new Thumbtack();
        readonly TranslateTransform pinLift = new TranslateTransform(0, -6);
        readonly TranslateTransform paperLift = new TranslateTransform();
        readonly Image photo = new Image();
        readonly RotateTransform turn = new RotateTransform();
        readonly ScaleTransform zoom = new ScaleTransform();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly double startWidth, startHeight, initialTilt;
        BitmapSource bitmap;
        IntPtr handle;
        readonly Point fallStart, landing;
        readonly Forms.Screen display;
        readonly double displayScale;
        Point anchor, moveStart, anchorStart;
        double pictureScale, dropStarted = -1;
        int placedLeft = int.MinValue, placedTop = int.MinValue, placedWidth, placedHeight;
        bool moving, rendering, returning, selected, checkingClose;
        TimeSpan lastRendering=TimeSpan.MinValue;int presentedFrames;
        internal readonly TaskCompletionSource<bool> PresentedFrame=new TaskCompletionSource<bool>();
        internal event Func<string,Task> ImageSaved;
        internal event Func<Task> ReturnRequested;
        readonly Grid layer = new Grid();
        InlineAnnotation inline;
        readonly Grid resizeHandles = new Grid();
        public Rect ReturnBounds { get; private set; }
        public BitmapSource Picture { get { return bitmap; } }
        public event Action<bool> NearShelf;
        const double Headroom = 22;

        public PinnedImageWindow(CaptureItem item, Rect origin, double tilt, BitmapSource picture, LineTheme theme, int colorIndex)
        {
            Item = item;
            initialTilt = tilt;
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)(origin.X + origin.Width / 2), (int)origin.Y));
            double dpi = WindowsNative.MonitorScale(screen);
            display = screen; displayScale = dpi;
            startWidth = Math.Max(1, origin.Width / dpi - 14);
            startHeight = Math.Max(1, origin.Height / dpi - 14);
            anchor = fallStart = new Point(origin.X + origin.Width / 2, origin.Y - 7 * dpi);
            Title = "桌面贴图 · " + Path.GetFileName(item.Path);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            IsHitTestVisible = false;
            Frame.Margin = new Thickness(12, 24+Headroom, 12, 12);
            Frame.Padding = new Thickness(6);
            Frame.Background = Brushes.White;
            Frame.BorderBrush = LightTheme.Brush("#DCE1E9");
            Frame.BorderThickness = new Thickness(1);
            Frame.CornerRadius = new CornerRadius(12);
            Frame.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.2 };
            Frame.CacheMode = new BitmapCache { RenderAtScale = dpi };
            Frame.RenderTransformOrigin = new Point(0.5, 0);
            var transform = new TransformGroup();
            transform.Children.Add(zoom); transform.Children.Add(turn); transform.Children.Add(paperLift);
            Frame.RenderTransform = transform;
            Frame.Cursor = Cursors.SizeAll;
            photo.Stretch = Stretch.Uniform;
            photo.SizeChanged += delegate { photo.Clip = new RectangleGeometry(new Rect(0, 0, photo.ActualWidth, photo.ActualHeight), 7, 7); };
            layer.Children.Add(photo);
            AddResizeCorner(false, false); AddResizeCorner(true, false);
            AddResizeCorner(false, true); AddResizeCorner(true, true);
            layer.Children.Add(resizeHandles);
            Frame.Child = layer;
            PinButton.Content = pin;
            PinButton.Width = PinButton.Height = 32;
            PinButton.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='Transparent'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
            PinButton.HorizontalAlignment = HorizontalAlignment.Center;
            PinButton.VerticalAlignment = VerticalAlignment.Top;
            PinButton.Focusable = false;
            PinButton.Cursor = Cursors.Hand;
            PinButton.Opacity = 0;
            PinButton.RenderTransform = pinLift;
            PinButton.Margin = new Thickness(0,Headroom,0,0);
            PinButton.ToolTip = new ToolTip { Content = "挂回晾衣绳", Style = LightTheme.HintStyle,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right, HorizontalOffset = 4 };
            ToolTipService.SetInitialShowDelay(PinButton, 650);
            System.Windows.Automation.AutomationProperties.SetName(PinButton, "挂回晾衣绳");
            PinButton.Click += delegate { ReturnToRope(); };
            var surface = new Grid();
            surface.Children.Add(Frame); surface.Children.Add(PinButton);
            Content = surface;
            ApplyTheme(theme, colorIndex);
            Frame.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (inline != null) return;
                selected=true;WindowsNative.AllowActivation(handle);WindowsNative.ActivateEditor(handle);Frame.Focusable=true;Frame.Focus();
                e.Handled = true;
                moving = true;
                var cursor = WindowsNative.Cursor();
                moveStart = new Point(cursor.X, cursor.Y);
                anchorStart = anchor;
                LiftPin(true);
                Frame.CaptureMouse();
            };
            Frame.MouseMove += delegate
            {
                if (!moving) return;
                var cursor = WindowsNative.Cursor();
                anchor = anchorStart + (new Point(cursor.X, cursor.Y) - moveStart);
                Position();
                if(NearShelf!=null)NearShelf(cursor.Y<=Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y)).Bounds.Top+150*displayScale);
            };
            Frame.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!moving) return;
                e.Handled = true; moving = false; LiftPin(false); Frame.ReleaseMouseCapture();
                var cursor=WindowsNative.Cursor();var screenAt=Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y));
                if(cursor.Y<=screenAt.Bounds.Top+150*WindowsNative.MonitorScale(screenAt))ReturnToRope();
            };
            Frame.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                StartInlineAnnotation();
            };
            Frame.LostMouseCapture += delegate { if (moving) { moving = false; LiftPin(false); } };
            PreviewMouseWheel += ResizePicture;
            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                WindowsNative.PrepareOverlay(handle);
                HwndSource.FromHwnd(handle).AddHook(delegate(IntPtr window, int message, IntPtr w, IntPtr l, ref bool handled)
                {
                    if (message == 0x21&&!selected&&inline==null) { handled = true; return new IntPtr(3); }
                    return IntPtr.Zero;
                });
                Position();
            };
            Loaded += delegate { dropStarted = clock.Elapsed.TotalSeconds; StartRendering(); };
            Closing += async delegate(object sender,System.ComponentModel.CancelEventArgs e){ReturnBounds=PhotoRect();if(inline==null||!inline.Dirty)return;e.Cancel=true;if(checkingClose)return;checkingClose=true;bool allowed=await inline.ConfirmEnd();checkingClose=false;if(allowed)Close();};
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&inline==null){e.Handled=true;ReturnToRope();}};
            Closed += delegate { CompositionTarget.Rendering -= Render;PresentedFrame.TrySetResult(false); };
            bitmap = picture;
            // Keep the moving surface small; reveal full-resolution pixels after it settles.
            photo.Source = item.Image.Thumbnail;
            RenderOptions.SetBitmapScalingMode(Frame,BitmapScalingMode.LowQuality);
            pictureScale = DefaultScale();
            double landingWidth = (bitmap.PixelWidth * pictureScale + 38) * dpi;
            double landingHeight = (bitmap.PixelHeight * pictureScale + 50+Headroom) * dpi;
            landing = new Point(
                Math.Max(screen.WorkingArea.Left + landingWidth / 2, Math.Min(screen.WorkingArea.Right - landingWidth / 2, fallStart.X)),
                Math.Max(screen.WorkingArea.Top + (17+Headroom) * dpi, Math.Min(screen.WorkingArea.Bottom - landingHeight + (17+Headroom) * dpi, fallStart.Y + Math.Max(180 * dpi, screen.WorkingArea.Height * 0.25))));
            Width = bitmap.PixelWidth * pictureScale + 38;
            Height = bitmap.PixelHeight * pictureScale + 50+Headroom;
            zoom.ScaleX = (startWidth + 14) / (Width - 24);
            zoom.ScaleY = (startHeight + 14) / (Height - 36-Headroom);
            turn.Angle = initialTilt;
            ReturnBounds = origin;
        }

        internal void StartInlineAnnotation()
        {
            if (inline != null || !IsHitTestVisible) return;
            if(InPlaceEditorWindow.FocusExisting(Item.Path))return;
            selected=true;WindowsNative.AllowActivation(handle);WindowsNative.ActivateEditor(handle);
            inline = new InlineAnnotation(this, Item.Path, bitmap, async annotated => {
                await InlineAnnotation.SaveImage(Item.Path,annotated);
                UpdateImage();
                if(ImageSaved!=null)await ImageSaved(Item.Path);
            }, done => {
                if (layer.Children.Contains(inline)) layer.Children.Remove(inline);
                inline = null;
                resizeHandles.Visibility=Visibility.Visible;
                Frame.CacheMode=new BitmapCache{RenderAtScale=displayScale};
                if (done) UpdateImage();
                else ReturnToRope();
            }, () => PhotoRect(),delta=>{anchor+=delta;Position();});
            resizeHandles.Visibility=Visibility.Collapsed;Frame.CacheMode=null;layer.Children.Add(inline);
        }

        internal Task<bool> ConfirmInlineClose(){return inline==null?Task.FromResult(true):inline.ConfirmEnd();}

        public void ApplyTheme(LineTheme theme, int colorIndex)
        {
            pin.Apply(theme, colorIndex);
        }

        void LiftPin(bool lifted)
        {
            pinLift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(lifted ? -4 : 0, TimeSpan.FromSeconds(0.14)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            paperLift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(lifted ? -1 : 0, TimeSpan.FromSeconds(0.14)));
            ((DropShadowEffect)Frame.Effect).BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(lifted ? 6 : 4, TimeSpan.FromSeconds(0.14)));
        }

        internal async void ReturnToRope()
        {
            if (returning) return;
            if(inline!=null&&!await inline.ConfirmEnd())return;
            returning = true;
            IsHitTestVisible=false;
            resizeHandles.Visibility=Visibility.Collapsed;
            PinButton.IsEnabled = false;
            ToolTipService.SetIsEnabled(PinButton, false);
            ((ToolTip)PinButton.ToolTip).IsOpen = false;
            pinLift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, TimeSpan.FromSeconds(0.12)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            var faded=new TaskCompletionSource<bool>();
            var fade=new DoubleAnimation(0,TimeSpan.FromSeconds(.12));fade.Completed+=delegate{faded.TrySetResult(true);};
            PinButton.BeginAnimation(OpacityProperty,fade);
            await faded.Task;
            try { if(ReturnRequested!=null)await ReturnRequested();else Close(); }
            catch { Close(); }
        }

        public void UpdateImage()
        {
            bitmap = ReadPicture(Item.Path);
            photo.Source = bitmap;
            if (handle != IntPtr.Zero)
            {
                var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.X, (int)anchor.Y));
                double dpi = WindowsNative.MonitorScale(screen);
                pictureScale = Math.Min(pictureScale, Math.Min((screen.WorkingArea.Width / dpi - 40) / bitmap.PixelWidth,
                    (screen.WorkingArea.Height / dpi - 76) / bitmap.PixelHeight));
                StartRendering();
            }
        }

        public static BitmapSource ReadPicture(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var bitmap = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                bitmap.Freeze();
                return bitmap;
            }
        }

        void StartRendering()
        {
            if (rendering) return;
            rendering = true;
            CompositionTarget.Rendering += Render;
            Render(null, EventArgs.Empty);
        }

        void Render(object sender, EventArgs args)
        {
            if(args is RenderingEventArgs){var time=((RenderingEventArgs)args).RenderingTime;if(time==lastRendering)return;lastRendering=time;if(++presentedFrames>=2)PresentedFrame.TrySetResult(true);}
            double now = clock.Elapsed.TotalSeconds;
            double elapsed = Math.Max(0, now - dropStarted);
            double t = Math.Min(1, elapsed / 0.40);
            double progress = t * t * t * (10 + t * (-15 + 6 * t));
            Width = bitmap.PixelWidth * pictureScale + 38;
            Height = bitmap.PixelHeight * pictureScale + 50+Headroom;
            zoom.ScaleX = (startWidth + 14) / (Width - 24) * (1 - progress) + progress;
            zoom.ScaleY = (startHeight + 14) / (Height - 36-Headroom) * (1 - progress) + progress;
            turn.Angle = initialTilt * (1 - progress);
            if (!IsHitTestVisible)
            {
                double settle = Math.Max(0, Math.Min(1, (elapsed - 0.40) / 0.14));
                double bounce = Math.Sin(Math.PI * settle);
                anchor = fallStart + (landing - fallStart) * progress - new Vector(0, 2 * displayScale * bounce * bounce);
                PinButton.Opacity = settle;
                pinLift.Y = -6 * (1 - settle);
                if (elapsed >= 0.54) IsHitTestVisible = true;
            }
            Position();
            if (IsHitTestVisible)
            {
                photo.Source=bitmap;
                RenderOptions.SetBitmapScalingMode(Frame,BitmapScalingMode.HighQuality);
                rendering = false;
                CompositionTarget.Rendering -= Render;
            }
        }

        void Position()
        {
            var screen = IsHitTestVisible||returning ? Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.X, (int)anchor.Y)) : display;
            double dpi = IsHitTestVisible||returning ? WindowsNative.MonitorScale(screen) : displayScale;
            int width = (int)Math.Round(Width * dpi), height = (int)Math.Round(Height * dpi);
            int left = (int)Math.Round(anchor.X - width / 2.0), top = (int)Math.Round(anchor.Y - (17+Headroom) * dpi);
            if (IsHitTestVisible)
            {
                int snapThreshold = (int)Math.Round(18 * dpi);
                int visibleMargin = (int)Math.Round(48 * dpi);
                if (Math.Abs(left - screen.WorkingArea.Left) <= snapThreshold) left = screen.WorkingArea.Left;
                else if (Math.Abs((left + width) - screen.WorkingArea.Right) <= snapThreshold) left = screen.WorkingArea.Right - width;
                if (Math.Abs(top - screen.WorkingArea.Top) <= snapThreshold) top = screen.WorkingArea.Top;
                else if (Math.Abs((top + height) - screen.WorkingArea.Bottom) <= snapThreshold) top = screen.WorkingArea.Bottom - height;

                int minLeft = screen.WorkingArea.Left - width + visibleMargin;
                int maxLeft = screen.WorkingArea.Right - visibleMargin;
                int minTop = screen.Bounds.Top;
                int maxTop = screen.WorkingArea.Bottom - visibleMargin;
                left = Math.Max(minLeft, Math.Min(maxLeft, left));
                top = Math.Max(minTop, Math.Min(maxTop, top));
            }
            if (left == placedLeft && top == placedTop && width == placedWidth && height == placedHeight) return;
            bool resize = width != placedWidth || height != placedHeight;
            placedLeft = left; placedTop = top; placedWidth = width; placedHeight = height;
            WindowsNative.SetWindowPos(handle, IntPtr.Zero, left, top, width, height, resize ? 0x14u : 0x15u);
        }

        void ResizePicture(object sender, MouseWheelEventArgs e)
        {
            if (inline != null) return;
            e.Handled = true;
            ChangeZoom(pictureScale*Math.Pow(1.12,e.Delta/120.0));
        }
        double DefaultScale(){return Math.Min(2,Math.Min(Math.Min(680,display.WorkingArea.Width/displayScale*.75)/bitmap.PixelWidth,Math.Min(480,display.WorkingArea.Height/displayScale*.70)/bitmap.PixelHeight));}
        void ChangeZoom(double value){if(inline!=null||returning||bitmap==null)return;var screen=WindowsNative.ScreenAtPointer();double dpi=WindowsNative.MonitorScale(screen);double maximum=Math.Max(.02,Math.Min((screen.WorkingArea.Width/dpi-40)/bitmap.PixelWidth,(screen.WorkingArea.Height/dpi-76)/bitmap.PixelHeight));pictureScale=Math.Max(Math.Min(.08,maximum),Math.Min(maximum,value));StartRendering();}

        void AddResizeCorner(bool right, bool bottom)
        {
            var corner = new Thumb { Width=20, Height=20, HorizontalAlignment=right?HorizontalAlignment.Right:HorizontalAlignment.Left,
                VerticalAlignment=bottom?VerticalAlignment.Bottom:VerticalAlignment.Top, Cursor=right==bottom?Cursors.SizeNWSE:Cursors.SizeNESW,
                Focusable=false, ToolTip="拖动边角缩放" };
            corner.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='Transparent'/></ControlTemplate>");
            System.Windows.Automation.AutomationProperties.SetAutomationId(corner,"pin-resize-"+(bottom?"bottom":"top")+"-"+(right?"right":"left"));
            Point start=new Point(), originalAnchor=new Point(); double originalScale=1, dpi=1;
            corner.DragStarted+=delegate {var cursor=WindowsNative.Cursor();start=new Point(cursor.X,cursor.Y);originalAnchor=anchor;originalScale=pictureScale;dpi=WindowsNative.MonitorScale(WindowsNative.ScreenAtPointer());};
            corner.DragDelta+=delegate {
                if(inline!=null||returning)return;
                var cursor=WindowsNative.Cursor();double dx=(cursor.X-start.X)/dpi,dy=(cursor.Y-start.Y)/dpi;
                double w=bitmap.PixelWidth,h=bitmap.PixelHeight;
                ChangeZoom(originalScale+(dx*(right?1:-1)*w+dy*(bottom?1:-1)*h)/(w*w+h*h));
                double delta=pictureScale-originalScale;
                anchor=new Point(originalAnchor.X+(right?1:-1)*w*delta*dpi/2,originalAnchor.Y+(bottom?0:-h*delta*dpi));
                Position();
            };
            resizeHandles.Children.Add(corner);
        }

        public Rect PhotoRect()
        {
            return new Rect(Frame.PointToScreen(new Point()), Frame.PointToScreen(new Point(Frame.ActualWidth, Frame.ActualHeight)));
        }
    }
}
