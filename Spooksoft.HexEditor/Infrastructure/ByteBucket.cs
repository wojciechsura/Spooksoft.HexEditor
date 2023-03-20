using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Infrastructure
{
    internal class ByteBucket : IDisposable
    {
        // Private fields -----------------------------------------------------

        private int size;
        private int capacity;
        private byte[] data;
        private IBufferPool<byte> bufferPool;

        // Public methods -----------------------------------------------------

        public ByteBucket(byte[] source, int offset, int size, int newBucketCapacity, IBufferPool<byte> bufferPool)
        {
            if (newBucketCapacity < size)
                throw new ArgumentOutOfRangeException(nameof(newBucketCapacity));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (offset + size > source.Length)
                throw new ArgumentOutOfRangeException(nameof(size));

            this.bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));

            data = bufferPool.Rent(newBucketCapacity);
            Array.Copy(source, offset, data, 0, size);

            this.size = size;
            this.capacity = data.Length;
        }

        public ByteBucket(Stream stream, int size, int capacity, IBufferPool<byte> bufferPool)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (bufferPool == null)
                throw new ArgumentNullException(nameof(bufferPool));
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (size > capacity)
                throw new ArgumentOutOfRangeException(nameof(size), "Size is bigger than capacity!");
            if (stream.Length - stream.Position < size)
                throw new ArgumentOutOfRangeException(nameof(size), "Not enough data in stream");

            this.bufferPool = bufferPool;
            data = bufferPool.Rent(capacity);
            stream.Read(data, 0, size);

            this.size = size;
            this.capacity = capacity;
        }

        public ByteBucket(int capacity, IBufferPool<byte> bufferPool)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
            data = bufferPool.Rent(capacity);
            size = 0;
            this.capacity = capacity;
        }

        public void Append(byte b)
        {
            if (size + 1 > capacity)
                throw new InvalidOperationException("Not enough capacity!");

            data[size++] = b;
        }

        public void Append(byte[] target, int targetOffset, int length)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (size + length > capacity)
                throw new InvalidOperationException("Not enough capacity!");

            Array.Copy(target, targetOffset, data, size, length);
            size += length;
        }

        public void Balance(ByteBucket target)
        {
            if (target.capacity < (this.size + target.size + 1) / 2)
                throw new InvalidOperationException("Not enough capacity in target bucket");

            if (size < target.size)
            {
                int dataToMove = (target.size - size) / 2;
                if (dataToMove > 0)
                {
                    // Move (dataToMove) bytes from beginning of target's buffer to this one

                    Array.Copy(target.data, 0, data, size, dataToMove);
                    Array.Copy(target.data, dataToMove, target.data, 0, target.size - dataToMove);

                    size += dataToMove;
                    target.size -= dataToMove;
                }
            }
            else if (size > target.size)
            {
                int dataToMove = (size - target.size) / 2;
                if (dataToMove > 0)
                {
                    // Move (dataToMove) bytes from end of this bufer to target's beginning

                    Array.Copy(target.data, 0, target.data, dataToMove, target.size);
                    Array.Copy(data, size - dataToMove, target.data, 0, dataToMove);

                    size -= dataToMove;
                    target.size += dataToMove;
                }
            }

            // Else sizes are equal and there's nothing to balance
        }

        public int ContinueAppending(byte[] target, ref int targetOffset, ref int length)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            int bytesToAppend = Math.Min(length, capacity - size);

            if (bytesToAppend > 0)
            {
                Array.Copy(target, targetOffset, data, size, bytesToAppend);

                size += bytesToAppend;
                targetOffset += bytesToAppend;
                length -= bytesToAppend;
            }

            return bytesToAppend;
        }

        public int ContinueGettingBytes(int offset, byte[] target, ref int targetOffset, ref int length)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            int bytesToRead = Math.Min(size - offset, length);
            Array.Copy(data, offset, target, targetOffset, bytesToRead);

            length -= bytesToRead;
            targetOffset += bytesToRead;

            return bytesToRead;
        }

        public int ContinueReplacing(int offset, byte[] target, ref int targetOffset, ref int length)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            int bytesToReplace = Math.Min(size - offset, length);
            Array.Copy(target, targetOffset, data, offset, bytesToReplace);

            length -= bytesToReplace;
            targetOffset += bytesToReplace;

            return bytesToReplace;
        }

        public int ContinueRemoving(int offset, ref int length)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var bytesToRemove = Math.Min(size - offset, length);

            if (offset + bytesToRemove < size)
                Array.Copy(data, offset + bytesToRemove, data, offset, size - (offset + bytesToRemove));

            size -= bytesToRemove;
            length -= bytesToRemove;

            return bytesToRemove;
        }

        public void Dispose()
        {
            bufferPool.Return(data);

            data = null;
            size = -1;
            capacity = -1;
        }

        public void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCapacity));

            if (capacity < requiredCapacity)
                SetCapacity(requiredCapacity);
        }

        public byte GetByte(int offset)
        {
            if (offset >= size || offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return data[offset];
        }

        public void GetBytes(byte[] target, int targetOffset, int offset, int length)
        {
            if (targetOffset >= target.Length || targetOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (offset >= size || offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "Not enough space in target");
            if (offset + length > size)
                throw new ArgumentOutOfRangeException(nameof(length), "Not enough data to copy");

            Array.Copy(data, offset, target, targetOffset, length);
        }

        public void Join(ByteBucket bucket)
        {
            if (bucket == null)
                throw new ArgumentNullException(nameof(bucket));
            if (size + bucket.size > capacity)
                throw new ArgumentException("Not enough capacity to perform join", nameof(bucket));

            Array.Copy(bucket.data, 0, data, size, bucket.size);
            size += bucket.size;

            bucket.Dispose();
        }

        public void Remove(int offset, int length)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + length > size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (offset + length < size)
                Array.Copy(data, offset + length, data, offset, size - (offset + length));

            size -= length;
        }

        public void Replace(int offset, byte[] target, int targetOffset, int length)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (offset + length > size)
                throw new ArgumentOutOfRangeException(nameof(length), "Not enough data to replace!");
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length), "Not enough source data to replace!");

            Array.Copy(target, targetOffset, data, offset, length);
        }

        public void Replace(int offset, byte target)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            data[offset] = target;
        }

        public void SetCapacity(int newCapacity)
        {
            if (newCapacity < size)
                throw new ArgumentOutOfRangeException(nameof(newCapacity), "Capacity must be equal or greater than size");

            if (newCapacity != capacity)
            {
                capacity = newCapacity;

                var oldData = data;
                data = bufferPool.Rent(newCapacity);
                Array.Copy(oldData, 0, data, 0, size);
                bufferPool.Return(oldData);
            }
        }

        public ByteBucket Split(int offset, int newBucketCapacity)
        {
            if (offset < 0 || offset >= size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            int bytesToCopy = size - offset;

            if (newBucketCapacity < bytesToCopy)
                throw new ArgumentOutOfRangeException(nameof(newBucketCapacity), "Not enough target capacity");

            size = offset;

            return new ByteBucket(data, offset, bytesToCopy, newBucketCapacity, bufferPool);
        }

        public void WriteToStream(Stream stream)
        {
            if (size > 0)
                stream.Write(data, 0, size);
        }

        // Public properties --------------------------------------------------

        public int Size => size;

        public int Capacity => capacity;
    }
}
