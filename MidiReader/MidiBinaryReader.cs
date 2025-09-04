using System;

namespace MidiReader
{
    /// <summary>
    /// 高性能二进制数据读取器，专门用于MIDI文件解析
    /// 使用Span&lt;byte&gt;避免不必要的内存分配
    /// </summary>
    public ref struct MidiBinaryReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public MidiBinaryReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        /// <summary>
        /// 当前位置
        /// </summary>
        public int Position => _position;

        /// <summary>
        /// 剩余可读字节数
        /// </summary>
        public int Remaining => _data.Length - _position;

        /// <summary>
        /// 是否已到达数据末尾
        /// </summary>
        public bool IsAtEnd => _position >= _data.Length;

        /// <summary>
        /// 读取单个字节
        /// </summary>
        public byte ReadByte()
        {
            if (_position >= _data.Length)
                throw new InvalidOperationException("Reached end of data");
            
            return _data[_position++];
        }

        /// <summary>
        /// 窥视下一个字节但不移动位置
        /// </summary>
        public byte PeekByte()
        {
            if (_position >= _data.Length)
                throw new InvalidOperationException("Reached end of data");
            
            return _data[_position];
        }

        /// <summary>
        /// 读取16位大端序整数
        /// </summary>
        public ushort ReadUInt16BigEndian()
        {
            if (_position + 2 > _data.Length)
                throw new InvalidOperationException("Not enough data for UInt16");

            ushort value = (ushort)((_data[_position] << 8) | _data[_position + 1]);
            _position += 2;
            return value;
        }

        /// <summary>
        /// 读取32位大端序整数
        /// </summary>
        public uint ReadUInt32BigEndian()
        {
            if (_position + 4 > _data.Length)
                throw new InvalidOperationException("Not enough data for UInt32");

            uint value = (uint)((_data[_position] << 24) | 
                               (_data[_position + 1] << 16) | 
                               (_data[_position + 2] << 8) | 
                               _data[_position + 3]);
            _position += 4;
            return value;
        }

        /// <summary>
        /// 读取Variable Length Quantity (VLQ)
        /// MIDI使用VLQ编码时间间隔和长度
        /// </summary>
        public uint ReadVariableLengthQuantity()
        {
            uint value = 0;
            byte currentByte;
            
            do
            {
                if (_position >= _data.Length)
                    throw new InvalidOperationException("Incomplete VLQ");
                
                currentByte = _data[_position++];
                value = (value << 7) | (uint)(currentByte & 0x7F);
                
                // 防止无限循环和整数溢出
                if (value > 0x0FFFFFFF)
                    throw new InvalidOperationException("VLQ too large");
                    
            } while ((currentByte & 0x80) != 0);
            
            return value;
        }

        /// <summary>
        /// 读取指定长度的字节数组
        /// </summary>
        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (_position + length > _data.Length)
                throw new InvalidOperationException($"Not enough data. Requested: {length}, Available: {Remaining}");

            var result = _data.Slice(_position, length);
            _position += length;
            return result;
        }

        /// <summary>
        /// 跳过指定数量的字节
        /// </summary>
        public void Skip(int count)
        {
            if (_position + count > _data.Length)
                throw new InvalidOperationException($"Cannot skip {count} bytes, only {Remaining} remaining");
            
            _position += count;
        }

        /// <summary>
        /// 设置当前位置
        /// </summary>
        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
                throw new ArgumentOutOfRangeException(nameof(position));
            
            _position = position;
        }

        /// <summary>
        /// 读取以null结尾的字符串
        /// </summary>
        public string ReadNullTerminatedString()
        {
            var start = _position;
            while (_position < _data.Length && _data[_position] != 0)
            {
                _position++;
            }
            
            if (_position < _data.Length)
            {
                var result = System.Text.Encoding.ASCII.GetString(_data.Slice(start, _position - start));
                _position++; // 跳过null终止符
                return result;
            }
            
            throw new InvalidOperationException("Null-terminated string not found");
        }

        /// <summary>
        /// 读取固定长度的ASCII字符串
        /// </summary>
        public string ReadFixedLengthString(int length)
        {
            var bytes = ReadBytes(length);
            return System.Text.Encoding.ASCII.GetString(bytes);
        }
    }
}