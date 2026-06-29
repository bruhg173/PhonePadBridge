using System.Diagnostics;
using System.Globalization;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PhonePadBridge.WebBridge;

public partial class MainWindow : Window
{
    private readonly LocalWebBridgeServer _server = new();
    private readonly VirtualXboxController _virtualXbox = new();
    private readonly Dictionary<string, Border> _buttonPills = new();
    private readonly DispatcherTimer _uiTimer;

    private Brush _accentBrush = new SolidColorBrush(Color.FromRgb(0x37, 0xD7, 0xFF));

    private readonly object _stateLock = new();
    private ControllerState? _latestState;
    private ulong _lastUiSeq;
    private long _receivedPackets;

    public MainWindow()
    {
        InitializeComponent();

        _server.StatusChanged += text => Dispatcher.BeginInvoke(() => Log(text));

        // Low-latency path:
        // Controller packets update the virtual Xbox controller immediately,
        // without waiting for the WPF GUI thread.
        _server.PacketReceived += state =>
        {
            _virtualXbox.Update(state);

            lock (_stateLock)
            {
                _latestState = state;
                _receivedPackets++;
            }
        };

        BuildButtonGrid();

        _uiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS GUI; controller still updates immediately.
        };
        _uiTimer.Tick += (_, _) => RefreshUiFromLatestState();
        _uiTimer.Start();

