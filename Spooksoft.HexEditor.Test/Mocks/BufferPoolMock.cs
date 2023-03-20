using Spooksoft.HexEditor.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Test.Mocks
{
    class BufferPoolMock<T> : IBufferPool<T>
    {
        public T[] Rent(int size)
        {
            return new T[size];
        }

        public void Return(T[] buffer)
        {
            // Do nothing
        }
    }
}
