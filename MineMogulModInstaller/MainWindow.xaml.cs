using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using WpfColor = System.Windows.Media.Color;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfMessageBox = System.Windows.MessageBox;
using WpfHAlign = System.Windows.HorizontalAlignment;
using WpfVAlign = System.Windows.VerticalAlignment;

namespace MineMogulModInstaller
{
    public partial class MainWindow : Window
    {
        // ── Feature list ─────────────────────────────────────────────────────
        private static readonly (string Icon, string Title, string Desc, string Tag, string TagColor)[] Features =
        {
            ("📊", "Factory HUD",        "Live overlay: Machines, Belts, Sales & Settings tabs.\nToggle with F5.",               "F5",  "#33b5e5"),
            ("🔧", "Splitter Inspector", "Kijk naar een splitter → druk F om te configureren.\nTab voor alle splitters.",         "F",   "#29d96c"),
            ("⚠️",  "Bottleneck Detector","Flags the slowest belt in your factory automatically,\nhighlighted in red.",            "HUD", "#f5a623"),
            ("🏷️", "Belt Inspector",     "Kijk naar een belt → detail panel rechtsonder.\nF6 = zwevende labels op alle belts.",  "F6",  "#33b5e5"),
            ("💰", "Sales Tracker",      "Session revenue, top-selling resources and\nlive earnings rate per minute.",            "HUD", "#33b5e5"),
            ("🖥️", "Terminal Integration","Open Factory HUD from any Computer Terminal\nvia Factory Overview.",                   "NEW", "#29d96c"),
        };

        // ── Progress ─────────────────────────────────────────────────────────
        private double _progressTarget = 0;
        private DispatcherTimer? _progressTimer;
        private double _progressBarWidth = 0;

        // ── Status enum ───────────────────────────────────────────────────────
        private enum Status { None, NotInstalled, BepInExOnly, Installed }
        private Status _status = Status.None;

        // ─────────────────────────────────────────────────────────────────────
        public MainWindow() => InitializeComponent();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BuildFeatureCards();
            StartProgressTimer();
            TryAutoDetect();
        }