        Log("Ready. Press Start Bridge, then open the shown URL on phone browser.");
        Log("For Android USB mode, click Start Bridge first, then click Android USB Setup.");
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port))
        {
            Log("Invalid port.");
            return;
        }

        var virtualOk = _virtualXbox.TryStart();
        VirtualStatusText.Text = _virtualXbox.Status;
        Log(_virtualXbox.Status);

        if (!virtualOk)
        {
            Log("Install ViGEmBus if Rocket League does not see a controller. Live input can still display without it.");
        }

        try
        {
            await _server.StartAsync(port);
            ServerStatusText.Text = $"Running :{port}";
            UrlText.Text = LocalWebBridgeServer.GetUrlList(port) + Environment.NewLine + "Android USB ADB mode: http://127.0.0.1:" + port;
        }
        catch (Exception ex)
        {
            Log("Could not start web server: " + ex.Message);
            Log("Try another port or allow the app through Windows Firewall.");
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        await _server.StopAsync();
        _virtualXbox.Dispose();

        ServerStatusText.Text = "Stopped";
        VirtualStatusText.Text = "Stopped";
        PacketText.Text = "—";
        GamepadText.Text = "No gamepad yet";
        Log("Bridge stopped.");
    }

    private void AndroidUsbButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port))
        {
            Log("Invalid port.");
            return;
        }

        RunAdbReverse(port);
    }

    private void RunAdbReverse(int port)
    {
        try
        {
            Log("Checking ADB...");

            var adbPath = FindAdbPath();
            if (adbPath == null)
            {
                Log("adb.exe was not found.");
                Log("Install Android SDK Platform Tools, then add platform-tools to PATH.");
                Log("Or put adb.exe next to this app's .exe.");
                return;
            }

            Log("Using ADB: " + adbPath);

            var devices = RunProcess(adbPath, "devices");
            Log("adb devices:");
            Log(devices.Trim().Length == 0 ? "(no output)" : devices.Trim());

            if (devices.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                Log("Phone is unauthorized. Unlock Android and tap Allow USB debugging.");
                Log("If no popup appears, revoke USB debugging authorizations and reconnect USB.");
                return;
            }

            var reverse = RunProcess(adbPath, $"reverse tcp:{port} tcp:{port}");
            if (!string.IsNullOrWhiteSpace(reverse))
            {
                Log(reverse.Trim());
            }

            Log("ADB reverse ready.");
            Log($"Open this on Android Chrome: http://127.0.0.1:{port}");
        }
        catch (Exception ex)
        {
            Log("ADB setup failed: " + ex.Message);
        }
    }

    private static string? FindAdbPath()
    {
        var appDir = AppContext.BaseDirectory;
        var local = IOPath.Combine(appDir, "adb.exe");
        if (IOFile.Exists(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var entry in path.Split(IOPath.PathSeparator))
        {
            try
            {
                var candidate = IOPath.Combine(entry.Trim(), "adb.exe");
                if (IOFile.Exists(candidate)) return candidate;
            }
            catch
            {
                // ignore bad PATH entries
            }
        }

        return null;
    }

    private static string RunProcess(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return "Could not start process.";

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit(5000);

        return (output + Environment.NewLine + error).Trim();
    }

    private void RefreshUiFromLatestState()
    {
        ControllerState? state;
        long packetCount;

        lock (_stateLock)
        {
            state = _latestState;
            packetCount = _receivedPackets;
        }

        if (state == null) return;
        if (state.Seq == _lastUiSeq) return;

        _lastUiSeq = state.Seq;
        ApplyStateToUi(state, packetCount);
    }

    private void ApplyStateToUi(ControllerState s, long packetCount)
    {
        PacketText.Text = "#" + s.Seq;
        GamepadText.Text = string.IsNullOrWhiteSpace(s.Id) ? "Gamepad" : s.Id;

        MoveStick(LeftStickDot, s.Lx, s.Ly);
        MoveStick(RightStickDot, s.Rx, s.Ry);

        LeftStickText.Text = $"{s.Lx:0.00}, {s.Ly:0.00}";
        RightStickText.Text = $"{s.Rx:0.00}, {s.Ry:0.00}";

        LtBar.Value = Math.Clamp(s.Lt * 100, 0, 100);
        RtBar.Value = Math.Clamp(s.Rt * 100, 0, 100);

        SetPill("A", s.A);
        SetPill("B", s.B);
        SetPill("X", s.X);
        SetPill("Y", s.Y);
        SetPill("LB", s.Lb);
        SetPill("RB", s.Rb);
        SetPill("Back", s.Back);
        SetPill("Start", s.Start);
        SetPill("LS", s.Ls);
        SetPill("RS", s.Rs);
        SetPill("↑", s.DpadUp);
        SetPill("↓", s.DpadDown);
        SetPill("←", s.DpadLeft);
        SetPill("→", s.DpadRight);
    }

    private void MoveStick(Ellipse dot, float x, float y)
    {
        // Dot is 18px on a 168px stick canvas, so 75 is centered.
        const double center = 75;
        const double range = 65;

        Canvas.SetLeft(dot, center + Math.Clamp(x, -1f, 1f) * range);
        Canvas.SetTop(dot, center - Math.Clamp(y, -1f, 1f) * range);
    }

    private void BuildButtonGrid()
    {
        ButtonsPanel.Children.Clear();

        var names = new[] { "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "↑", "↓", "←", "→" };

        foreach (var name in names)
        {
            var text = new TextBlock
            {
                Text = name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x0B, 0x14, 0x28),
                    Color.FromRgb(0x06, 0x0D, 0x1A),
                    35),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x42, 0x6D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(8),
                Margin = new Thickness(4),
                MinHeight = 36,
                Child = text
            };

            _buttonPills[name] = border;
            ButtonsPanel.Children.Add(border);
        }
    }

    private void SetPill(string name, bool active)
    {
        if (!_buttonPills.TryGetValue(name, out var pill)) return;

        pill.Background = active
            ? _accentBrush
            : new LinearGradientBrush(Color.FromRgb(0x0B, 0x14, 0x28), Color.FromRgb(0x06, 0x0D, 0x1A), 35);

        pill.BorderBrush = active
            ? _accentBrush
            : new SolidColorBrush(Color.FromRgb(0x28, 0x42, 0x6D));

        pill.Effect = active
            ? new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.55, Color = Colors.Cyan }
            : null;
    }

    private void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string hex) return;

        var color = (Color)ColorConverter.ConvertFromString(hex);
        _accentBrush = new SolidColorBrush(color);

        LeftStickDot.Fill = _accentBrush;
        RightStickDot.Fill = _accentBrush;
        UrlText.Foreground = _accentBrush;

        Log("Accent changed to " + hex);
    }

    private void Log(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        LogText.Text = line + Environment.NewLine + LogText.Text;
    }

    protected override async void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
        await _server.StopAsync();
        _server.Dispose();
        _virtualXbox.Dispose();
        base.OnClosed(e);
    }
}
