using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("拾影花笺 · AgentBloom")]
[assembly: AssemblyProduct("拾影花笺 · AgentBloom")]
[assembly: AssemblyDescription("拾影花笺：把截图挂在屏幕上沿，随手复制、拖拽和保存。")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace ScreenLine
{
    internal sealed class DesktopController : IDisposable
    {
        public readonly Gallery Gallery;
        public readonly CapturePipeline Captures;
        public readonly TextCollection Texts;
        public readonly ShelfWindow Shelf;
        internal event Action CollectionRequested;
        internal event Action AppearanceChanged;
        internal event Action ShortcutRequested;
        readonly WindowsEvents events;
        readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
        LightNotice notice;
        readonly CheckBox collect;
        readonly CheckBox collectText;
        readonly CheckBox autoHang;
        internal readonly TrayPanel TrayMenu;
        internal AppearanceWindow Appearance { get; private set; }
        internal BasketWindow Basket { get; private set; }
        internal TextWindow TextWindow { get; private set; }
        internal readonly List<ImageEditorWindow> Editors = new List<ImageEditorWindow>();
        readonly AppUpdates updates = new AppUpdates();
        readonly CancellationTokenSource updateLifetime = new CancellationTokenSource();
        readonly DispatcherTimer updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        readonly MenuItem updateMenu;
        readonly bool automaticUpdates;
        UpdateRelease availableUpdate;
        UpdateWindow updateWindow;
        bool checkingUpdates, disposed;
        uint textClipboardSequence;
        int textClipboardVersion;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public DesktopController(StateStore store, string screenshots, bool automaticUpdates = false)
        {
            this.automaticUpdates = automaticUpdates;
            InlineAnnotation.WarnBeforeDiscard=store.State.WarnUnsavedEdits;
            Gallery = new Gallery(store);
            Texts = new TextCollection(store);
            Captures = new CapturePipeline(Gallery, Application.Current.Dispatcher, screenshots);
            Shelf = new ShelfWindow(Gallery, Act, EnableClipboard);
            // AgentSession connects the project board.
            Shelf.TextRequested += ShowTexts;
            Texts.Changed += Shelf.UpdateTexts;
            events = new WindowsEvents();
            events.ToggleRequested += Shelf.Toggle;
            events.ExitRequested += Exit;
            events.ClipboardChanged += Captures.ClipboardChanged;
            textClipboardSequence = WindowsNative.GetClipboardSequenceNumber();
            events.ClipboardChanged += CollectClipboardText;
            Captures.Arrived += Shelf.ShowNewCapture;
            Captures.Failed += Notice;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PicRope.Icon"))
                tray.Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
            tray.Text = "拾影花笺 · AgentBloom";
            TrayMenu = new TrayPanel();
            TrayMenu.OnAction(TrayMenu.VisibilityButton, delegate { Shelf.Toggle(); });
            if (events.ShortcutAvailable) TrayMenu.Shortcut.Text = "Ctrl+Alt+T";
            else if (!AgentApplication.QaPassive) Notice("Ctrl+Alt+T 已被占用，可点击托盘图标打开。");
            TrayMenu.OnAction(TrayMenu.BasketButton, ShowBasket);
            TrayMenu.OnAction(TrayMenu.TextButton, ShowTexts);
            autoHang = TrayMenu.AutoHang;
            TrayMenu.OnToggle(autoHang, Captures.AutoHang, delegate(bool value) { if (Captures.AutoHang != value) Captures.AutoHang = value; });
            Gallery.Changed += SyncAutoHang;
            collect = TrayMenu.Pictures;
            TrayMenu.OnToggle(collect, Captures.CollectClipboard, delegate(bool value) { Captures.CollectClipboard = value; Shelf.Update(); });
            collectText = TrayMenu.Text;
            TrayMenu.OnToggle(collectText, Texts.CollectClipboard, delegate(bool value)
            {
                textClipboardVersion++;
                textClipboardSequence = WindowsNative.GetClipboardSequenceNumber();
                Texts.CollectClipboard = value;
            });
            TrayMenu.OnAction(TrayMenu.AppearanceButton, ShowAppearance);
            var more = TrayMenu.MoreMenu;
            more.AddAction("添加图片…", "add", async delegate
            {
                var picker = new OpenFileDialog { Title = "添加图片", Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff", Multiselect = true };
                if (picker.ShowDialog() != true) return;
                foreach (string file in picker.FileNames)
                    if (!await Captures.Import(file, null, true)) Notice("无法读取图片：" + Path.GetFileName(file));
            });
            more.AddAction("打开收集文件夹", "folder", delegate
            {
                try { WindowsShell.OpenInbox(store); }
                catch (Exception error) { store.Log(error); Notice("无法打开收集文件夹：" + error.Message); }
            });
            more.AddAction("全部取下（保留文件）", "clear", delegate { Gallery.Clear(); });
            more.Separator();
            MenuItem startup = null;
            startup = more.AddAction("开机启动", "startup", delegate
            {
                AgentStartup.SetEnabled(startup.IsChecked);
            });
            startup.IsCheckable = true;
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) startup.IsChecked = key != null && key.GetValue("AgentBloom") != null;
            MenuItem editReminder=null;editReminder=more.AddAction("退出标注时提醒保存", "edit",delegate{InlineAnnotation.WarnBeforeDiscard=editReminder.IsChecked;store.State.WarnUnsavedEdits=editReminder.IsChecked;store.Save();});editReminder.IsCheckable=true;editReminder.IsChecked=store.State.WarnUnsavedEdits;
            more.AddAction("截图快捷键…","appearance",delegate{if(ShortcutRequested!=null)ShortcutRequested();});
            updateMenu = more.AddAction("检查更新…", "save", async delegate { await CheckForUpdates(true); });
            updateMenu.IsEnabled = false; updateMenu.Header = "AgentBloom 本地开发版";
            updateTimer.Tick += async delegate { updateTimer.Interval = TimeSpan.FromHours(12); await CheckForUpdates(false); };
            tray.BalloonTipClicked += delegate { ShowUpdate(); };
            TrayMenu.OnAction(TrayMenu.ExitButton, Exit);
            tray.MouseUp += delegate(object sender, Forms.MouseEventArgs args) { if (args.Button == Forms.MouseButtons.Right) ShowTrayMenu(); };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs args) { if (args.Button == Forms.MouseButtons.Left) Shelf.Toggle(); };
            tray.Visible = true;
        }

        async void Exit()
        {
            if(!await Shelf.ConfirmInlineEditors())return;
            foreach (var editor in Editors.ToArray()) if (!await editor.ConfirmClose()) return;
            Application.Current.Shutdown();
        }

        public async Task Start()
        {
            Shelf.Show();
            await Captures.Start();
            if (automaticUpdates) updateTimer.Start();
        }

        async Task CheckForUpdates(bool manual)
        {
            if (disposed || checkingUpdates) return;
            if (manual && availableUpdate != null) { ShowUpdate(); return; }
            checkingUpdates = true; updateMenu.IsEnabled = false; updateMenu.Header = "正在检查更新…";
            try
            {
                availableUpdate = await updates.CheckAsync(updateLifetime.Token);
                if (disposed) return;
                if (availableUpdate == null) { if (manual) Notice("当前已是最新版 " + AppUpdates.CurrentVersion + "。"); }
                else if (manual) ShowUpdate();
                else if (Gallery.Store.State.LastUpdateNotification != availableUpdate.Version)
                {
                    tray.ShowBalloonTip(10000, "拾影花笺有新版本 " + availableUpdate.Version, "点击查看更新并下载安装。", Forms.ToolTipIcon.Info);
                    Gallery.Store.State.LastUpdateNotification = availableUpdate.Version; Gallery.Store.Save();
                }
            }
            catch (Exception error)
            {
                if (!disposed && manual) Notice("暂时无法检查更新，请检查网络后重试。" + (error is System.IO.InvalidDataException ? "\n" + error.Message : ""));
            }
            finally
            {
                checkingUpdates = false; updateMenu.IsEnabled = true;
                updateMenu.Header = availableUpdate == null ? "检查更新…" : "更新到 " + availableUpdate.Version + "…";
            }
        }

        void ShowUpdate()
        {
            if (disposed || availableUpdate == null) return;
            if (updateWindow != null) { updateWindow.Activate(); return; }
            updateWindow = new UpdateWindow(availableUpdate, updates, async delegate(string work)
            {
                foreach (var editor in Editors.ToArray()) if (!await editor.ConfirmClose()) return false;
                AppUpdates.Install(work);
                Application.Current.Shutdown();
                return true;
            });
            updateWindow.Closed += delegate { updateWindow = null; };
            updateWindow.Show();
        }

        void EnableClipboard()
        {
            Captures.CollectClipboard = true;
            collect.IsChecked = true;
        }

        void SyncAutoHang() { autoHang.IsChecked = Captures.AutoHang; }

        async void CollectClipboardText()
        {
            uint sequence = WindowsNative.GetClipboardSequenceNumber();
            if (disposed || !Texts.CollectClipboard || sequence == textClipboardSequence) return;
            textClipboardSequence = sequence;
            int version = textClipboardVersion;
            try
            {
                string content = await WindowsClipboard.ReadText();
                if (disposed || !Texts.CollectClipboard || version != textClipboardVersion || sequence != WindowsNative.GetClipboardSequenceNumber()) return;
                Texts.Add(content);
            }
            catch (Exception error) { if (!disposed) Gallery.Store.Log(error); }
        }

        internal void ShowBasket()
        {
            if(CollectionRequested!=null){CollectionRequested();return;}
            if (Basket != null) { Basket.Activate(); return; }
            Gallery.Prune();
            Basket = new BasketWindow(Gallery, delegate(bool value) { Captures.AutoHang = value; }, Captures.Hang, Act);
            Basket.Owner = Shelf;
            Shelf.ChoosingBasket = true;
            Shelf.Reveal(true, false, 0);
            Basket.Closed += delegate { Basket = null; Shelf.ChoosingBasket = false; };
            Basket.Show();
        }

        internal void ShowTexts()
        {
            if (TextWindow != null) { TextWindow.Close(); return; }
            Shelf.Reveal(true, false, 0);
            Shelf.ChoosingTexts = true;
            TextWindow = new TextWindow(Texts, Shelf);
            TextWindow.Closed += delegate { TextWindow = null; Shelf.ChoosingTexts = false; };
            TextWindow.Show();
        }

        internal void ShowAppearance()
        {
            if (Appearance != null) { Appearance.Activate(); return; }
            Appearance = new AppearanceWindow(Shelf.Appearance, Gallery, delegate(LineTheme theme)
            {
                Gallery.Store.State.Theme = theme.Id;
                Gallery.Store.Save();
                Shelf.ApplyTheme(theme);
                if(AppearanceChanged!=null)AppearanceChanged();
                Shelf.Reveal(true, false, 3);
            });
            Appearance.Owner = Shelf;
            Shelf.ChoosingAppearance = true;
            Shelf.Reveal(true, false, 0);
            Appearance.Closed += delegate { Appearance = null; Shelf.ChoosingAppearance = false; };
            Appearance.Show();
        }

        internal void ShowTrayMenu()
        {
            WindowsNative.SetForegroundWindow(Shelf.Handle);
            var cursor = WindowsNative.Cursor();
            var point = Shelf.PointFromScreen(new Point(cursor.X, cursor.Y));
            TrayMenu.PlacementTarget = Shelf;
            TrayMenu.Placement = PlacementMode.RelativePoint;
            TrayMenu.HorizontalOffset = point.X - 16;
            TrayMenu.VerticalOffset = point.Y - 16;
            TrayMenu.IsOpen = true;
        }

        void Notice(string message)
        {
            if (notice != null) notice.Close();
            notice = new LightNotice(message);
            var current = notice;
            current.Closed += delegate { if (notice == current) notice = null; };
            notice.Show();
        }

        static string Quote(string path) { return "\"" + path + "\""; }

        public async void Act(ShotAction action, CaptureItem item)
        {
            try
            {
                if (!File.Exists(item.Path)) { Gallery.Prune(); return; }
                switch (action)
                {
                    case ShotAction.Copy:
                        await CopyImage(item);
                        break;
                    case ShotAction.Open:
                        Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
                        Shelf.FollowImageViewer(item.Path);
                        break;
                    case ShotAction.Edit: Shelf.EditInline(item); break;
                    case ShotAction.Save:
                        var dialog = new SaveFileDialog { Title = "保存副本", FileName = Path.GetFileName(item.Path), Filter = "图片|*" + Path.GetExtension(item.Path), OverwritePrompt = true };
                        if (dialog.ShowDialog() == true && !FileRules.SameFile(item.Path, dialog.FileName)) File.Copy(item.Path, dialog.FileName, true);
                        break;
                    case ShotAction.Locate:
                        Process.Start("explorer.exe", "/select," + Quote(item.Path));
                        break;
                    case ShotAction.Remove:
                        Shelf.ShowCollect(item);
                        Gallery.Remove(item);
                        break;
                    case ShotAction.Trash:
                        FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        Shelf.ShowDiscard(item);
                        Gallery.Remove(item);
                        Gallery.Prune();
                        break;
                }
            }
            catch (Exception error) { Gallery.Store.Log(error); Notice("操作未完成：" + error.Message); }
        }

        internal async Task CopyImage(CaptureItem item){var content=await Task.Run(delegate{using(var stream=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var image=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];image.Freeze();return Tuple.Create<System.Windows.Media.Imaging.BitmapSource,byte[]>(image,ImageContent.EncodeClipboard(image));}});var payload=new DataObject();payload.SetImage(content.Item1);payload.SetData("PNG",new MemoryStream(content.Item2));payload.SetFileDropList(new StringCollection{item.Path});await WindowsClipboard.Write(payload);Shelf.ShowCopied(item.Path);}

        public void Dispose()
        {
            disposed = true; updateTimer.Stop(); updateLifetime.Cancel();
            if (updateWindow != null) updateWindow.Close();
            updates.Dispose();
            if (Basket != null) Basket.Close();
            if (TextWindow != null) TextWindow.Close();
            if (Appearance != null) Appearance.Close();
            if (notice != null) notice.Close();
            foreach (var editor in Editors.ToArray()) editor.CloseWithoutPrompt();
            TrayMenu.Close();
            Gallery.Changed -= SyncAutoHang;
            Texts.Changed -= Shelf.UpdateTexts;
            Captures.Dispose();
            events.Dispose();
            tray.Visible = false;
            var icon = tray.Icon;
            tray.Dispose();
            icon.Dispose();
            Shelf.Close();
        }

        async Task RefreshEditedImage(string path)
        {
            ImageContent image = await Task.Run(() => ImageContent.Read(path));
            var item = Gallery.Items.Find(candidate => FileRules.SameFile(candidate.Path, path));
            if (item == null) Gallery.Add(path, image);
            else
            {
                item.Image = image;
                item.Written = File.GetLastWriteTimeUtc(path);
                Gallery.Notify();
            }
        }
    }

    internal static class Program { [STAThread] public static int Main(string[] args) { int code = WindowsNative.EnsureDefaultDesktop(); if (code >= 0) return code; AgentApplication.Run(args); return 0; } }
}
