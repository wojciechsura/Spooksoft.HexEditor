using System;
using System.IO;
using System.Linq;
using Spooksoft.HexEditor.Infrastructure;
using Spooksoft.HexEditor.Test.Mocks;
using Spooksoft.HexEditor.Test.Utils;
using NUnit.Framework;

namespace Spooksoft.HexEditor.Test
{
    [TestFixture]
    public class ByteBucketTests
    {
        private ByteBucket CreateFrom(byte[] array, int capacity, IBufferPool<byte> bufferPool)
        {
            MemoryStream ms = new MemoryStream(array);
            ms.Seek(0, SeekOrigin.Begin);
            return new ByteBucket(ms, array.Length, capacity, bufferPool);
        }

        private void VerifyContents(ByteBucket bucket, byte[] expectedData)
        {
            Assert.AreEqual(expectedData.Length, bucket.Size);

            if (expectedData.Length > 0)
            {
                byte[] result = new byte[bucket.Size];
                bucket.GetBytes(result, 0, 0, bucket.Size);

                for (int i = 0; i < result.Length; i++)
                    Assert.AreEqual(expectedData[i], result[i]);
            }
        }

        [Test]
        public void CreateFromStreamTest()
        {
            // Arrange

            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            MemoryStream ms = new MemoryStream(data);
            ms.Seek(0, SeekOrigin.Begin);

            // Act

            ByteBucket byteBucket = new ByteBucket(ms, 10, 10, new BufferPoolMock<byte>());

            // Assert

            VerifyContents(byteBucket, data);
        }

        [Test]
        public void CreateFromNotSeekableStreamTest()
        {
            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            MemoryStream ms = new MemoryStream(data);
            ms.Seek(0, SeekOrigin.Begin);

            var wrapper = new NonSeekableStreamWrapper(ms);

            // Act

            ByteBucket byteBucket = new ByteBucket(ms, 10, 10, new BufferPoolMock<byte>());

            // Assert

            VerifyContents(byteBucket, data);
        }

        [Test]
        public void CreateFromByteDataTest()
        {
            byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // Act

            ByteBucket byteBucket = new ByteBucket(data, 0, 10, 10, new BufferPoolMock<byte>());

            // Assert

            VerifyContents(byteBucket, data);
        }

        [Test]
        public void CreateEmptyTest()
        {
            // Arrange

            // Act

            ByteBucket byteBucket = new ByteBucket(10, new BufferPoolMock<byte>());

            Assert.AreEqual(10, byteBucket.Capacity);
            Assert.AreEqual(0, byteBucket.Size);
        }

        [Test]
        public void ContinueReplacingTest()
        {
            // Arrange

            var poolMock = new BufferPoolMock<byte>();

            var buckets = Enumerable.Range(0, 3)
                .Select(i => CreateFrom(new byte[] { 1, 1, 1, 1, 1 }, 10, poolMock))
                .ToList();

            var replace = new byte[] { 2, 2, 2, 2, 2, 2, 2 };

            // Act

            int index = 0;
            int length = 7;
            buckets[0].ContinueReplacing(4, replace, ref index, ref length);
            buckets[1].ContinueReplacing(0, replace, ref index, ref length);
            buckets[2].ContinueReplacing(0, replace, ref index, ref length);

            // Assert

            Assert.AreEqual(0, length);
            Assert.AreEqual(7, index);
            VerifyContents(buckets[0], new byte[] { 1, 1, 1, 1, 2 });
            VerifyContents(buckets[1], new byte[] { 2, 2, 2, 2, 2 });
            VerifyContents(buckets[2], new byte[] { 2, 1, 1, 1, 1 });
        }

