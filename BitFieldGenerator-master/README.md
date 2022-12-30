# BitFieldGenerator

Roslyn Code Fix provider for metaprogramming to generate bit fields

original code:

```cs
 struct MyCode
{
    struct BitFields
    {
        [BitField(10)]
        short X;

        [BitField(2)]
        byte Y;

        [BitField(12)]
        short Z;

        [BitField(24)]
        int W;
    }

    long _value;
}
```

generated code:

```cs
partial struct MyCode
{
    public short X => (short)((_value >> 0) & 0x3FF);

    public byte Y => (byte)((_value >> 10) & 0x3);

    public short Z => (short)((_value >> 12) & 0xFFF);

    public int W => (int)((_value >> 24) & 0xFFFFFF);

    public MyCode(short x, byte y, short z, int w)
    {
        _value = 0;
        _value |= (long)(x & 0x3FF) << 0;
        _value |= (long)(y & 0x3) << 10;
        _value |= (long)(z & 0xFFF) << 12;
        _value |= (long)(w & 0xFFFFFF) << 24;
    }
}
```

