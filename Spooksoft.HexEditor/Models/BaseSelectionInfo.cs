using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Models
{
    public abstract class BaseSelectionInfo
    {
        public abstract bool IsHexCharSelected(int offset, int @char);
        public abstract bool IsCharSelected(int offset);
    }
}