        [Test]
        public void ContinueGettingBytesTest()
        {
            // Arrange

            var bufferPool = new BufferPoolMock<byte>();

            var buckets = Enumerable.Range(0, 3)
                .Select(i => Enumerable.Range(i * 3, 3).Select(j => (byte)j).ToArray())
                .Select(a => CreateFrom(a, a.Length * 2, bufferPool))
                .ToList();

            // Act

            int length = 5;
            int offset = 0;
            byte[] result = new byte[length];
            int[] readBytes = new int[3];

            readBytes[0] = buckets[0].ContinueGettingBytes(2, result, ref offset, ref length);
            readBytes[1] = buckets[1].ContinueGettingBytes(0, result, ref offset, ref length);
            readBytes[2] = buckets[2].ContinueGettingBytes(0, result, ref offset, ref length);

            // Assert

            Assert.AreEqual(1, readBytes[0]);
            Assert.AreEqual(3, readBytes[1]);
            Assert.AreEqual(1, readBytes[2]);

            byte[] expected = new byte[] { 2, 3, 4, 5, 6 };
            for (int i = 0; i < result.Length; i++)
                Assert.AreEqual(expected[i], result[i]);

            Assert.AreEqual(length, 0);
            Assert.AreEqual(offset, 5);
        }

        [Test]
        [TestCase(10, 20, 20)]
        [TestCase(20, 10, 20)]
        public void EnsureCapacityTest1(byte start, byte change, byte test)
        {
            // Arrange

            ByteBucket byteBucket = new ByteBucket(start, new BufferPoolMock<byte>());

            // Act

            byteBucket.EnsureCapacity(change);

            // Assert

            Assert.AreEqual(test, byteBucket.Capacity);
        }

        [Test]
        public void GetBytesTest()
        {
            // Arrange

            var data = new byte[] { 1, 2, 3, 4, 5 };

            ByteBucket byteBucket = CreateFrom(data, 5, new BufferPoolMock<byte>());

            // Act

            var result = new byte[byteBucket.Size];
            byteBucket.GetBytes(result, 0, 0, 5);

            // Assert

            Assert.AreEqual(result.Length, data.Length);
            for (int i = 0; i < result.Length; i++)
                Assert.AreEqual(result[i], data[i]);
        }

