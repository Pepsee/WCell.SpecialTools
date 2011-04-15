/*************************************************************************
 *
 *   file		: PacketIn.cs
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

using System.IO;
using System.Text;
using System;
using System.Runtime.CompilerServices;

namespace WoWExtractor
{
    public sealed class AuthPacketIn : PacketIn
    {
        public AuthPacketIn(byte[] data, int size)
            : base(data, size)
        { }

        public override void ResetIndex()
        {
            m_Index = 0;
        }
    }

    public sealed class AuthPacketOut : PacketOut
    {
        public override void FinalizePacket()
        {
        }
    }

    public sealed class RealmPacketOut : PacketOut
    {
        public RealmPacketOut(uint opcode, int initialCapacity) : base(initialCapacity)
        {
            WriteInt16(0); // skip len
            WriteUInt32(opcode);
        }

        public override void FinalizePacket()
        {
            long curPos = m_Stream.Position;
            m_Stream.Position = 0;
            WriteUInt16((ushort)(m_Stream.Length - 2));
            m_Stream.Position = curPos;
        }
    }

    public sealed class RealmPacketIn : PacketIn
    {
        private readonly uint m_opcode;

        public RealmPacketIn(byte[] data, int size)
            : base(data, size)
        {
            int length = ReadUInt16BE() + 2;

            if (m_Size != length)
            {
                // whoops...
            }

            m_opcode = ReadUInt32();
        }

        public override void ResetIndex()
        {
            m_Index = 6;
        }

        public uint Opcode
        {
            get { return m_opcode; }
        }
    }

    public abstract class PacketIn
    {
        private readonly byte[] m_Data;
        protected int m_Index;
        protected readonly int m_Size;

        public abstract void ResetIndex();

        protected PacketIn(byte[] data, int size)
        {
            m_Data = data;
            m_Size = size;
            m_Index = 0;
        }

        public short ReadInt16()
        {
            if ((m_Index + 2) > m_Size)
                return 0;

            return (short)((m_Data[m_Index++] << 8) | m_Data[m_Index++]);
        }

        [MethodImpl(MethodCodeType = MethodCodeType.Native)]
        public unsafe short ReadInt16_Unsafe()
        {
            if ((m_Index + 2) > m_Size)
            {
                return 0;
            }

            m_Index += 2;
            fixed (byte* pData = &m_Data[m_Index])
            {
                return *(short*)pData;
            }
        }

        public unsafe short ReadInt16BE()
        {
            if ((m_Index + 2) > m_Size)
            {
                return 0;
            }

            return (short)
                (m_Data[m_Index++] |
                (m_Data[m_Index++] << 8));
        }

        public char ReadChar()
        {
            if ((m_Index + 1) > m_Size)
            {
                return char.MinValue;
            }

            return (char)m_Data[m_Index++];
        }

        public unsafe char ReadChar_Unsafe()
        {
            if ((m_Index + 1) > m_Size)
            {
                return char.MinValue;
            }

            char[] retChars = new char[1];

            fixed (byte* pData = &m_Data[m_Index++])
            {
                fixed (char* cData = retChars)
                {
                    if (UTF8Encoding.UTF8.GetDecoder().GetChars(pData, 1, cData, 1, true) == 1)
                    {
                        return retChars[0];
                    }
                    else
                    {
                        return char.MinValue;
                    }
                }
            }
        }

        public char[] ReadChars(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count", "char count must be greater than zero!");
            }

            byte[] bytes = new byte[count];

            int i = 0;
            while (m_Index < m_Size && i < count)
            {
                bytes[i++] = m_Data[m_Index++];
            }

            if (i < count)
            {
                if (i > 0)
                {
                    byte[] actualBytes = new byte[i];

                    Buffer.BlockCopy(bytes, 0, actualBytes, 0, i);

                    return UTF8Encoding.UTF8.GetChars(actualBytes);
                }
                else
                {
                    return new char[0];
                }
            }

            return UTF8Encoding.UTF8.GetChars(bytes);
        }

        public string ReadPascalString()
        {
            int size = ReadByte();
            return new string(ReadChars(size));
        }

        public int ReadInt32()
        {
            if ((m_Index + 4) > m_Size)
                return 0;

            return (m_Data[m_Index++] << 24)
                 | (m_Data[m_Index++] << 16)
                 | (m_Data[m_Index++] << 8)
                 | m_Data[m_Index++];
        }

        public unsafe int ReadInt32_Unsafe()
        {
            if ((m_Index + 4) > m_Size)
            {
                return 0;
            }
            
            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 4;
                return *(int*)pData;
            }
        }

        public int ReadInt32BE()
        {
            if ((m_Index + 4) > m_Size)
            {
                return 0;
            }

            return (m_Data[m_Index++] |
                (m_Data[m_Index++] << 8) |
                (m_Data[m_Index++] << 16) |
                (m_Data[m_Index++] << 24));
        }
    
        public byte ReadByte()
        {
            if ((m_Index + 1) > m_Size)
                return 0;

            return m_Data[m_Index++];
        }

        public unsafe float ReadFloat_Unsafe()
        {
            if ((m_Index + 4) > m_Size)
            {
                return float.NaN;
            }
            
            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 4;
                return *(float*)pData;
            }
        }

        public unsafe double ReadDouble_Unsafe()
        {
            if ((m_Index + 8) > m_Size)
            {
                return double.NaN;
            }
            
            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 8;
                return *(double*)pData;
            }
        }

        public byte[] DataBuffer
        {
            get { return m_Data; }
        }

        public int Size
        {
            get { return m_Size; }
        }

        public byte[] ReadBytes2(int count)
        {
            byte[] retBuffer;
            if ((m_Index + count) > m_Size)
            {
                retBuffer = new byte[m_Size - m_Index];
                Buffer.BlockCopy(m_Data, m_Index, retBuffer, 0, retBuffer.Length);
                m_Index = m_Size;
                return retBuffer;
            }

            retBuffer = new byte[count];
            Buffer.BlockCopy(m_Data, m_Index, retBuffer, 0, count);
            m_Index += count;
            return retBuffer;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count", "byte count must be greater than zero!");
            }

            byte[] bytes = new byte[count];

            int i = 0;
            while (m_Index < m_Size && i < count)
            {
                bytes[i++] = m_Data[m_Index++];
            }

            // in case we prematurely hit the end of a packet
            if (i < count)
            {
                if (i > 0)
                {
                    byte[] actualBytes = new byte[i];

                    Buffer.BlockCopy(bytes, 0, actualBytes, 0, i);

                    return actualBytes;
                }
                else
                {
                    return new byte[0];
                }
            }

            return bytes;
        }

        public sbyte ReadSByte()
        {
            if ((m_Index + 1) > m_Size)
                return 0;

            return (sbyte)m_Data[m_Index++];
        }

        public bool ReadBoolean()
        {
            if ((m_Index + 1) > m_Size)
                return false;

            return (m_Data[m_Index++] != 0);
        }



        public ushort ReadUInt16()
        {
            if ((m_Index + 2) > m_Size)
                return 0;

            return (ushort)((m_Data[m_Index++] << 8) | m_Data[m_Index++]);
        }

        public unsafe ushort ReadUInt16_Unsafe()
        {
            if ((m_Index + 2) > m_Size)
            {
                return 0;
            }
            
            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 2;
                return *(ushort*)pData;
            }
        }

        public ushort ReadUInt16BE()
        {
            if ((m_Index + 2) > m_Size)
                return 0;

            return (ushort)(m_Data[m_Index++] | (m_Data[m_Index++] << 8));
        }

        public uint ReadUInt32()
        {
            if ((m_Index + 4) > m_Size)
                return 0;

            return (uint)((m_Data[m_Index++] << 24) 
                | (m_Data[m_Index++] << 16) 
                | (m_Data[m_Index++] << 8) 
                | m_Data[m_Index++]);
        }



        public unsafe uint ReadUInt32_Unsafe()
        {
            if ((m_Index + 4) > m_Size)
            {
                return 0;
            }

            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 4;
                return *(uint*)pData;
            }
        }

        public uint ReadUInt32BE()
        {
            if ((m_Index + 4) > m_Size)
            {
                return 0;
            }

            return (uint)(m_Data[m_Index++] |
                (m_Data[m_Index++] << 8) |
                (m_Data[m_Index++] << 16) |
                (m_Data[m_Index++] << 24));
        }

        public unsafe long ReadInt64_Unsafe()
        {
            if ((m_Index + 8) > m_Size)
            {
                return 0;
            }

            m_Index += 8;
            fixed (byte* pData = &m_Data[m_Index])
            {
                return *(long*)pData;
            }
        }

        public unsafe long ReadInt64BE()
        {
            if ((m_Index + 8) > m_Size)
            {
                return 0;
            }

            byte* pData;

            fixed (byte* bufPtr = &m_Data[m_Index])
            {
                pData = bufPtr;
            }

            m_Index += 8;

            return (long)((*(byte*)&pData[0] << 56) | (*(byte*)&pData[1] << 48) | (*(byte*)&pData[2] << 40) | (*(byte*)&pData[3] << 32) |
                          (*(byte*)&pData[4] << 24) | (*(byte*)&pData[5] << 16) | (*(byte*)&pData[6] << 8) | *(byte*)&pData[7]);
        }

        public ulong ReadUInt64()
        {
            if ((m_Index + 8) > m_Size)
                return 0;

            uint low = ReadUInt32();
            uint high = ReadUInt32();
            return ((high << 32) | low);
        }

        public unsafe ulong ReadUInt64_Unsafe()
        {
            if ((m_Index + 8) > m_Size)
                return 0;

            fixed (byte* pData = &m_Data[m_Index])
            {
                m_Index += 8;
                return *(ulong*)pData;
            }
        }

        public string ReadString()
        {
            StringBuilder sb = new StringBuilder();
            byte c;

            while (m_Index < m_Size && (c = m_Data[m_Index++]) != 0)
            {
                sb.Append((char)c);
            }

            return sb.ToString();
        }

        public int Seek(int offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: m_Index = offset; break;
                case SeekOrigin.Current: m_Index += offset; break;
                case SeekOrigin.End: m_Index = m_Size - offset; break;
            }

            return m_Index;
        }
    }

    public abstract class PacketOut
    {
        private static readonly byte[] m_Data = new byte[4];

        protected readonly MemoryStream m_Stream;

        public PacketOut()
            : this(32)
        { }

        public PacketOut(int initialCapacity)
        {
            m_Stream = new MemoryStream(initialCapacity);
        }

        public abstract void FinalizePacket();

        public void Seek(int offset, SeekOrigin origin)
        {
            m_Stream.Seek(offset, origin);
        }

        /// <summary>
        /// Writes a 1-byte boolean value to the underlying stream. False is represented by 0, true by 1.
        /// </summary>
        public void WriteBoolean(bool value)
        {
            m_Stream.WriteByte((byte)(value ? 1 : 0));
        }

        /// <summary>
        /// Writes a 1-byte unsigned integer value to the underlying stream.
        /// </summary>
        public void WriteByte(byte value)
        {
            m_Stream.WriteByte(value);
        }

        /// <summary>
        /// Writes a 1-byte signed integer value to the underlying stream.
        /// </summary>
        public void WriteSByte(sbyte value)
        {
            m_Stream.WriteByte((byte)value);
        }

        /// <summary>
        /// Writes a 2-byte signed integer value to the underlying stream.
        /// </summary>
        public void WriteInt16(short value)
        {
            m_Data[0] = (byte)(value >> 8);
            m_Data[1] = (byte)value;

            m_Stream.Write(m_Data, 0, 2);
        }

        public void WriteInt16BE(short value)
        {
            m_Data[0] = (byte)value;
            m_Data[1] = (byte)(value >> 8);

            m_Stream.Write(m_Data, 0, 2);
        }

        /// <summary>
        /// Writes a 2-byte unsigned integer value to the underlying stream.
        /// </summary>
        public void WriteUInt16(ushort value)
        {
            m_Data[0] = (byte)(value >> 8);
            m_Data[1] = (byte)value;

            m_Stream.Write(m_Data, 0, 2);
        }

        public void WriteUInt16BE(ushort value)
        {      
            m_Data[0] = (byte)value;
            m_Data[1] = (byte)(value >> 8);

            m_Stream.Write(m_Data, 0, 2);
        }

        /// <summary>
        /// Writes a 4-byte signed integer value to the underlying stream.
        /// </summary>
        public void WriteInt32(int value)
        {
            m_Data[0] = (byte)(value >> 24);
            m_Data[1] = (byte)(value >> 16);
            m_Data[2] = (byte)(value >> 8);
            m_Data[3] = (byte)value;

            m_Stream.Write(m_Data, 0, 4);
        }

        /// <summary>
        /// Writes a 4-byte unsigned integer value to the underlying stream.
        /// </summary>
        public void WriteUInt32(uint value)
        {
            m_Data[0] = (byte)(value >> 24);
            m_Data[1] = (byte)(value >> 16);
            m_Data[2] = (byte)(value >> 8);
            m_Data[3] = (byte)value;

            m_Stream.Write(m_Data, 0, 4);
        }

        public void WriteUInt64(ulong value)
        {
            // this way allows us to keep m_Data at 4 bytes by using 2 writes

            m_Data[0] = (byte)(value >> 56);
            m_Data[1] = (byte)(value >> 48);
            m_Data[2] = (byte)(value >> 40);
            m_Data[3] = (byte)(value >> 32);
            m_Stream.Write(m_Data, 0, 4);

            m_Data[0] = (byte)(value >> 24);
            m_Data[1] = (byte)(value >> 16);
            m_Data[2] = (byte)(value >> 8);
            m_Data[3] = (byte)value;

            m_Stream.Write(m_Data, 0, 4);
        }

        public void WriteCString(string value)
        {
            byte[] stringBytes = Encoding.ASCII.GetBytes(value);
            m_Stream.Write(stringBytes, 0, stringBytes.Length);
            m_Stream.WriteByte(0);
        }

        /// <summary>
        /// Writes a sequence of bytes to the underlying stream
        /// </summary>
        public void Write(byte[] buffer, int offset, int size)
        {
            m_Stream.Write(buffer, offset, size);
        }
    }
}