        // ── Feature cards ─────────────────────────────────────────────────────
        private void BuildFeatureCards()
        {
            foreach (var (icon, title, desc, tag, tagColor) in Features)
            {
                var card = new Border
                {
                    Background      = new SolidColorBrush(WpfColor.FromRgb(0x10, 0x15, 0x23)),
                    BorderBrush     = new SolidColorBrush(WpfColor.FromRgb(0x1e, 0x25, 0x40)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(12, 10, 12, 10),
                    Margin          = new Thickness(4),
                };

                var tagColor2 = (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(tagColor);
                var stripe    = new Border
                {
                    Width           = 3,
                    VerticalAlignment = WpfVAlign.Stretch,
                    Margin          = new Thickness(-12, -10, 8, -10),
                    Background      = new SolidColorBrush(WpfColor.FromArgb(180, tagColor2.R, tagColor2.G, tagColor2.B)),
                    CornerRadius    = new CornerRadius(8, 0, 0, 8),
                };

                var tagBorder = new Border
                {
                    Background      = new SolidColorBrush(WpfColor.FromArgb(50, tagColor2.R, tagColor2.G, tagColor2.B)),
                    BorderBrush     = new SolidColorBrush(WpfColor.FromArgb(100, tagColor2.R, tagColor2.G, tagColor2.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(5, 1, 5, 1),
                    VerticalAlignment = WpfVAlign.Center,
                    Margin          = new Thickness(6, 0, 0, 0),
                    Child           = new TextBlock
                    {
                        Text       = tag,
                        Foreground = new SolidColorBrush(tagColor2),
                        FontSize   = 8,
                        FontWeight = FontWeights.Bold,
                    }
                };

                var titleRow = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                titleRow.Children.Add(new TextBlock
                {
                    Text       = title,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(0xe8, 0xed, 0xf8)),
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = WpfVAlign.Center,
                });
                titleRow.Children.Add(tagBorder);

                var content = new StackPanel { Orientation = WpfOrientation.Horizontal };
                content.Children.Add(stripe);

                var textStack = new StackPanel { Margin = new Thickness(0) };
                textStack.Children.Add(new TextBlock
                {
                    Text       = icon + "  " + title.PadLeft(title.Length + 1),
                    FontSize   = 22,
                    Margin     = new Thickness(0, 0, 0, 2),
                    Visibility = Visibility.Collapsed,
                });
                textStack.Children.Add(titleRow);
                textStack.Children.Add(new TextBlock
                {
                    Text            = desc,
                    Foreground      = new SolidColorBrush(WpfColor.FromRgb(0x72, 0x82, 0xa4)),
                    FontSize        = 10,
                    TextWrapping    = TextWrapping.Wrap,
                    LineHeight      = 16,
                });

                // Icon circle
                var iconBorder = new Border
                {
                    Width           = 36,
                    Height          = 36,
                    CornerRadius    = new CornerRadius(8),
                    Background      = new SolidColorBrush(WpfColor.FromArgb(25, tagColor2.R, tagColor2.G, tagColor2.B)),
                    BorderBrush     = new SolidColorBrush(WpfColor.FromArgb(50, tagColor2.R, tagColor2.G, tagColor2.B)),
                    BorderThickness = new Thickness(1),
                    Margin          = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = WpfVAlign.Top,
                    Child           = new TextBlock
                    {
                        Text                = icon,
                        FontSize            = 16,
                        HorizontalAlignment = WpfHAlign.Center,
                        VerticalAlignment   = WpfVAlign.Center,
                    }
                };

                var row = new StackPanel { Orientation = WpfOrientation.Horizontal };
                row.Children.Add(iconBorder);
                row.Children.Add(textStack);

                card.Child = row;
                FeaturesGrid.Children.Add(card);
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────
        private void GetStarted_Click(object sender, RoutedEventArgs e)
        {
            LandingView.Visibility   = Visibility.Collapsed;
            InstallerView.Visibility = Visibility.Visible;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            LandingView.Visibility   = Visibility.Visible;
            InstallerView.Visibility = Visibility.Collapsed;
        }

        // ── Game path ─────────────────────────────────────────────────────────
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.FolderBrowserDialog
            {
                Description            = "Select the MineMogul game folder (contains MineMogul.exe)",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                txtGamePath.Text = dlg.SelectedPath;
        }

        private void Auto_Click(object sender, RoutedEventArgs e) => TryAutoDetect();

        private void GamePath_Changed(object sender, TextChangedEventArgs e) => UpdateStatus();

        private void TryAutoDetect()
        {
            try
            {
                string[] keys =
                {
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                    @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam",
                };
                foreach (var key in keys)
                {
                    if (Registry.GetValue(key, "InstallPath", null) is not string steam) continue;
                    var dir = System.IO.Path.Combine(steam, "steamapps", "common", "MineMogul");
                    if (Directory.Exists(dir) && File.Exists(System.IO.Path.Combine(dir, "MineMogul.exe")))
                    {
                        txtGamePath.Text = dir;
                        Log("Found MineMogul at: " + dir);
                        return;
                    }
                }
                Log("Auto-detect: MineMogul not found. Please select folder manually.");
            }
            catch (Exception ex) { Log("Auto-detect error: " + ex.Message); }
        }

        private void UpdateStatus()
        {
            string path = txtGamePath.Text.Trim();
            if (!Directory.Exists(path))
            {
                SetStatus(Status.None);
                return;
            }
            bool mod = File.Exists(System.IO.Path.Combine(path, "BepInEx", "plugins", "MML.dll"));
            bool bep = File.Exists(System.IO.Path.Combine(path, "winhttp.dll"));
            SetStatus(mod ? Status.Installed : bep ? Status.BepInExOnly : Status.NotInstalled);
        }

        private void SetStatus(Status s)
        {
            _status = s;
            (WpfColor dot, string text) = s switch
            {
                Status.Installed    => (WpfColor.FromRgb(0x29, 0xd9, 0x6c), "✓  MML is installed"),
                Status.BepInExOnly  => (WpfColor.FromRgb(0xf5, 0xa6, 0x23), "⚡  BepInEx found — MML not installed yet"),
                Status.NotInstalled => (WpfColor.FromRgb(0xdc, 0x3c, 0x3c), "✕  Not installed"),
                _                  => (WpfColor.FromRgb(0x72, 0x82, 0xa4), "—  Select a game folder above"),
            };
            StatusDot.Fill  = new SolidColorBrush(dot);
            StatusText.Text = text;
            StatusText.Foreground = new SolidColorBrush(dot);
        }

        // ── Install ───────────────────────────────────────────────────────────
        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            string gamePath = txtGamePath.Text.Trim();
            if (!ValidatePath(gamePath)) return;
            SetBusy(true);
            SetProgress(0);
            Log("");
            Log("─── Installation started ──────────────────────");
            try
            {
                await Task.Run(() =>
                {
                    Log("Extracting BepInEx...");
                    SetProgress(0.3);
                    ExtractBepInExZip(gamePath);
                    SetProgress(0.75);
                    Log("Copying MML.dll...");
                    WriteEmbeddedMmlDll(System.IO.Path.Combine(gamePath, "BepInEx", "plugins"));
                    SetProgress(1.0);
                });
                Log("");
                Log("✓ Installation complete!");
                Log("  F5 = Factory HUD  |  F6 = Belt labels  |  F = Splitter config");
                UpdateStatus();
                WpfMessageBox.Show(
                    "Installation successful!\nLaunch MineMogul and press F5 to open the mod overlay.",
                    "MML Installer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                WpfMessageBox.Show("Installation failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetBusy(false); }
        }

        // ── Uninstall ─────────────────────────────────────────────────────────
        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            string gamePath = txtGamePath.Text.Trim();
            if (!ValidatePath(gamePath)) return;
            if (WpfMessageBox.Show("Remove MML?\n\nYour save data will NOT be affected.",
                "Confirm Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SetBusy(true);
            SetProgress(0);
            Log("");
            Log("─── Uninstall started ─────────────────────────");
            try
            {
                bool removeBep = chkRemoveBep.IsChecked == true;
                await Task.Run(() =>
                {
                    foreach (var dll in new[]
                    {
                        System.IO.Path.Combine(gamePath, "BepInEx", "plugins", "MML.dll"),
                        System.IO.Path.Combine(gamePath, "BepInEx", "plugins", "MineMogulMod.dll"),
                    })
                        if (File.Exists(dll)) { File.Delete(dll); Log("Removed: " + System.IO.Path.GetFileName(dll)); }

                    string cfg = System.IO.Path.Combine(gamePath, "BepInEx", "config", "com.minemogul.mml.cfg");
                    if (File.Exists(cfg)) { File.Delete(cfg); Log("Removed config."); }

                    foreach (var f in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" })
                    {
                        var fp = System.IO.Path.Combine(gamePath, f);
                        if (File.Exists(fp)) { File.Delete(fp); Log("Removed: " + f); }
                    }

                    if (removeBep)
                    {
                        string bepDir = System.IO.Path.Combine(gamePath, "BepInEx");
                        if (Directory.Exists(bepDir)) { Directory.Delete(bepDir, true); Log("BepInEx folder removed."); }
                    }

                    SetProgress(1.0);
                });
                Log("");
                Log("✓ Uninstall complete.");
                UpdateStatus();
                WpfMessageBox.Show("MML has been removed.", "MML Installer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                WpfMessageBox.Show("Uninstall failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { SetBusy(false); }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = txtGamePath.Text.Trim();
            if (Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private bool ValidatePath(string path)
        {
            if (!Directory.Exists(path))
            { WpfMessageBox.Show("Please select a valid folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            if (!File.Exists(System.IO.Path.Combine(path, "MineMogul.exe")))
            { WpfMessageBox.Show("MineMogul.exe not found in this folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            return true;
        }

        private static Stream GetEmbedded(string name)
        {
            var asm  = Assembly.GetExecutingAssembly();
            string n = asm.GetManifestResourceNames().First(x => x.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            return asm.GetManifestResourceStream(n)!;
        }

        private void ExtractBepInExZip(string gamePath)
        {
            using var stream = GetEmbedded("BepInEx.zip");
            using var zip    = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string dest = System.IO.Path.Combine(gamePath, entry.FullName);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
                Log("  " + entry.FullName);
            }
        }

        private void WriteEmbeddedMmlDll(string pluginsDir)
        {
            Directory.CreateDirectory(pluginsDir);
            string dest = System.IO.Path.Combine(pluginsDir, "MML.dll");
            using var src = GetEmbedded("MML.dll");
            using var dst = File.Create(dest);
            src.CopyTo(dst);
            Log("Mod copied → " + dest);
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText(msg + Environment.NewLine);
                LogScroller.ScrollToEnd();
            });
        }

        private void SetBusy(bool busy)
        {
            Dispatcher.Invoke(() =>
            {
                btnInstall.IsEnabled   = !busy;
                btnUninstall.IsEnabled = !busy;
            });
        }

        // ── Progress ─────────────────────────────────────────────────────────
        private void StartProgressTimer()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _progressTimer.Tick += (_, _) =>
            {
                double parentW = ((Border)ProgressFill.Parent).ActualWidth;
                double target  = _progressTarget * parentW;
                _progressBarWidth += (target - _progressBarWidth) * 0.08;
                ProgressFill.Width = Math.Max(0, _progressBarWidth);
            };
            _progressTimer.Start();
        }

        private void SetProgress(double value)
        {
            _progressTarget = Math.Clamp(value, 0, 1);
        }
    }
}

