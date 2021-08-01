using System;
using System.Collections.Generic;
using System.IO;

namespace ET
{
    /// <summary>
    /// 环形缓存区
    /// </summary>
    public class CircularBuffer: Stream
    {
        public int ChunkSize = 8192;

        //
        private readonly Queue<byte[]> _bufferQueue = new Queue<byte[]>();

        //
        private readonly Queue<byte[]> _bufferCache = new Queue<byte[]>();

        public int LastIndex { get; set; }

        public int FirstIndex { get; set; }

        private byte[] _lastBuffer;

        public CircularBuffer()
        {
            AddLast();
        }

        public override long Length
        {
            get
            {
                int c = 0;
                if (_bufferQueue.Count == 0)
                {
                    c = 0;
                }
                else
                {
                    c = (_bufferQueue.Count - 1) * ChunkSize + LastIndex - FirstIndex;
                }

                if (c < 0)
                {
                    Log.Error("CircularBuffer count < 0: {0}, {1}, {2}".Fmt(_bufferQueue.Count, LastIndex, FirstIndex));
                }

                return c;
            }
        }

        public void AddLast()
        {
            byte[] buffer;
            if (_bufferCache.Count > 0)
            {
                buffer = _bufferCache.Dequeue();
            }
            else
            {
                buffer = new byte[ChunkSize];
            }

            _bufferQueue.Enqueue(buffer);
            _lastBuffer = buffer;
        }

        public void RemoveFirst()
        {
            _bufferCache.Enqueue(_bufferQueue.Dequeue());
        }

        public byte[] First
        {
            get
            {
                if (_bufferQueue.Count == 0)
                {
                    AddLast();
                }

                return _bufferQueue.Peek();
            }
        }

        public byte[] Last
        {
            get
            {
                if (_bufferQueue.Count == 0)
                {
                    AddLast();
                }

                return _lastBuffer;
            }
        }

        /// <summary>
        /// 从CircularBuffer读到stream中
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        //public async ETTask ReadAsync(Stream stream)
        //{
        //    long buffLength = Length;
        //	int sendSize = ChunkSize - FirstIndex;
        //    if (sendSize > buffLength)
        //    {
        //	    sendSize = (int)buffLength;
        //    }
        //	
        //    await stream.WriteAsync(First, FirstIndex, sendSize);
        //    
        //    FirstIndex += sendSize;
        //    if (FirstIndex == ChunkSize)
        //    {
        //	    FirstIndex = 0;
        //	    RemoveFirst();
        //    }
        //}

        // 从CircularBuffer读到stream
        public void Read(Stream stream, int count)
        {
            if (count > Length)
            {
                throw new Exception($"bufferList length < count, {Length} {count}");
            }

            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                int n = count - alreadyCopyCount;
                if (ChunkSize - FirstIndex > n)
                {
                    stream.Write(First, FirstIndex, n);
                    FirstIndex += n;
                    alreadyCopyCount += n;
                }
                else
                {
                    stream.Write(First, FirstIndex, ChunkSize - FirstIndex);
                    alreadyCopyCount += ChunkSize - FirstIndex;
                    FirstIndex = 0;
                    RemoveFirst();
                }
            }
        }

        // 从stream写入CircularBuffer
        public void Write(Stream stream)
        {
            int count = (int)(stream.Length - stream.Position);

            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                if (LastIndex == ChunkSize)
                {
                    AddLast();
                    LastIndex = 0;
                }

                int n = count - alreadyCopyCount;
                if (ChunkSize - LastIndex > n)
                {
                    stream.Read(_lastBuffer, LastIndex, n);
                    LastIndex += count - alreadyCopyCount;
                    alreadyCopyCount += n;
                }
                else
                {
                    stream.Read(_lastBuffer, LastIndex, ChunkSize - LastIndex);
                    alreadyCopyCount += ChunkSize - LastIndex;
                    LastIndex = ChunkSize;
                }
            }
        }

        /// <summary>
        ///  从stream写入CircularBuffer
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        //public async ETTask<int> WriteAsync(Stream stream)
        //{
        //    int size = ChunkSize - LastIndex;
        //    
        //    int n = await stream.ReadAsync(Last, LastIndex, size);
        //
        //    if (n == 0)
        //    {
        //	    return 0;
        //    }
        //
        //    LastIndex += n;
        //
        //    if (LastIndex == ChunkSize)
        //    {
        //	    AddLast();
        //	    LastIndex = 0;
        //    }
        //
        //    return n;
        //}

        // 把CircularBuffer中数据写入buffer
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer.Length < offset + count)
            {
                throw new Exception($"bufferList length < coutn, buffer length: {buffer.Length} {offset} {count}");
            }

            long length = Length;
            if (length < count)
            {
                count = (int)length;
            }

            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                int n = count - alreadyCopyCount;
                if (ChunkSize - FirstIndex > n)
                {
                    Array.Copy(First, FirstIndex, buffer, alreadyCopyCount + offset, n);
                    FirstIndex += n;
                    alreadyCopyCount += n;
                }
                else
                {
                    Array.Copy(First, FirstIndex, buffer, alreadyCopyCount + offset, ChunkSize - FirstIndex);
                    alreadyCopyCount += ChunkSize - FirstIndex;
                    FirstIndex = 0;
                    RemoveFirst();
                }
            }

            return count;
        }

        // 把buffer写入CircularBuffer中
        public override void Write(byte[] buffer, int offset, int count)
        {
            int alreadyCopyCount = 0;
            while (alreadyCopyCount < count)
            {
                if (LastIndex == ChunkSize)
                {
                    AddLast();
                    LastIndex = 0;
                }

                int n = count - alreadyCopyCount;
                if (ChunkSize - LastIndex > n)
                {
                    Array.Copy(buffer, alreadyCopyCount + offset, _lastBuffer, LastIndex, n);
                    LastIndex += count - alreadyCopyCount;
                    alreadyCopyCount += n;
                }
                else
                {
                    Array.Copy(buffer, alreadyCopyCount + offset, _lastBuffer, LastIndex, ChunkSize - LastIndex);
                    alreadyCopyCount += ChunkSize - LastIndex;
                    LastIndex = ChunkSize;
                }
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Position { get; set; }
    }
}