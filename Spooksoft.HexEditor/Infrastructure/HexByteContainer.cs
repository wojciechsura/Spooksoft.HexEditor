using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditor.Infrastructure
{
    public class HexByteContainer : HistoryByteContainer
    {
        // Private constants --------------------------------------------------

        private const int DEFAULT_BUCKET_SIZE = 10240;

        // Private fields -----------------------------------------------------

        private byte bytesPerRow = 16;

        // Private methods ----------------------------------------------------

        private void SetBytesPerRow(byte value)
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(BytesPerRow));

            if (value != bytesPerRow)
            {
                bytesPerRow = value;
                BytesPerRowChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Public methods -----------------------------------------------------

        public HexByteContainer(int bucketSize = DEFAULT_BUCKET_SIZE) : base(bucketSize)
        {

        }

        public HexByteContainer(BufferPool<byte> bufferPool, int bucketSize = DEFAULT_BUCKET_SIZE) : base(bucketSize, bufferPool)
        {

        }

        public HexByteContainer(Stream stream, int bucketSize = DEFAULT_BUCKET_SIZE) : base(stream, bucketSize)
        {

        }

        public HexByteContainer(Stream stream, BufferPool<byte> bufferPool, int bucketSize = DEFAULT_BUCKET_SIZE) : base(stream, bucketSize, bufferPool)
        {

        }

        // Public properties --------------------------------------------------

        public byte BytesPerRow
        {
            get => bytesPerRow;
            set
            {
                SetBytesPerRow(value);
            }
        }

        public event EventHandler BytesPerRowChanged;
    }
}
