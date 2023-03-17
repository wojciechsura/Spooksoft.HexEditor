/******************************************************************************
*                                                                             *
* This code was generated automatically from a template. Don't modify it,     *
* because all your changes will be overwritten. Instead, if needed, modify    *
* the template file (*.tt)                                                    *
*                                                                             *
******************************************************************************/

using System;

namespace HexEditor.Geometry
{
    // *** BaseIntRectangle ***

    public abstract class BaseIntRectangle<TConcreteRectangle, TConcretePoint>
	    where TConcreteRectangle : BaseIntRectangle<TConcreteRectangle, TConcretePoint>
		where TConcretePoint : BaseIntPoint<TConcretePoint>
    {
        // Protected methods ------------------------------------------------------

        protected abstract TConcreteRectangle CreateRectangle(int left, int top, int right, int bottom);
        protected abstract TConcretePoint CreatePoint(int x, int y);

        // Public methods ---------------------------------------------------------

        public BaseIntRectangle(int left, int top, int right, int bottom)
        {
            if (right < left)
                throw new ArgumentOutOfRangeException(nameof(right));
            if (bottom < top)
                throw new ArgumentOutOfRangeException(nameof(bottom));

	        Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

		public bool Contains(TConcretePoint point)
		{
			return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
		}

		public TConcreteRectangle ExtendBy(TConcretePoint point)
		{
			return CreateRectangle(Math.Min(Left, point.X), Math.Min(Top, point.Y), Math.Max(Right, point.X), Math.Max(Bottom, point.Y));
		}

		public bool IntersectsWith(TConcreteRectangle rect) 
		{
			return rect.Left <= Right &&
				rect.Right >= Left &&
				rect.Top <= Bottom &&
				rect.Bottom >= Top;
		}

        // Public properties ------------------------------------------------------

        public int Left { get; }    
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public virtual int Width => Right - Left;
        public virtual int Height => Bottom - Top;

        public TConcretePoint Center => CreatePoint((Left + Right) / 2, (Top + Bottom) / 2);
    }

    // *** BaseFloatRectangle ***

    public abstract class BaseFloatRectangle<TConcreteRectangle, TConcretePoint>
	    where TConcreteRectangle : BaseFloatRectangle<TConcreteRectangle, TConcretePoint>
		where TConcretePoint : BaseFloatPoint<TConcretePoint>
    {
        // Protected methods ------------------------------------------------------

        protected abstract TConcreteRectangle CreateRectangle(float left, float top, float right, float bottom);
        protected abstract TConcretePoint CreatePoint(float x, float y);

        // Public methods ---------------------------------------------------------

        public BaseFloatRectangle(float left, float top, float right, float bottom)
        {
            if (float.IsNaN(left))
			    throw new ArgumentException(nameof(left));
            if (float.IsNaN(right))
			    throw new ArgumentException(nameof(right));
            if (float.IsNaN(top))
			    throw new ArgumentException(nameof(top));
            if (float.IsNaN(bottom))
			    throw new ArgumentException(nameof(bottom));
            if (right < left)
                throw new ArgumentOutOfRangeException(nameof(right));
            if (bottom < top)
                throw new ArgumentOutOfRangeException(nameof(bottom));

	        Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

		public bool Contains(TConcretePoint point)
		{
			return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
		}

		public TConcreteRectangle ExtendBy(TConcretePoint point)
		{
			return CreateRectangle(Math.Min(Left, point.X), Math.Min(Top, point.Y), Math.Max(Right, point.X), Math.Max(Bottom, point.Y));
		}

		public bool IntersectsWith(TConcreteRectangle rect) 
		{
			return rect.Left <= Right &&
				rect.Right >= Left &&
				rect.Top <= Bottom &&
				rect.Bottom >= Top;
		}

        // Public properties ------------------------------------------------------

        public float Left { get; }    
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
        public virtual float Width => Right - Left;
        public virtual float Height => Bottom - Top;

        public TConcretePoint Center => CreatePoint((Left + Right) / 2.0f, (Top + Bottom) / 2.0f);
    }

    // *** BaseDoubleRectangle ***

    public abstract class BaseDoubleRectangle<TConcreteRectangle, TConcretePoint>
	    where TConcreteRectangle : BaseDoubleRectangle<TConcreteRectangle, TConcretePoint>
		where TConcretePoint : BaseDoublePoint<TConcretePoint>
    {
        // Protected methods ------------------------------------------------------

        protected abstract TConcreteRectangle CreateRectangle(double left, double top, double right, double bottom);
        protected abstract TConcretePoint CreatePoint(double x, double y);

        // Public methods ---------------------------------------------------------

        public BaseDoubleRectangle(double left, double top, double right, double bottom)
        {
            if (double.IsNaN(left))
			    throw new ArgumentException(nameof(left));
            if (double.IsNaN(right))
			    throw new ArgumentException(nameof(right));
            if (double.IsNaN(top))
			    throw new ArgumentException(nameof(top));
            if (double.IsNaN(bottom))
			    throw new ArgumentException(nameof(bottom));
            if (right < left)
                throw new ArgumentOutOfRangeException(nameof(right));
            if (bottom < top)
                throw new ArgumentOutOfRangeException(nameof(bottom));

	        Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

		public bool Contains(TConcretePoint point)
		{
			return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
		}

		public TConcreteRectangle ExtendBy(TConcretePoint point)
		{
			return CreateRectangle(Math.Min(Left, point.X), Math.Min(Top, point.Y), Math.Max(Right, point.X), Math.Max(Bottom, point.Y));
		}

		public bool IntersectsWith(TConcreteRectangle rect) 
		{
			return rect.Left <= Right &&
				rect.Right >= Left &&
				rect.Top <= Bottom &&
				rect.Bottom >= Top;
		}

        // Public properties ------------------------------------------------------

        public double Left { get; }    
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }
        public virtual double Width => Right - Left;
        public virtual double Height => Bottom - Top;

        public TConcretePoint Center => CreatePoint((Left + Right) / 2.0, (Top + Bottom) / 2.0);
    }

}