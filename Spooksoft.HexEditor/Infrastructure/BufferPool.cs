using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Infrastructure
{
    public class BufferPool<T> : IBufferPool<T>
    {
        // Private types ------------------------------------------------------

        private class Pool
        {
            public List<T[]> Used { get; } = new List<T[]>();
            public List<T[]> Available { get; } = new List<T[]>();
        }

        // Private fields -----------------------------------------------------

        private readonly Dictionary<int, Pool> pools = new Dictionary<int, Pool>();

        // Private methods ----------------------------------------------------

        private Pool GetOrCreatePool(int size)
        {
            Pool pool;
            if (pools.ContainsKey(size))
                pool = pools[size];
            else
            {
                pool = new Pool();
                pools[size] = pool;
            }

            return pool;
        }

        // Public methods -----------------------------------------------------

        public T[] Rent(int size)
        {
            Pool pool;

            pool = GetOrCreatePool(size);

            if (pool.Available.Count > 0)
            {
                var buffer = pool.Available[pool.Available.Count - 1];
                pool.Available.RemoveAt(pool.Available.Count - 1);
                pool.Used.Add(buffer);

                return buffer;
            }
            else
            {
                var buffer = new T[size];
                pool.Used.Add(buffer);

                return buffer;
            }
        }

        public void Return(T[] buffer)
        {
            if (pools.ContainsKey(buffer.Length))
            {
                var pool = pools[buffer.Length];
                if (pool.Used.Contains(buffer))
                {
                    pool.Used.Remove(buffer);
                    pool.Available.Add(buffer);
                    return;
                }
            }

            throw new InvalidOperationException("Attempt to return non-rented buffer (may lead to memory fragmentation)");
        }

        public void Preallocate(int size, int count)
        {
            var pool = GetOrCreatePool(size);
            while (pool.Available.Count < count)
            {
                pool.Available.Add(new T[size]);
            }
        }
    }
}
