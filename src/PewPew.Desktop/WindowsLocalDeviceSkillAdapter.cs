using System.Diagnostics;
using System.Runtime.InteropServices;
using PewPew.Application.DeviceSkills;
using PewPew.Application.IntentRouting;

namespace PewPew.Desktop;

/// <summary>
/// Windows-only adapter. The application layer supplies only allowlisted enum
/// values; no executable path, shell text, or arbitrary virtual key crosses
/// this boundary.
/// </summary>
public sealed class WindowsLocalDeviceSkillAdapter : ILocalDeviceSkillAdapter
{
    private const ushort VkMediaNextTrack = 0xB0;
    private const ushort VkMediaPreviousTrack = 0xB1;
    private const ushort VkMediaStop = 0xB2;
    private const ushort VkMediaPlayPause = 0xB3;
    private const ushort VkVolumeDown = 0xAE;
    private const ushort VkVolumeUp = 0xAF;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public Task<LocalDeviceSkillAdapterResult> ExecuteAsync(
        AuthorizedLocalDeviceSkillCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var deviceCommand = command.Command;

        return deviceCommand.Intent switch
        {
            LocalIntentName.OpenApplication => Task.FromResult(OpenApplication(deviceCommand.Resource)),
            LocalIntentName.MediaControl => Task.FromResult(SendMediaCommand(deviceCommand.Resource)),
            LocalIntentName.AdjustVolume => Task.FromResult(SendVolumeCommand(deviceCommand.Resource)),
            _ => Task.FromResult(LocalDeviceSkillAdapterResult.Failed("unsupported_device_skill"))
        };
    }

    private static LocalDeviceSkillAdapterResult OpenApplication(string applicationId)
    {
        var executable = applicationId switch
        {
            "calculator" => "calc.exe",
            "notepad" => "notepad.exe",
            _ => null
        };

        if (executable is null)
        {
            return LocalDeviceSkillAdapterResult.Failed("application_not_allowlisted");
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            return process is not null && !process.HasExited
                ? LocalDeviceSkillAdapterResult.Verified($"process_started:{applicationId}")
                : LocalDeviceSkillAdapterResult.Failed("application_start_not_verified");
        }
        catch (Exception)
        {
            return LocalDeviceSkillAdapterResult.Failed("application_start_failed");
        }
    }

    private static LocalDeviceSkillAdapterResult SendMediaCommand(string command) =>
        command switch
        {
            "play" or "pause" => SendVirtualKey(VkMediaPlayPause, "media_play_pause_input_accepted"),
            "stop" => SendVirtualKey(VkMediaStop, "media_stop_input_accepted"),
            "next" => SendVirtualKey(VkMediaNextTrack, "media_next_input_accepted"),
            "previous" => SendVirtualKey(VkMediaPreviousTrack, "media_previous_input_accepted"),
            _ => LocalDeviceSkillAdapterResult.Failed("media_command_not_allowlisted")
        };

    private static LocalDeviceSkillAdapterResult SendVolumeCommand(string direction) =>
        direction switch
        {
            "up" => SendVirtualKey(VkVolumeUp, "volume_up_input_accepted"),
            "down" => SendVirtualKey(VkVolumeDown, "volume_down_input_accepted"),
            _ => LocalDeviceSkillAdapterResult.Failed("volume_direction_not_allowlisted")
        };

    private static LocalDeviceSkillAdapterResult SendVirtualKey(ushort virtualKey, string evidence)
    {
        var inputs = new[]
        {
            Input.Keyboard(virtualKey, 0),
            Input.Keyboard(virtualKey, KeyEventKeyUp)
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length
            ? LocalDeviceSkillAdapterResult.Verified(evidence)
            : LocalDeviceSkillAdapterResult.Failed("windows_input_not_accepted");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, [In] Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;

        public static Input Keyboard(ushort virtualKey, uint flags) => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = flags } }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
