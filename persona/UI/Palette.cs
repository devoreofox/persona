using System.Numerics;
using persona.Models;

namespace persona.UI;

public static class Palette
{
    public static Vector4 Heading;
    public static Vector4 Label;
    public static Vector4 Connected;
    public static Vector4 Disconnected;

    public static void Apply(ThemeColors t)
    {
        Heading = t.Heading;
        Label = t.Label;
        Connected = t.Connected;
        Disconnected = t.Disconnected;
    }
}
