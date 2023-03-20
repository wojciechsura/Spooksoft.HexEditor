using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Infrastructure
{
    public class HistoryByteContainer : ByteContainer
    {
        // Private types ------------------------------------------------------

        private class HistoryAction
        {
            public HistoryAction(ByteBufferChange change, int offset, int length, byte[] data, HistoryAction continueWith)
            {
                Change = change;
                Offset = offset;
                Length = length;
                Data = data;
                ContinueWith = continueWith;
            }

            public ByteBufferChange Change { get; }
            public int Offset { get; }
            public int Length { get; }
            public byte[] Data { get; }
            public HistoryAction ContinueWith { get; }
        }

        private class HistoryEntry
        {
            public HistoryEntry(HistoryAction undoAction, HistoryAction redoAction)
            {
                UndoAction = undoAction;
                RedoAction = redoAction;
            }

            public HistoryAction UndoAction { get; }
            public HistoryAction RedoAction { get; }
        }

        // Private fields -----------------------------------------------------

        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private int currentEntry;
        private int maxHistoryEntries = 64;

        // Private methods ----------------------------------------------------

        private void AddHistoryEntry(HistoryEntry entry)
        {
            // TODO take into account current position and clear history after
            // TODO limit history

            // If not at the end of the history (user undoed actions),
            // discard the undone actions
            if (currentEntry < history.Count)
                history.RemoveRange(currentEntry, history.Count - currentEntry);

            // Limit history entries
            if (history.Count >= maxHistoryEntries)
                history.RemoveRange(0, history.Count - maxHistoryEntries + 1);

            history.Add(entry);
            currentEntry++;

            UndoParamsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Execute(HistoryAction action)
        {
            switch (action.Change)
            {
                case ByteBufferChange.Insert:
                    {
                        base.Insert(action.Offset, action.Data, 0, action.Length);
                        break;
                    }
                case ByteBufferChange.Replace:
                    {
                        base.Replace(action.Offset, action.Data, 0, action.Length);
                        break;
                    }
                case ByteBufferChange.Remove:
                    {
                        base.Remove(action.Offset, action.Length);
                        break;
                    }
                default:
                    throw new InvalidEnumArgumentException("Unsupported byte buffer change");
            }

            if (action.ContinueWith != null)
                Execute(action.ContinueWith);
        }

        // Public methods -----------------------------------------------------

        public HistoryByteContainer(int bucketSize) : base(bucketSize)
        {
            currentEntry = 0;
        }

        public HistoryByteContainer(int bucketSize, BufferPool<byte> bufferPool) : base(bucketSize, bufferPool)
        {
            currentEntry = 0;
        }

        public HistoryByteContainer(Stream stream, int bucketSize) : base(stream, bucketSize)
        {
            currentEntry = 0;
        }

        public HistoryByteContainer(Stream stream, int bucketSize, BufferPool<byte> bufferPool) : base(stream, bucketSize, bufferPool)
        {
            currentEntry = 0;
        }

        public override void Insert(int offset, byte target)
        {
            var undoAction = new HistoryAction(ByteBufferChange.Remove, offset, 1, null, null);

            base.Insert(offset, target);

            var redoAction = new HistoryAction(ByteBufferChange.Insert, offset, 1, new byte[] { target }, null);

            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Insert(int offset, byte[] target, int targetOffset, int targetLength)
        {
            var undoAction = new HistoryAction(ByteBufferChange.Remove, offset, targetLength, null, null);

            base.Insert(offset, target, targetOffset, targetLength);

            byte[] data = new byte[targetLength];
            Array.Copy(target, targetOffset, data, 0, targetLength);

            var redoAction = new HistoryAction(ByteBufferChange.Insert, offset, targetLength, data, null);
            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Remove(int offset, int length)
        {
            int availableBytes = CountAvailableBytes(offset, length);
            byte[] data = new byte[availableBytes];
            base.GetAvailableBytes(offset, availableBytes, data, 0);
            var undoAction = new HistoryAction(ByteBufferChange.Insert, offset, availableBytes, data, null);

            base.Remove(offset, length);

            var redoAction = new HistoryAction(ByteBufferChange.Remove, offset, length, null, null);
            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Remove(int offset, RemoveMode mode)
        {
            int availableBytes = CountAvailableBytes(offset, 1);
            byte[] data = new byte[availableBytes];
            base.GetAvailableBytes(offset, availableBytes, data, 0);
            var undoAction = new HistoryAction(ByteBufferChange.Insert, offset, availableBytes, data, null);

            base.Remove(offset, mode);

            var redoAction = new HistoryAction(ByteBufferChange.Remove, offset, 1, null, null);
            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Replace(int offset, byte target)
        {
            int availableBytes = CountAvailableBytes(offset, 1);
            byte[] data = new byte[availableBytes];
            base.GetAvailableBytes(offset, availableBytes, data, 0);
            var undoAction = new HistoryAction(ByteBufferChange.Replace, offset, 1, data, null);

            base.Replace(offset, target);

            var redoAction = new HistoryAction(ByteBufferChange.Replace, offset, 1, new[] { target }, null);
            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Replace(int offset, byte[] target, int targetOffset, int length)
        {
            int availableBytes = CountAvailableBytes(offset, length);
            HistoryAction undoAction;

            byte[] newData = new byte[length];
            Array.Copy(target, targetOffset, newData, 0, length);

            byte[] originalData = new byte[availableBytes];
            GetAvailableBytes(offset, availableBytes, originalData, 0);

            if (availableBytes == length)
            {
                undoAction = new HistoryAction(ByteBufferChange.Replace, offset, availableBytes, originalData, null);
            }
            else
            {
                var insertAction = new HistoryAction(ByteBufferChange.Insert, offset, availableBytes, originalData, null);
                undoAction = new HistoryAction(ByteBufferChange.Remove, offset, length, null, insertAction);
            }

            base.Replace(offset, target, targetOffset, length);

            var redoAction = new HistoryAction(ByteBufferChange.Replace, offset, length, newData, null);
            var entry = new HistoryEntry(undoAction, redoAction);
            AddHistoryEntry(entry);
        }

        public override void Clear()
        {
            base.Clear();
            ClearUndoHistory();
        }

        public void ClearUndoHistory()
        {
            history.Clear();
            currentEntry = 0;
        }

        public void Undo()
        {
            if (!CanUndo)
                throw new InvalidOperationException("Cannot undo!");

            currentEntry--;

            Execute(history[currentEntry].UndoAction);

            UndoParamsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo)
                throw new InvalidOperationException("Cannot redo!");

            Execute(history[currentEntry].RedoAction);

            currentEntry++;

            UndoParamsChanged?.Invoke(this, EventArgs.Empty);
        }

        // Public properties --------------------------------------------------

        public event EventHandler UndoParamsChanged;

        public bool CanUndo => currentEntry > 0;

        public bool CanRedo => currentEntry < history.Count;
    }
}