        [Test]
        public void JoinTest()
        {
            // Arrange

            var bufferPool = new BufferPoolMock<byte>();

            var bucket1 = CreateFrom(new byte[] { 1, 2, 3, 4, 5 }, 5, bufferPool);
            var bucket2 = CreateFrom(new byte[] { 6, 7, 8, 9, 10 }, 5, bufferPool);

            // Act

            bucket1.EnsureCapacity(bucket1.Size + bucket2.Size);
            bucket1.Join(bucket2);

            // Assert

            VerifyContents(bucket1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        }

        [Test]
        [TestCase(new byte[] { 1, 1, 1, 1, 1, 1 }, 10, new byte[] { 2, 2, 2, 2 }, 1, 0, 4, new byte[] { 1, 2, 2, 2, 2, 1 })]
        [TestCase(new byte[] { 1, 1, 1, 1, 1, 1 }, 6, new byte[] { 0, 0, 2, 2, 2, 2, 2, 2, 0, 0}, 0, 2, 6, new byte[] { 2, 2, 2, 2, 2, 2 })]
        public void ReplaceTest(byte[] data, int capacity, byte[] replace, int offset, int targetOffset, int length, byte[] expectedResult)
        {
            // Arrange
            
            var byteBucket = CreateFrom(data, capacity, new BufferPoolMock<byte>());

            // Act

            byteBucket.Replace(offset, replace, targetOffset, length);

            // Assert

            VerifyContents(byteBucket, expectedResult);
        }

        [Test]
        [TestCase(10, 20, 20)]
        [TestCase(20, 10, 10)]
        public void SetCapacityTest(int initialCapacity, int change, int expected)
        {
            // Arrange

            var byteBucket = new ByteBucket(initialCapacity, new BufferPoolMock<byte>());

            // Act

            byteBucket.SetCapacity(change);

            // Assert

            Assert.AreEqual(expected, byteBucket.Capacity);
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10, 5, 10, new byte[] { 1, 2, 3, 4, 5 }, new byte[] { 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10, 0, 10, new byte[] { }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        public void SplitTest(byte[] data, int capacity, int offset, int targetCapacity, byte[] expected1, byte[] expected2)
        {
            // Arrange

            var byteBucket = CreateFrom(data, capacity, new BufferPoolMock<byte>());

            // Act

            var byteBucket2 = byteBucket.Split(offset, targetCapacity);

            // Assert

            VerifyContents(byteBucket, expected1);
            VerifyContents(byteBucket2, expected2);

            Assert.AreEqual(capacity, byteBucket.Capacity);
            Assert.AreEqual(targetCapacity, byteBucket2.Capacity);
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3 }, 10, new byte[] { 4, 5, 6, 7, 8, 9, 10 }, 10, new byte[] { 1, 2, 3, 4, 5 }, new byte[] { 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, 10, new byte[] { 8, 9, 10 }, 10, new byte[] { 1, 2, 3, 4, 5 }, new byte[] { 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1 }, 5, new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10, new byte[] { 1, 2, 3, 4, 5 }, new byte[] { 6, 7, 8, 9, 10 })]
        public void BalanceTest(byte[] data1, int capacity1, byte[] data2, int capacity2, byte[] expected1, byte[] expected2)
        {
            // Arrange

            var bufferPool = new BufferPoolMock<byte>();

            var byteBucket1 = CreateFrom(data1, capacity1, bufferPool);
            var byteBucket2 = CreateFrom(data2, capacity2, bufferPool);

            // Act

            byteBucket1.Balance(byteBucket2);

            // Assert

            VerifyContents(byteBucket1, expected1);
            VerifyContents(byteBucket2, expected2);
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3 }, 10, 4, new byte[] { 1, 2, 3, 4 })]
        [TestCase(new byte[] { }, 10, 0, new byte[] { 0 })]
        public void AppendByteTest(byte[] data, int capacity, byte added, byte[] expected)
        {
            // Arrange
            var byteBucket = CreateFrom(data, capacity, new BufferPoolMock<byte>());

            // Act
            byteBucket.Append(added);

            // Assert
            VerifyContents(byteBucket, expected);
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3 }, 10, new byte[] { 4, 5, 6 }, 0, 3, new byte[] {  1, 2, 3, 4, 5, 6 })]
        [TestCase(new byte[] { 1, 2, 3 }, 10, new byte[] { 4, 5, 6 }, 1, 1, new byte[] { 1, 2, 3, 5 })]
        [TestCase(new byte[] { }, 10, new byte[] { 1, 2, 3, 4 }, 0, 4, new byte[] { 1, 2, 3, 4})]
        public void AppendArrayTest(byte[] data, int capacity, byte[] added, int offset, int length, byte[] expected)
        {
            // Arrange
            var byteBucket = CreateFrom(data, capacity, new BufferPoolMock<byte>());

            // Act
            byteBucket.Append(added, offset, length);

            // Assert
            VerifyContents(byteBucket, expected);
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6 }, 10, 2, 2, new byte[] { 1, 2, 5, 6 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6 }, 10, 0, 6, new byte[] { })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6 }, 10, 3, 3, new byte[] { 1, 2, 3 })]
        public void RemoveTest(byte[] data, int capacity, int offset, int length, byte[] expected)
        {
            // Arrange
            var byteBucket = CreateFrom(data, capacity, new BufferPoolMock<byte>());

            // Act
            byteBucket.Remove(offset, length);

            // Assert
            VerifyContents(byteBucket, expected);
        }

        [Test]
        public void ContinueRemovingTest()
        {
            // Arrange

            var poolMock = new BufferPoolMock<byte>();

            var buckets = Enumerable.Range(0, 3)
                .Select(i => CreateFrom(new byte[] { 1, 2, 3, 4, 5 }, 10, poolMock))
                .ToList();

            // Act

            int length = 7;
            buckets[0].ContinueRemoving(4, ref length);
            buckets[1].ContinueRemoving(0, ref length);
            buckets[2].ContinueRemoving(0, ref length);

            // Assert

            Assert.AreEqual(length, 0);
            VerifyContents(buckets[0], new byte[] { 1, 2, 3, 4 });
            VerifyContents(buckets[1], new byte[] { });
            VerifyContents(buckets[2], new byte[] { 2, 3, 4, 5 });
        }
    }
}
