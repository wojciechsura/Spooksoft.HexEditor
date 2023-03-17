using HexEditor.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HexEditor.Units
{
    public class PixelRectangle : BaseDoubleRectangle<PixelRectangle, PixelPoint>
    {
        protected override PixelPoint CreatePoint(double x, double y)
        {
            return new PixelPoint(x, y);
        }

        protected override PixelRectangle CreateRectangle(double left, double top, double right, double bottom)
        {
            return new PixelRectangle(left, top, right, bottom);
        }

        public PixelRectangle(double left, double top, double right, double bottom) 
            : base(left, top, right, bottom)
        {

        }

        public Rect ToRect()
        {
            return new Rect(Left, Top, Width, Height);
        }

        public override double Width => base.Width + 1;
        public override double Height => base.Height + 1;
    }
}
