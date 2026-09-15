using System;
using System.Numerics;

namespace MoonlightSuite.Core;

internal static class MoonMath
{
    public const float Epsilon = 1e-6f;

    public static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    public static Vector2 SafeNormalize(Vector2 v)
    {
        var length = v.Length();
        return length > Epsilon ? v / length : Vector2.Zero;
    }

    public static Vector2 ClampLength(Vector2 v, float max)
    {
        if (max <= 0f)
            return Vector2.Zero;

        var length = v.Length();
        return length > max ? v * (max / length) : v;
    }

    public static float Alpha(float deltaTime, float tau)
        => tau <= Epsilon ? 1f : 1f - MathF.Exp(-deltaTime / tau);

    public static float Damp(float current, float target, float deltaTime, float tau)
        => current + (target - current) * Alpha(deltaTime, tau);

    public static Vector2 Damp(Vector2 current, Vector2 target, float deltaTime, float tau)
        => Vector2.Lerp(current, target, Alpha(deltaTime, tau));

    public static float WrapDegrees(float degrees)
    {
        degrees %= 360f;

        if (degrees >= 180f)
            return degrees - 360f;

        if (degrees < -180f)
            return degrees + 360f;

        return degrees;
    }

    public static Vector2 Rotate(Vector2 v, float radians)
        => Vector2.Transform(v, Matrix3x2.CreateRotation(radians));

    public static Vector2 Perpendicular(Vector2 v) => new(-v.Y, v.X);

    public static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
