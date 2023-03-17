using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Models
{
    public class HexCursorSelectionInfo : BaseOffsetSelectionInfo
    {
        public HexCursorSelectionInfo(int offset, int @char)
            : base(offset)
        {
            Char = @char;
        }

        public override bool IsCharSelected(int offset) => false;
        public override bool IsHexCharSelected(int offset, int @char) => offset == Offset && @char == Char;

        public int Char { get; }
    }
}
