using System;

namespace Islet.Core;

/// <summary>
/// Spring parameters expressed the SwiftUI way: <see cref="Response"/> (the natural period, seconds)
/// and <see cref="DampingFraction"/> (zeta; 1.0 = critically damped, &lt;1 overshoots).
/// This matches the mental model Apple uses for Dynamic Island animations.
/// </summary>
public readonly record struct SpringParams(double Response, double DampingFraction)
{
    /// <summary>Primary geometry spring (size/radius/position). Estimate tuned to the reference.</summary>
    public static SpringParams Default => new(0.45, 0.76);

    /// <summary>Slightly softer spring for content transforms.</summary>
    public static SpringParams Content => new(0.40, 0.82);

    /// <summary>Fast, crisp spring for small indicator moves.</summary>
    public static SpringParams Snappy => new(0.32, 0.86);

    /// <summary>Natural angular frequency (rad/s).</summary>
    public double Omega => 2.0 * Math.PI / Math.Max(1e-4, Response);

    /// <summary>Stiffness term k = omega^2.</summary>
    public double Stiffness => Omega * Omega;

    /// <summary>Damping term c = 2 * zeta * omega.</summary>
    public double Damping => 2.0 * DampingFraction * Omega;
}

/// <summary>
/// A single animated scalar driven by a damped-spring integrator.
/// Retargeting never resets velocity, so animations are interruptible and continue
/// from the current speed (velocity continuity), exactly as required.
/// </summary>
public sealed class SpringValue
{
    public double Value;
    public double Velocity;
    public double Target;
    public SpringParams Params;
    public bool AtRest { get; private set; }

    // Rest thresholds; relative to a reference scale so small values settle too.
    public double RestDisplacement = 0.1;
    public double RestVelocity = 0.1;

    public SpringValue(double initial = 0, SpringParams? p = null)
    {
        Value = Target = initial;
        Params = p ?? SpringParams.Default;
        AtRest = true;
    }

    /// <summary>Set a new target, preserving current velocity (interruptible retarget).</summary>
    public void SetTarget(double target)
    {
        if (Math.Abs(target - Target) > 1e-9)
        {
            Target = target;
            AtRest = false;
        }
    }

    /// <summary>Jump instantly to a value with zero velocity (no animation).</summary>
    public void SnapTo(double value)
    {
        Value = Target = value;
        Velocity = 0;
        AtRest = true;
    }

    /// <summary>Advance the simulation by <paramref name="dt"/> seconds. Returns true while animating.</summary>
    public bool Update(double dt)
    {
        if (AtRest) return false;
        if (dt <= 0) return true;

        double k = Params.Stiffness;
        double c = Params.Damping;

        // Semi-implicit Euler with fixed substeps for stability at any frame rate.
        const double maxStep = 1.0 / 240.0;
        double remaining = Math.Min(dt, 0.1); // guard against huge dt (e.g. after a stall)
        while (remaining > 1e-9)
        {
            double h = Math.Min(maxStep, remaining);
            remaining -= h;
            double x = Value - Target;
            double accel = -k * x - c * Velocity;
            Velocity += accel * h;
            Value += Velocity * h;
        }

        if (Math.Abs(Value - Target) < RestDisplacement && Math.Abs(Velocity) < RestVelocity)
        {
            Value = Target;
            Velocity = 0;
            AtRest = true;
            return false;
        }
        return true;
    }
}
