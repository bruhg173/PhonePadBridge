using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace PhonePadBridge.WebBridge;

public sealed class VirtualXboxController : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;

    public bool IsConnected { get; private set; }
    public string Status { get; private set; } = "Virtual controller not started";

    public bool TryStart()
    {
        try
        {
            if (IsConnected) return true;

            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.Connect();

            IsConnected = true;
            Status = "Virtual Xbox 360 controller connected";
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Status = "Virtual controller failed: " + ex.Message;
            return false;
        }
    }

    public void Update(ControllerState s)
    {
        if (_controller == null || !IsConnected) return;

        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, ToShortAxis(s.Lx));
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, ToShortAxis(s.Ly));
        _controller.SetAxisValue(Xbox360Axis.RightThumbX, ToShortAxis(s.Rx));
        _controller.SetAxisValue(Xbox360Axis.RightThumbY, ToShortAxis(s.Ry));

        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, ToByteTrigger(s.Lt));
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, ToByteTrigger(s.Rt));

        _controller.SetButtonState(Xbox360Button.A, s.A);
        _controller.SetButtonState(Xbox360Button.B, s.B);
        _controller.SetButtonState(Xbox360Button.X, s.X);
        _controller.SetButtonState(Xbox360Button.Y, s.Y);

        _controller.SetButtonState(Xbox360Button.LeftShoulder, s.Lb);
        _controller.SetButtonState(Xbox360Button.RightShoulder, s.Rb);

        _controller.SetButtonState(Xbox360Button.Back, s.Back);
        _controller.SetButtonState(Xbox360Button.Start, s.Start);

        _controller.SetButtonState(Xbox360Button.LeftThumb, s.Ls);
        _controller.SetButtonState(Xbox360Button.RightThumb, s.Rs);

        _controller.SetButtonState(Xbox360Button.Up, s.DpadUp);
        _controller.SetButtonState(Xbox360Button.Down, s.DpadDown);
        _controller.SetButtonState(Xbox360Button.Left, s.DpadLeft);
        _controller.SetButtonState(Xbox360Button.Right, s.DpadRight);

        _controller.SubmitReport();
    }

    private static short ToShortAxis(float value)
    {
        value = Math.Clamp(value, -1f, 1f);
        return (short)(value >= 0 ? value * short.MaxValue : value * 32768f);
    }

    private static byte ToByteTrigger(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return (byte)(value * byte.MaxValue);
    }

    public void Dispose()
    {
        try
        {
            _controller?.Disconnect();
        }
        catch
        {
            // ignored
        }

        _controller = null;
        _client?.Dispose();
        _client = null;
        IsConnected = false;
        Status = "Virtual controller stopped";
    }
}
