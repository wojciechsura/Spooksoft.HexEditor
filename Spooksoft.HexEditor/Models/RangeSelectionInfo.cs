using HexEditor.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Models
{
    public class RangeSelectionInfo : BaseSelectionInfo
    {
        public RangeSelectionInfo(int selectionStart, int selectionEnd, DataArea area, bool cursorOnStart)
        {
            SelectionStart = selectionStart;
            SelectionEnd = selectionEnd;
            Area = area;
            CursorOnStart = cursorOnStart;
        }

        public override bool IsCharSelected(int offset) => offset >= SelectionStart && offset <= SelectionEnd;
        public override bool IsHexCharSelected(int offset, int @char) => offset >= SelectionStart && offset <= SelectionEnd;

        public int SelectionStart { get; }
        public int SelectionEnd { get; }
        public int SelectionLength => SelectionEnd - SelectionStart + 1;
        public DataArea Area { get; }
        public bool CursorOnStart { get; }
        public int Cursor => CursorOnStart ? SelectionStart : SelectionEnd;
        public int CursorOpposite => CursorOnStart ? SelectionEnd : SelectionStart;
    }
}
