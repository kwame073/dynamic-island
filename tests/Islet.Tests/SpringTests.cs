using Islet.Core;

namespace Islet.Tests;

public class SpringTests
{
    private static int Steps(SpringValue s, double seconds, double dt = 1.0 / 120)
    {
        int n = 0;
        for (double t = 0; t < seconds; t += dt) { s.Update(dt); n++; }
        return n;
    }

    [Fact]
    public void Settles_To_Target()
    {
        var s = new SpringValue(0, SpringParams.Default);
        s.SetTarget(100);
        Steps(s, 3.0);
        Assert.True(s.AtRest, "spring should come to rest");
        Assert.Equal(100, s.Value, 1.0);
    }

    [Fact]
    public void Retarget_Preserves_Velocity()
    {
        var s = new SpringValue(0, SpringParams.Default);
        s.SetTarget(100);
        Steps(s, 0.15); // mid-flight, moving
        double vBefore = s.Velocity;
        Assert.True(System.Math.Abs(vBefore) > 1.0, "should be moving mid-flight");

        s.SetTarget(0); // interrupt
        Assert.Equal(vBefore, s.Velocity, 1e-9); // velocity continuity: not reset
        Assert.False(s.AtRest);
    }

    [Fact]
    public void Snap_Is_Instant_And_Resting()
    {
        var s = new SpringValue(0, SpringParams.Default);
        s.SetTarget(100);
        s.SnapTo(42);
        Assert.True(s.AtRest);
        Assert.Equal(42, s.Value, 1e-9);
        Assert.Equal(0, s.Velocity, 1e-9);
        Assert.False(s.Update(1.0 / 120));
    }

    [Fact]
    public void Underdamped_Overshoots()
    {
        var s = new SpringValue(0, new SpringParams(0.45, 0.55)); // low damping => overshoot
        s.SetTarget(100);
        double max = 0;
        for (double t = 0; t < 2.0; t += 1.0 / 120) { s.Update(1.0 / 120); max = System.Math.Max(max, s.Value); }
        Assert.True(max > 100.0, $"expected overshoot above 100, got {max}");
    }

    [Fact]
    public void Large_Dt_Is_Stable()
    {
        var s = new SpringValue(0, SpringParams.Default);
        s.SetTarget(100);
        for (int i = 0; i < 10; i++) s.Update(0.5); // huge frames
        Assert.True(s.Value is > 50 and < 150, $"value should stay bounded, got {s.Value}");
    }
}
