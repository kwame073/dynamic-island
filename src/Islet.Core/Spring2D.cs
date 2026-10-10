namespace Islet.Core;

/// <summary>Two <see cref="SpringValue"/>s animating a 2D point with shared parameters.</summary>
public sealed class Spring2D
{
    public readonly SpringValue X;
    public readonly SpringValue Y;

    public Spring2D(Vec2 initial = default, SpringParams? p = null)
    {
        X = new SpringValue(initial.X, p);
        Y = new SpringValue(initial.Y, p);
    }

    public Vec2 Value => new(X.Value, Y.Value);
    public Vec2 Velocity => new(X.Velocity, Y.Velocity);
    public bool AtRest => X.AtRest && Y.AtRest;

    public SpringParams Params
    {
        set { X.Params = value; Y.Params = value; }
    }

    public void SetTarget(Vec2 t) { X.SetTarget(t.X); Y.SetTarget(t.Y); }
    public void SnapTo(Vec2 v) { X.SnapTo(v.X); Y.SnapTo(v.Y); }

    public bool Update(double dt)
    {
        bool a = X.Update(dt);
        bool b = Y.Update(dt);
        return a || b;
    }
}
