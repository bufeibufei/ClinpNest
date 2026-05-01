using System.Windows.Input;

namespace ClipNest.Models;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public sealed class HotkeySettings
{
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Shift;
    public Key Key { get; set; } = Key.V;

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
            parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }

    public override string ToString() => $"{(int)Modifiers}|{Key}";

    public static HotkeySettings Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HotkeySettings();
        }

        var parts = value.Split('|', 2);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var modifiers) ||
            !Enum.TryParse<Key>(parts[1], out var key))
        {
            return new HotkeySettings();
        }

        return new HotkeySettings { Modifiers = (HotkeyModifiers)modifiers, Key = key };
    }
}
