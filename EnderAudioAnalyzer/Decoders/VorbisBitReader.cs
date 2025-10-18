using System;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// Vorbis 位级读取器（LSB优先）
/// Vorbis 使用小端序位顺序
/// </summary>
internal class VorbisBitReader
{
    private byte[] _data = Array.Empty<byte>();
    private int _bytePosition;
    private int _bitPosition;
    
    public void Initialize(byte[] data)
    {
        _data = data;
        _bytePosition = 0;
        _bitPosition = 0;
    }
    
    /// <summary>
    /// 读取指定位数（LSB优先）
    /// </summary>
    public int ReadBits(int count)
    {
        if (count <= 0 || count > 32)
            throw new ArgumentException("位数必须在 1-32 之间", nameof(count));
        
        int result = 0;
        
        for (int i = 0; i < count; i++)
        {
            if (_bytePosition >= _data.Length)
                return result;
            
            // Vorbis 使用 LSB 优先
            int bit = (_data[_bytePosition] >> _bitPosition) & 1;
            result |= (bit << i);
            
            _bitPosition++;
            if (_bitPosition >= 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 读取有符号位
    /// </summary>
    public int ReadSignedBits(int count)
    {
        int value = ReadBits(count);
        
        // 检查符号位
        if ((value & (1 << (count - 1))) != 0)
        {
            // 负数：扩展符号位
            value |= ~((1 << count) - 1);
        }
        
        return value;
    }
    
    /// <summary>
    /// 对齐到字节边界
    /// </summary>
    public void AlignToByte()
    {
        if (_bitPosition != 0)
        {
            _bitPosition = 0;
            _bytePosition++;
        }
    }
    
    /// <summary>
    /// 跳过指定位数
    /// </summary>
    public void Skip(int bits)
    {
        for (int i = 0; i < bits; i++)
        {
            _bitPosition++;
            if (_bitPosition >= 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }
        }
    }
    
    /// <summary>
    /// 读取 32 位小端整数
    /// </summary>
    public int ReadInt32()
    {
        return ReadBits(32);
    }
    
    /// <summary>
    /// 读取 16 位小端整数
    /// </summary>
    public int ReadInt16()
    {
        return ReadBits(16);
    }
    
    /// <summary>
    /// 读取 8 位整数
    /// </summary>
    public int ReadByte()
    {
        return ReadBits(8);
    }
    
    /// <summary>
    /// 当前位置（以位为单位）
    /// </summary>
    public int Position => _bytePosition * 8 + _bitPosition;
    
    /// <summary>
    /// 剩余位数
    /// </summary>
    public int RemainingBits
    {
        get
        {
            if (_bytePosition >= _data.Length)
                return 0;
            return (_data.Length - _bytePosition) * 8 - _bitPosition;
        }
    }
}