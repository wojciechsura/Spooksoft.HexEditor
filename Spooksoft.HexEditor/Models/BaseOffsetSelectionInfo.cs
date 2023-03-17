using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Models
{
    public abstract class BaseOffsetSelectionInfo : BaseSelectionInfo
    {
        public BaseOffsetSelectionInfo(int offset)
        {
            Offset = offset;
        }

        public int Offset { get; }
    }
}
