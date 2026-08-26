using System.Numerics;

namespace persona.Models;

public class ThemeColors
{
    public Vector4 Heading = new(0.7f, 0.5f, 1.0f, 1.0f);
    public Vector4 Label = new(0.9f, 0.9f, 0.9f, 1.0f);
    public Vector4 Connected = new(0.4f, 1.0f, 0.4f, 1.0f);
    public Vector4 Disconnected = new(1.0f, 0.4f, 0.4f, 1.0f);
    public Vector4 Muted = new(0.5f, 0.5f, 0.5f, 1.0f);
    public Vector4 Conflict = new(1.0f, 0.6f, 0.6f, 1.0f);
}
