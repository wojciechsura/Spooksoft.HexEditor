using Spooksoft.HexEditor.Infrastructure;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Test
{
    [TestFixture]
    public class ByteContainerTests
    {
        private ByteContainer CreateFrom(byte[] array, int bucketSize)
        {
            MemoryStream ms = new MemoryStream(array);
            ms.Seek(0, SeekOrigin.Begin);
            return new ByteContainer(ms, bucketSize);
        }

        private bool ArraysEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return false;
            return true;
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 12)]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10)]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5)]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 1)]
        public void ReadingFromStream(byte[] data, int bucketSize)
        {
            // Arrange

            // Act
            var container = CreateFrom(data, bucketSize);

            // Assert
            Assert.AreEqual(data.Length, container.Size);

            byte[] result = new byte[container.Size];
            container.GetAvailableBytes(0, data.Length, result, 0);

            Assert.IsTrue(ArraysEqual(data, result));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 10, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 1, new byte[] { 1 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 5, new byte[] { 1, 2, 3, 4, 5 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 5, 5, new byte[] { 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 4, 2, new byte[] { 5, 6 })]
        public void GetAvailableBytesTest(byte[] data, int bucketSize, int offset, int length, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            byte[] buffer = new byte[length];
            container.GetAvailableBytes(offset, length, buffer, 0);

            // Assert
            Assert.IsTrue(ArraysEqual(expectedResult, buffer));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 2, RemoveMode.Delete, new byte[] { 1, 2, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, RemoveMode.Delete, new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 9, RemoveMode.Delete, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 2, RemoveMode.Backspace, new byte[] { 1, 2, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, RemoveMode.Backspace, new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 9, RemoveMode.Backspace, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        public void RemoveByteTest(byte[] data, int bucketSize, int offset, RemoveMode mode, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Remove(offset, mode);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }


        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 2, 1, new byte[] { 1, 2, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 5, new byte[] { 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 4, 2, new byte[] { 1, 2, 3, 4, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 3, 2, 5, new byte[] { 1, 2, 8, 9, 10 })]
        public void RemoveByteRangeTest(byte[] data, int bucketSize, int offset, int length, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Remove(offset, length);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 20, new byte[] { 20, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 4, 20, new byte[] { 1, 2, 3, 4, 20, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 9, 20, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 20 })]
        public void ReplaceByteTest(byte[] data, int bucketSize, int offset, byte newValue, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Replace(offset, newValue);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 1, new byte[] { 22, 22 }, new byte[] { 1, 22, 22, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 1, new byte[] { 22, 22, 22, 22, 22, 22, 22, 22 }, new byte[] { 1, 22, 22, 22, 22, 22, 22, 22, 22, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, new byte[] { 22, 22, 22, 22, 22 }, new byte[] { 22, 22, 22, 22, 22, 6, 7, 8, 9, 10 })]
        public void ReplaceRangeTest(byte[] data, int bucketSize, int offset, byte[] replace, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Replace(offset, replace, 0, replace.Length);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 99, new byte[] { 99, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 4, 99, new byte[] { 1, 2, 3, 4, 99, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 10, 99, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99 })]
        public void InsertByteTest(byte[] data, int bucketSize, int offset, byte target, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Insert(offset, target);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, new byte[] { 99, 88 }, new byte[] { 99, 88, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 4, new byte[] { 99, 88 }, new byte[] { 1, 2, 3, 4, 99, 88, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 10, new byte[] { 99, 88 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99, 88 })]
        public void InsertArrayTest(byte[] data, int bucketSize, int offset, byte[] target, byte[] expected)
        {
            // Arrange
            var container = CreateFrom(data, bucketSize);

            // Act
            container.Insert(offset, target, 0, target.Length);

            // Assert
            byte[] buffer = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, buffer, 0);
            Assert.IsTrue(ArraysEqual(expected, buffer));
        }
    }
}
