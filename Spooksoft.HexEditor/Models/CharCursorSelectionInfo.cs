using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Models
{
    public class CharCursorSelectionInfo : BaseOffsetSelectionInfo
    {
        public CharCursorSelectionInfo(int offset)
            : base(offset)
        {

        }

        public override bool IsCharSelected(int offset) => offset == Offset;
        public override bool IsHexCharSelected(int offset, int @char) => false;
    }
}
