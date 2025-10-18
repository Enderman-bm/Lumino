using System;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// OPUS 位级读取器
/// </summary>
internal class OpusBitReader
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
    /// 读取指定位数（MSB优先）
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
            
            // OPUS 使用 MSB 优先（大端序）
            int bit = (_data[_bytePosition] >> (7 - _bitPosition)) & 1;
            result = (result << 1) | bit;
            
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
    /// 读取无符号整数（变长编码）
    /// </summary>
    public int ReadVarInt()
    {
        int value = 0;
        int shift = 0;
        
        while (true)
        {
            if (_bytePosition >= _data.Length)
                break;
            
            int b = ReadBits(8);
            value |= (b & 0x7F) << shift;
            
            if ((b & 0x80) == 0)
                break;
            
            shift += 7;
        }
        
        return value;
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