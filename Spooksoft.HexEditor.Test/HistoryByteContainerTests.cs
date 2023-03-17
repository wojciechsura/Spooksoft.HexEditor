using HexEditor.Infrastructure;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Test
{
    [TestFixture]
    public class HistoryByteContainerTests
    {
        private HistoryByteContainer CreateFrom(byte[] array, int bucketSize)
        {
            MemoryStream ms = new MemoryStream(array);
            ms.Seek(0, SeekOrigin.Begin);
            return new HistoryByteContainer(ms, bucketSize);
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
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, new byte[] { 1, 2, 3 })]
        public void UndoInsertTest(byte[] initialData, int bucketSize, int insertOffset, byte[] insertData)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Insert(insertOffset, insertData, 0, insertData.Length);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 7, 8, 9, 10 }, 5, 2, new byte[] { 3, 4, 5, 6 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        public void RedoInsertTest(byte[] initialData, int bucketSize, int insertOffset, byte[] insertData, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Insert(insertOffset, insertData, 0, insertData.Length);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, 20)]
        public void UndoInsertByteTest(byte[] initialData, int bucketSize, int insertOffset, byte insertData)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Insert(insertOffset, insertData);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 4, 5, 6, 7, 8, 9, 10 }, 5, 2, 3, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        public void RedoInsertByteTest(byte[] initialData, int bucketSize, int insertOffset, byte insertData, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Insert(insertOffset, insertData);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }


        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, new byte[] { 1, 2, 3 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 9, new byte[] { 1, 2, 3 })]
        public void UndoReplaceTest(byte[] initialData, int bucketSize, int replaceOffset, byte[] replaceData)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Replace(replaceOffset, replaceData, 0, replaceData.Length);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, new byte[] { 3, 4, 5, 6 }, new byte[] { 3, 4, 5, 6, 5, 6, 7, 8, 9, 10 })]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 9, new byte[] { 3, 4, 5, 6 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 3, 4, 5, 6 })]
        public void RedoReplaceTest(byte[] initialData, int bucketSize, int insertOffset, byte[] insertData, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Replace(insertOffset, insertData, 0, insertData.Length);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, 20)]
        public void UndoReplaceByteTest(byte[] initialData, int bucketSize, int replaceOffset, byte replaceData)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Replace(replaceOffset, replaceData);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 0, 20, new byte[] { 20, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]
        public void RedoReplaceByteTest(byte[] initialData, int bucketSize, int insertOffset, byte insertData, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Replace(insertOffset, insertData);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, 6)]
        public void UndoRemoveTest(byte[] initialData, int bucketSize, int removeOffset, int removeLength)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Remove(removeOffset, removeLength);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, 6, new byte[] { 1, 2, 3, 10 })]
        public void RedoRemoveTest(byte[] initialData, int bucketSize, int removeOffset, int removeLength, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Remove(removeOffset, removeLength);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3)]
        public void UndoRemoveTest(byte[] initialData, int bucketSize, int removeOffset)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Remove(removeOffset, RemoveMode.Delete);
            container.Undo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, initialData));
        }

        [Test]
        [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 3, new byte[] { 1, 2, 3, 5, 6, 7, 8, 9, 10 })]
        public void RedoRemoveTest(byte[] initialData, int bucketSize, int removeOffset, byte[] expectedResult)
        {
            // Arrange
            var container = CreateFrom(initialData, bucketSize);

            // Act
            container.Remove(removeOffset, RemoveMode.Delete);
            container.Undo();
            container.Redo();

            // Assert
            byte[] data = new byte[container.Size];
            container.GetAvailableBytes(0, container.Size, data, 0);

            Assert.IsTrue(ArraysEqual(data, expectedResult));
        }
    }
}
