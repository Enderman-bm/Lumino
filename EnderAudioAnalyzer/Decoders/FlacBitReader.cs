using System;
using System.IO;

namespace EnderAudioAnalyzer.Decoders
{
    /// <summary>
    /// FLAC位级读取器
    /// 支持按位读取，用于解码FLAC压缩数据
    /// </summary>
    internal class FlacBitReader : IDisposable
    {
        private readonly Stream _stream;
        private byte _currentByte;
        private int _bitPosition; // 0-7，从高位到低位
        private bool _disposed;

        public FlacBitReader(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _bitPosition = 8; // 强制读取第一个字节
        }

        /// <summary>
        /// 检查是否到达流末尾
        /// </summary>
        public bool IsEndOfStream()
        {
            return _stream.Position >= _stream.Length && _bitPosition >= 8;
        }

        /// <summary>
        /// 读取单个位
        /// </summary>
        public bool ReadBit()
        {
            if (_bitPosition >= 8)
            {
                if (_stream.Position >= _stream.Length)
                    throw new EndOfStreamException();
                
                _currentByte = (byte)_stream.ReadByte();
                _bitPosition = 0;
            }

            bool bit = (_currentByte & (0x80 >> _bitPosition)) != 0;
            _bitPosition++;
            return bit;
        }

        /// <summary>
        /// 读取指定数量的位（无符号）
        /// </summary>
        public int ReadBits(int count)
        {
            if (count < 0 || count > 32)
                throw new ArgumentOutOfRangeException(nameof(count));

            int value = 0;
            for (int i = 0; i < count; i++)
            {
                value = (value << 1) | (ReadBit() ? 1 : 0);
            }
            return value;
        }

        /// <summary>
        /// 读取指定数量的位（长整型，无符号）
        /// </summary>
        public long ReadBitsLong(int count)
        {
            if (count < 0 || count > 64)
                throw new ArgumentOutOfRangeException(nameof(count));

            long value = 0;
            for (int i = 0; i < count; i++)
            {
                value = (value << 1) | (ReadBit() ? 1L : 0L);
            }
            return value;
        }

        /// <summary>
        /// 读取指定数量的位（有符号，二进制补码）
        /// </summary>
        public int ReadSignedBits(int count)
        {
            if (count < 0 || count > 32)
                throw new ArgumentOutOfRangeException(nameof(count));

            int value = ReadBits(count);
            
            // 检查符号位并转换为有符号数
            if (count > 0 && (value & (1 << (count - 1))) != 0)
            {
                // 负数：扩展符号位
                value |= (-1 << count);
            }
            
            return value;
        }

        /// <summary>
        /// 预览指定数量的位（不移动位置）
        /// </summary>
        public int PeekBits(int count)
        {
            if (count < 0 || count > 32)
                throw new ArgumentOutOfRangeException(nameof(count));

            long savedPosition = _stream.Position;
            int savedBitPosition = _bitPosition;
            byte savedByte = _currentByte;

            int value = 0;
            try
            {
                value = ReadBits(count);
            }
            finally
            {
                // 恢复位置
                _stream.Position = savedPosition;
                _bitPosition = savedBitPosition;
                _currentByte = savedByte;
            }

            return value;
        }

        /// <summary>
        /// 读取指定数量的字节
        /// </summary>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            // 对齐到字节边界
            AlignToByte();

            byte[] buffer = new byte[count];
            int bytesRead = _stream.Read(buffer, 0, count);
            
            if (bytesRead != count)
                throw new EndOfStreamException($"预期读取 {count} 字节，实际读取 {bytesRead} 字节");

            return buffer;
        }

        /// <summary>
        /// 跳过指定数量的字节
        /// </summary>
        public void SkipBytes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            // 对齐到字节边界
            AlignToByte();

            _stream.Seek(count, SeekOrigin.Current);
        }

        /// <summary>
        /// 对齐到下一个字节边界
        /// </summary>
        public void AlignToByte()
        {
            if (_bitPosition > 0 && _bitPosition < 8)
            {
                _bitPosition = 8; // 丢弃当前字节的剩余位
            }
        }

        /// <summary>
        /// 获取当前流位置（字节）
        /// </summary>
        public long Position => _stream.Position;

        /// <summary>
        /// 获取流总长度（字节）
        /// </summary>
        public long Length => _stream.Length;

        public void Dispose()
        {
            if (!_disposed)
            {
                // 注意：不关闭底层流，因为它可能由调用者管理
                _disposed = true;
            }
        }
    }
}