using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Infrastructure
{
    internal interface IBufferPool<T>
    {
        T[] Rent(int size);
        void Return(T[] buffer);
    }
}
