namespace Islet.Core;

/// <summary>Minimal double-precision 2D vector used by geometry and layout math.</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);
    public Vec2 Plus(Vec2 o) => new(X + o.X, Y + o.Y);
    public Vec2 Scaled(double s) => new(X * s, Y * s);
}
