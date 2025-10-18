using System;

namespace EnderAudioAnalyzer.Decoders;

/// <summary>
/// AAC 位级读取器
/// </summary>
internal class AacBitReader
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
    
    public int ReadBits(int count)
    {
        if (count <= 0 || count > 32)
            throw new ArgumentException("位数必须在 1-32 之间", nameof(count));
        
        int result = 0;
        
        for (int i = 0; i < count; i++)
        {
            if (_bytePosition >= _data.Length)
                return result;
            
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
    
    public void AlignToByte()
    {
        if (_bitPosition != 0)
        {
            _bitPosition = 0;
            _bytePosition++;
        }
    }
    
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
    
    public int Position => _bytePosition * 8 + _bitPosition;
    
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