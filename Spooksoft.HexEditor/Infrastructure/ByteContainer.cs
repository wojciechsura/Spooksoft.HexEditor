using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Infrastructure
{
    public class DataChangeEventArgs
    {
        public DataChangeEventArgs(ByteBufferChange changeKind, int offset, int length)
        {
            Change = changeKind;
            Offset = offset;
            Length = length;
        }

        public ByteBufferChange Change { get; }
        public int Offset { get; }
        public int Length { get; }
    }

    public delegate void DataChangeEventHandler(object sender, DataChangeEventArgs args);

    public class ByteContainer : IDisposable
    {
        // Private fields -----------------------------------------------------

        private readonly BufferPool<byte> bufferPool;

        private readonly int bucketSize;
        private readonly List<ByteBucket> buckets = new List<ByteBucket>();

        private int? cachedSize = null;

        // Private methods ----------------------------------------------------

        private (int bucketIndex, int bucketOffset) GetBucketFromOffset(int offset)
        {
            int bucketIndex = 0;

            while (bucketIndex < buckets.Count && offset >= buckets[bucketIndex].Size)
            {
                offset -= buckets[bucketIndex].Size;
                bucketIndex++;
            }

            return (bucketIndex, offset);
        }

        private void OptimizeBuckets()
        {
            int i = 0;

            while (i < buckets.Count)
            {
                if (buckets[i].Size == 0)
                {
                    buckets[i].Dispose();
                    buckets.RemoveAt(i);

                    continue;
                }

                if (buckets[i].Size < bucketSize)
                {
                    while (i + 1 < buckets.Count && buckets[i + 1].Size == 0)
                    {
                        buckets[i + 1].Dispose();
                        buckets.RemoveAt(i + 1);
                    }

                    if (i + 1 < buckets.Count)
                    {
                        if (buckets[i].Size + buckets[i + 1].Size <= bucketSize)
                        {
                            buckets[i].Join(buckets[i + 1]);
                            buckets.RemoveAt(i + 1);

                            // Continue joining buckets to current
                            // as long as possible
                            continue;
                        }
                        else if (buckets[i].Size < bucketSize / 2 && buckets[i].Size + buckets[i + 1].Size > bucketSize)
                        {
                            // Balance only if current bucket's size
                            // is less than half buffer

                            buckets[i].Balance(buckets[i + 1]);

                            i++;
                        }
                        else
                            i++;
                    }
                    else
                        i++;
                }
                else
                    i++;
            }
        }

        private void ProcessBucketsForInserting(int offset, ref int bucketIndex, ref int bucketOffset)
        {
            if (bucketOffset > 0)
            {
                // User started entering data at different place, optimizing buckets
                OptimizeBuckets();

                // Optimization doesn't change count of data, only reorganizes bytes
                // within buckets, but indices might have changed (buckets might have
                // been removed or joined)
                (bucketIndex, bucketOffset) = GetBucketFromOffset(offset);

                // If we're still somewhere inside bucket, split the bucket, so that
                // we'll be appending to end of bucket's buffer
                if (bucketOffset > 0)
                {
                    var newBucket = buckets[bucketIndex].Split(bucketOffset, bucketSize);
                    buckets.Insert(bucketIndex + 1, newBucket);

                    bucketIndex++;
                    bucketOffset = 0;
                }
            }
        }

        // Protected methods --------------------------------------------------

        protected int CountAvailableBytes(int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            int availableBytes = 0;
            int requiredBytes = length;

            while (bucketIndex < buckets.Count && requiredBytes > 0)
            {
                int bytes = Math.Min(requiredBytes, buckets[bucketIndex].Size - bucketOffset);

                availableBytes += bytes;
                requiredBytes -= bytes;

                bucketIndex++;
                bucketOffset = 0;
            }

            return availableBytes;
        }

        // Public methods -----------------------------------------------------

        public ByteContainer(int bucketSize)
        {
            this.bufferPool = new BufferPool<byte>();
            this.bucketSize = bucketSize;
        }

        public ByteContainer(int bucketSize, BufferPool<byte> bufferPool)
        {
            this.bucketSize = bucketSize;
            this.bufferPool = bufferPool;
        }

        public ByteContainer(Stream stream, int bucketSize)
        {
            this.bucketSize = bucketSize;
            bufferPool = new BufferPool<byte>();
            LoadFromStream(stream);
        }

        public ByteContainer(Stream stream, int bucketSize, BufferPool<byte> bufferPool)
        {
            this.bucketSize = bucketSize;
            this.bufferPool = bufferPool;
            LoadFromStream(stream);
        }

        public virtual int GetAvailableBytes(int offset, int length, byte[] buffer, int bufferOffset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0 || bufferOffset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (bufferOffset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                return 0;

            int remainingLength = length;

            while (bucketIndex < buckets.Count && remainingLength > 0)
            {
                buckets[bucketIndex].ContinueGettingBytes(bucketOffset, buffer, ref bufferOffset, ref remainingLength);
                bucketIndex++;
                bucketOffset = 0;
            }

            return length - remainingLength;
        }

        public virtual byte GetByte(int offset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (bucketOffset < 0 || bucketOffset >= buckets[bucketIndex].Size)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return buckets[bucketIndex].GetByte(bucketOffset);
        }

        public virtual void Remove(int offset, RemoveMode mode)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Remove, offset, 1));

            try
            {
                if (mode == RemoveMode.Backspace)
                {
                    if (bucketOffset < buckets[bucketIndex].Size - 1)
                    {
                        var bucket = buckets[bucketIndex].Split(bucketOffset + 1, bucketSize);
                        buckets.Insert(bucketIndex + 1, bucket);

                        // Change ocurred after the current bucketIndex + bucketOffset,
                        // so there's no need to evaluate new indices
                    }
                }

                buckets[bucketIndex].Remove(bucketOffset, 1);
                if (buckets[bucketIndex].Size == 0)
                {
                    buckets[bucketIndex].Dispose();
                    buckets.RemoveAt(bucketIndex);
                }

                cachedSize = null;
            }
            finally
            {
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Remove, offset, 1));
            }
        }

        public virtual void Remove(int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Remove, offset, length));

            try
            {
                int remainingLength = length;

                while (bucketIndex < buckets.Count && remainingLength > 0)
                {
                    buckets[bucketIndex].ContinueRemoving(bucketOffset, ref remainingLength);

                    bucketOffset = 0;

                    if (buckets[bucketIndex].Size == 0)
                    {
                        buckets[bucketIndex].Dispose();
                        buckets.RemoveAt(bucketIndex);
                    }
                    else
                    {
                        bucketIndex++;
                    }
                }

                cachedSize = null;
            }
            finally
            {
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Remove, offset, length));
            }
        }

        public virtual void Replace(int offset, byte target)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex >= buckets.Count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Replace, offset, 1));
            try
            {
                buckets[bucketIndex].Replace(bucketOffset, target);
            }
            finally
            {
                // Replace doesn't change size, no need for size cache invalidation
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Replace, offset, 1));
            }
        }

        public virtual void Replace(int offset, byte[] target, int targetOffset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetOffset < 0 || targetOffset >= target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset));
            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || (bucketIndex > buckets.Count) || (bucketIndex == buckets.Count && bucketOffset > 0))
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Replace, offset, length));

            try
            {
                int remainingLength = length;

                // Replace bytes as long as posible
                while (bucketIndex < buckets.Count && remainingLength > 0)
                {
                    buckets[bucketIndex].ContinueReplacing(bucketOffset, target, ref targetOffset, ref remainingLength);
                    bucketIndex++;
                    bucketOffset = 0;
                }

                // If there are any bytes remaining, append them as additional buckets
                while (remainingLength > 0)
                {
                    var bucket = new ByteBucket(bucketSize, bufferPool);
                    buckets.Add(bucket);

                    bucket.ContinueAppending(target, ref targetOffset, ref remainingLength);
                }
            }
            finally
            {
                // Replace doesn't change size, no need for size cache invalidation
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Remove, offset, length));
            }
        }

        public virtual void Insert(int offset, byte target)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex > buckets.Count || (bucketIndex == buckets.Count && bucketOffset > 0))
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Insert, offset, 1));

            try
            {
                ProcessBucketsForInserting(offset, ref bucketIndex, ref bucketOffset);

                // If we're at the beginning, adding new bucket
                if (bucketIndex == 0)
                {
                    var newBucket = new ByteBucket(bucketSize, bufferPool);
                    buckets.Insert(0, newBucket);
                    newBucket.Append(target);
                }
                else
                {
                    bucketIndex--;

                    // Can we append to previous bucket?
                    if (buckets[bucketIndex].Size < buckets[bucketIndex].Capacity)
                    {
                        buckets[bucketIndex].Append(target);
                    }
                    else
                    {
                        var newBucket = new ByteBucket(bucketSize, bufferPool);
                        buckets.Insert(bucketIndex + 1, newBucket);

                        newBucket.Append(target);
                    }
                }

                cachedSize = null;
            }
            finally
            {
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Insert, offset, 1));
            }
        }

        public virtual void Insert(int offset, byte[] target, int targetOffset, int targetLength)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            (int bucketIndex, int bucketOffset) = GetBucketFromOffset(offset);
            if (bucketIndex < 0 || bucketIndex > buckets.Count || (bucketIndex == buckets.Count && bucketOffset > 0))
                throw new ArgumentOutOfRangeException(nameof(offset));

            BeforeChange?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Insert, offset, targetLength));

            try
            {
                ProcessBucketsForInserting(offset, ref bucketIndex, ref bucketOffset);

                int index = targetOffset;
                int remainingLength = targetLength;

                if (bucketIndex > 0 && bucketOffset > 0)
                {
                    buckets[bucketIndex].ContinueAppending(target, ref index, ref remainingLength);
                    bucketIndex++;
                }

                while (remainingLength > 0)
                {
                    var bucket = new ByteBucket(bucketSize, bufferPool);
                    buckets.Insert(bucketIndex, bucket);

                    bucket.ContinueAppending(target, ref index, ref remainingLength);
                    bucketIndex++;
                }

                cachedSize = null;
            }
            finally
            {
                Changed?.Invoke(this, new DataChangeEventArgs(ByteBufferChange.Insert, offset, targetLength));
            }            
        }

        public virtual void Clear()
        {
            foreach (var bucket in buckets)
                bucket.Dispose();

            buckets.Clear();
            cachedSize = null;
        }

        public void LoadFromStream(Stream stream)
        {
            if (stream.CanSeek)
            {
                bufferPool.Preallocate(bucketSize, (int)((stream.Length - stream.Position + bucketSize - 1) / bucketSize));

                Clear();

                while (stream.Position < stream.Length)
                    buckets.Add(new ByteBucket(stream, Math.Min(bucketSize, (int)stream.Length - (int)stream.Position), bucketSize, bufferPool));
            }
            else
            {
                bufferPool.Preallocate(bucketSize, 1);

                Clear();

                var buffer = new byte[bucketSize];
                int bytesRead;

                do
                {
                    bytesRead = stream.Read(buffer, 0, bucketSize);
                    if (bytesRead > 0)
                        buckets.Add(new ByteBucket(buffer, 0, bytesRead, bucketSize, bufferPool));
                }
                while (bytesRead > 0);
            }
        }

        public void SaveToStream(Stream stream)
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                buckets[i].WriteToStream(stream);
            }
        }

        public void Dispose()
        {
            foreach (var bucket in buckets)
                bucket.Dispose();
            buckets.Clear();
        }

        // Public properties --------------------------------------------------

        public int Size
        {
            get
            {
                if (cachedSize != null)
                    return cachedSize.Value;

                cachedSize = buckets.Sum(b => b.Size);
                return cachedSize.Value;
            }
        }

        /// <remarks>
        /// Contracts:
        /// Changed is called _always after_ matching BeforeChange
        /// BeforeChanged and Changed _are always called_ in pair for matching event
        /// BeforeChanged and Changed _are not nested_
        /// </remarks>
        public event DataChangeEventHandler Changed;

        public event DataChangeEventHandler BeforeChange;
    }
}
