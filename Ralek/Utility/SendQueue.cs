/*************************************************************************
 *
 *   file		: SendQueue.cs
 *   copyright		: (C) The WCell Team
 *   email		: info@wcell.org
 *   last changed	: $LastChangedDate: 2008-01-31 19:35:36 +0800 (Thu, 31 Jan 2008) $
 *   last author	: $LastChangedBy: tobz $
 *   revision		: $Rev: 87 $
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 *************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace WoWExtractor
{
    public class ByteQueue
    {
        private int m_Head;
        private int m_Tail;
        private int m_Size;

        private byte[] m_Buffer;

        public int Length { get { return m_Size; } }

        public ByteQueue()
        {
            m_Buffer = new byte[2048];
        }

        public void Clear()
        {
            m_Head = 0;
            m_Tail = 0;
            m_Size = 0;
        }

        private void SetCapacity(int capacity)
        {
            byte[] newBuffer = new byte[capacity];

            if (m_Size > 0)
            {
                if (m_Head < m_Tail)
                {
                    Buffer.BlockCopy(m_Buffer, m_Head, newBuffer, 0, m_Size);
                }
                else
                {
                    Buffer.BlockCopy(m_Buffer, m_Head, newBuffer, 0, m_Buffer.Length - m_Head);
                    Buffer.BlockCopy(m_Buffer, 0, newBuffer, m_Buffer.Length - m_Head, m_Tail);
                }
            }

            m_Head = 0;
            m_Tail = m_Size;
            m_Buffer = newBuffer;
        }

        public int Dequeue(byte[] buffer, int offset, int size)
        {
            if (size > m_Size)
                size = m_Size;

            if (size == 0)
                return 0;

            if (m_Head < m_Tail)
            {
                Buffer.BlockCopy(m_Buffer, m_Head, buffer, offset, size);
            }
            else
            {
                int rightLength = (m_Buffer.Length - m_Head);

                if (rightLength >= size)
                {
                    Buffer.BlockCopy(m_Buffer, m_Head, buffer, offset, size);
                }
                else
                {
                    Buffer.BlockCopy(m_Buffer, m_Head, buffer, offset, rightLength);
                    Buffer.BlockCopy(m_Buffer, 0, buffer, offset + rightLength, size - rightLength);
                }
            }

            m_Head = (m_Head + size) % m_Buffer.Length;
            m_Size -= size;

            if (m_Size == 0)
            {
                m_Head = 0;
                m_Tail = 0;
            }

            return size;
        }

        public void Enqueue(byte[] buffer, int offset, int size)
        {
            if ((m_Size + size) > m_Buffer.Length)
                SetCapacity((m_Size + size + 2047) & ~2047);

            if (m_Head < m_Tail)
            {
                int rightLength = (m_Buffer.Length - m_Tail);

                if (rightLength >= size)
                {
                    Buffer.BlockCopy(buffer, offset, m_Buffer, m_Tail, size);
                }
                else
                {
                    Buffer.BlockCopy(buffer, offset, m_Buffer, m_Tail, rightLength);
                    Buffer.BlockCopy(buffer, offset + rightLength, m_Buffer, 0, size - rightLength);
                }
            }
            else
            {
                Buffer.BlockCopy(buffer, offset, m_Buffer, m_Tail, size);
            }

            m_Tail = (m_Tail + size) % m_Buffer.Length;
            m_Size += size;
        }
    }

    public class SendQueue
    {
        public class Gram
        {
            private static Stack<Gram> _pool = new Stack<Gram>();

            public static Gram Acquire()
            {
                lock (_pool)
                {
                    Gram gram;

                    if (_pool.Count > 0)
                    {
                        gram = _pool.Pop();
                    }
                    else
                    {
                        gram = new Gram();
                    }

                    gram._buffer = AcquireBuffer();
                    gram._length = 0;

                    return gram;
                }
            }

            private byte[] _buffer;
            private int _length;

            public byte[] Buffer
            {
                get { return _buffer; }
            }

            public int Length
            {
                get { return _length; }
            }

            public int Available
            {
                get { return (_buffer.Length - _length); }
            }

            public bool IsFull
            {
                get { return (_length == _buffer.Length); }
            }

            private Gram() { }

            public int Write(byte[] buffer, int offset, int length)
            {
                int write = Math.Min(length, this.Available);

                System.Buffer.BlockCopy(buffer, offset, _buffer, _length, write);

                _length += write;

                return write;
            }

            public void Release()
            {
                lock (_pool)
                {
                    _pool.Push(this);
                    ReleaseBuffer(_buffer);
                }
            }
        }

        private static int s_CoalesceBufferSize = 512;
        private static BufferPool s_UnusedBuffers = new BufferPool("Coalesced", 2048, s_CoalesceBufferSize);

        public static int CoalesceBufferSize
        {
            get { return s_CoalesceBufferSize; }
            set
            {
                if (s_CoalesceBufferSize == value)
                    return;

                if (s_UnusedBuffers != null)
                    s_UnusedBuffers.Free();

                s_CoalesceBufferSize = value;
                s_UnusedBuffers = new BufferPool("Coalesced", 2048, s_CoalesceBufferSize);
            }
        }

        public static byte[] AcquireBuffer()
        {
            return s_UnusedBuffers.AcquireBuffer();
        }

        public static void ReleaseBuffer(byte[] buffer)
        {
            if (buffer != null && buffer.Length == s_CoalesceBufferSize)
            {
                s_UnusedBuffers.ReleaseBuffer(buffer);
            }
        }

        private Queue<Gram> _pending;

        private Gram _buffered;

        public bool IsFlushReady
        {
            get { return (_pending.Count == 0 && _buffered != null); }
        }

        public bool IsEmpty
        {
            get { return (_pending.Count == 0 && _buffered == null); }
        }

        public SendQueue()
        {
            _pending = new Queue<Gram>();
        }

        public Gram CheckFlushReady()
        {
            Gram gram = null;

            if (_pending.Count == 0 && _buffered != null)
            {
                gram = _buffered;

                _pending.Enqueue(_buffered);
                _buffered = null;
            }

            return gram;
        }

        public Gram Dequeue()
        {
            Gram gram = null;

            if (_pending.Count > 0)
            {
                _pending.Dequeue().Release();

                if (_pending.Count > 0)
                {
                    gram = _pending.Peek();
                }
            }

            return gram;
        }

        private const int PendingCap = 96 * 1024;

        public Gram Enqueue(byte[] buffer, int length)
        {
            return Enqueue(buffer, 0, length);
        }

        public Gram Enqueue(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            else if (!(offset >= 0 && offset < buffer.Length))
            {
                throw new ArgumentOutOfRangeException("offset", offset, "Offset must be greater than or equal to zero and less than the size of the buffer.");
            }
            else if (length < 0 || length > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length cannot be less than zero or greater than the size of the buffer.");
            }
            else if ((buffer.Length - offset) < length)
            {
                throw new ArgumentException("Offset and length do not point to a valid segment within the buffer.");
            }

            int existingBytes = (_pending.Count * s_CoalesceBufferSize) + (_buffered == null ? 0 : _buffered.Length);

            if ((existingBytes + length) > PendingCap)
            {
                throw new CapacityExceededException();
            }

            Gram gram = null;

            while (length > 0)
            {
                if (_buffered == null)
                { 
                    // nothing yet buffered
                    _buffered = Gram.Acquire();
                }

                int bytesWritten = _buffered.Write(buffer, offset, length);

                offset += bytesWritten;
                length -= bytesWritten;

                if (_buffered.IsFull)
                {
                    if (_pending.Count == 0)
                    {
                        gram = _buffered;
                    }

                    _pending.Enqueue(_buffered);
                    _buffered = null;
                }
            }

            return gram;
        }

        public void Clear()
        {
            if (_buffered != null)
            {
                _buffered.Release();
                _buffered = null;
            }

            while (_pending.Count > 0)
            {
                _pending.Dequeue().Release();
            }
        }
    }

    public sealed class CapacityExceededException : Exception
    {
        public CapacityExceededException()
            : base("Too much data pending.") { }
    }

    public class BufferPool
    {
        private static List<BufferPool> s_Pools = new List<BufferPool>();

        public static List<BufferPool> Pools
        {
            get { return s_Pools; }
        }

        private string m_Name;

        private int m_InitialCapacity;
        private int m_BufferSize;

        private int m_Misses;

        private Queue<byte[]> m_FreeBuffers;

        public void GetInfo(out string name, out int freeCount, out int initialCapacity, out int currentCapacity, out int bufferSize, out int misses)
        {
            lock (this)
            {
                name = m_Name;
                freeCount = m_FreeBuffers.Count;
                initialCapacity = m_InitialCapacity;
                currentCapacity = m_InitialCapacity * (1 + m_Misses);
                bufferSize = m_BufferSize;
                misses = m_Misses;
            }
        }

        public BufferPool(string name, int initialCapacity, int bufferSize)
        {
            m_Name = name;

            m_InitialCapacity = initialCapacity;
            m_BufferSize = bufferSize;

            m_FreeBuffers = new Queue<byte[]>(initialCapacity);

            for (int i = 0; i < initialCapacity; ++i)
            {
                m_FreeBuffers.Enqueue(new byte[bufferSize]);
            }

            lock (s_Pools)
                s_Pools.Add(this);
        }

        public byte[] AcquireBuffer()
        {
            lock (this)
            {
                if (m_FreeBuffers.Count > 0)
                {
                    return m_FreeBuffers.Dequeue();
                }

                ++m_Misses;

                for (int i = 0; i < m_InitialCapacity; ++i)
                {
                    m_FreeBuffers.Enqueue(new byte[m_BufferSize]);
                }

                return m_FreeBuffers.Dequeue();
            }
        }

        public void ReleaseBuffer(byte[] buffer)
        {
            if (buffer == null)
                return;

            lock (this)
            {
                m_FreeBuffers.Enqueue(buffer);
            }
        }

        public void Free()
        {
            lock (s_Pools)
            {
                s_Pools.Remove(this);
            }
        }
    }
}
