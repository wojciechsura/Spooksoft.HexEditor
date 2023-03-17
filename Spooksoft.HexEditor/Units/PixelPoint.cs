using HexEditor.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HexEditor.Units
{
    public class PixelPoint : BaseDoublePoint<PixelPoint>
    {
        public PixelPoint(double x, double y) : base(x, y)
        {

        }

        public PixelPoint(Point point)
            : base(point.X, point.Y)
        {

        }
    }
}
