using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spooksoft.HexEditor.Test.Utils
{
    public class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream stream;

        public NonSeekableStreamWrapper(Stream source)
        {
            stream = source;
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Stream is not seekable");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Stream is read-only!");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Stream is read-only!");
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException("Stream is not seekable, cannot retrieve length");

        public override long Position 
        { 
            get => throw new NotSupportedException("Stream is not seekable");
            set => throw new NotSupportedException("Stream is not seekable");
        }
    }
}
