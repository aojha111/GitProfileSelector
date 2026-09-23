namespace GitProfile.Core;

/// <summary>
/// One account as the picker shows it: who it is, what it currently does, and the colours to draw it
/// in. Colours are hex strings so this stays free of user interface types.
/// </summary>
public sealed record AccountRow(string Login, string Initials, string Avatar, string Role, bool IsDefault, bool IsPinned)
{
    public static string InitialsOf(string login)
    {
        var trimmed = login.Trim();
        return trimmed.Length switch
        {
            0 => "?",
            1 => trimmed[..1].ToUpperInvariant(),
            _ => trimmed[..2].ToUpperInvariant(),
        };
    }

    /// <summary>A colour spread evenly over the wheel, kept dark enough for white initials.</summary>
    public static string AvatarOf(string login)
    {
        var hash = 14_171UL;
        foreach (var character in login)
            hash = hash * 31 + character;
        return Hue((int)(hash % 360));
    }

    private static string Hue(int degrees)
    {
        const double saturation = 0.58, lightness = 0.45;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var secondary = chroma * (1 - Math.Abs((degrees % 60) / 30.0 - 1));
        var match = lightness - chroma / 2;
        var (r, g, b) = degrees switch
        {
            < 60 => (chroma, secondary, 0.0),
            < 120 => (secondary, chroma, 0.0),
            < 180 => (0.0, chroma, secondary),
            < 240 => (0.0, secondary, chroma),
            < 300 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary),
        };
        return $"#{ToByte(r + match):X2}{ToByte(g + match):X2}{ToByte(b + match):X2}";

        static int ToByte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
    }
}
