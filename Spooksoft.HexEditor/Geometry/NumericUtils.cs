using System;

namespace Spooksoft.HexEditor.Geometry
{
    public static class IntUtils
    {
        public static int ClampTo(this int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static bool IsWithin(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        public static bool IsOutside(this int value, int min, int max)
        {
            return value < min && value > max;
        }

        public static bool IsWithinExclusive(this int value, int min, int max)
        {
            return value > min && value < max;
        }
    }

    public static class FloatUtils
    {
        public static float ClampTo(this float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static bool IsWithin(this float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        public static bool IsOutside(this float value, float min, float max)
        {
            return value < min && value > max;
        }

        public static bool IsWithinExclusive(this float value, float min, float max)
        {
            return value > min && value < max;
        }
    }

    public static class DoubleUtils
    {
        public static double ClampTo(this double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static bool IsWithin(this double value, double min, double max)
        {
            return value >= min && value <= max;
        }

        public static bool IsOutside(this double value, double min, double max)
        {
            return value < min && value > max;
        }

        public static bool IsWithinExclusive(this double value, double min, double max)
        {
            return value > min && value < max;
        }
    }

}