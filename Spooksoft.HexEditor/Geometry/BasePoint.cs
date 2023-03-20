/******************************************************************************
*                                                                             *
* This code was generated automatically from a template. Don't modify it,     *
* because all your changes will be overwritten. Instead, if needed, modify    *
* the template file (*.tt)                                                    *
*                                                                             *
******************************************************************************/

using System;

namespace Spooksoft.HexEditor.Geometry
{
    public class BaseIntPoint<TConcretePoint>
        where TConcretePoint : BaseIntPoint<TConcretePoint>
    {
        // Public methods ---------------------------------------------------------

        public BaseIntPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }
    public class BaseFloatPoint<TConcretePoint>
        where TConcretePoint : BaseFloatPoint<TConcretePoint>
    {
        // Public methods ---------------------------------------------------------

        public BaseFloatPoint(float x, float y)
        {
            if (float.IsNaN(x))
                throw new ArgumentException(nameof(x));
            if (float.IsNaN(y))
                throw new ArgumentException(nameof(y));
				
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }
    public class BaseDoublePoint<TConcretePoint>
        where TConcretePoint : BaseDoublePoint<TConcretePoint>
    {
        // Public methods ---------------------------------------------------------

        public BaseDoublePoint(double x, double y)
        {
            if (double.IsNaN(x))
                throw new ArgumentException(nameof(x));
            if (double.IsNaN(y))
                throw new ArgumentException(nameof(y));
				
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }
}