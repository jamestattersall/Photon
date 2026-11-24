using ProtonConsole2.Proton;
using System.Buffers.Binary;

namespace ProtonConsole2.Proton
{

    public class BigEndianIntegerPrimitive : Proton.IPrimativeIntConverter
    {
        public bool IsBigEndian => true;

        public short Int16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt16BigEndian(memory.Span);

        public Int32 Int32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt32BigEndian(memory.Span);

        public ushort UInt16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt16BigEndian(memory.Span);

        public UInt32 UInt32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt32BigEndian(memory.Span);
    }

    public class LittleEndianIntegerPrimitive : Proton.IPrimativeIntConverter
    {
        public bool IsBigEndian => false;

        public short Int16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt16LittleEndian(memory.Span);

        public Int32 Int32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt32LittleEndian(memory.Span);

        public ushort UInt16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Span);

        public UInt32 UInt32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Span);
    }


    /// IEEE 754 floating point big-endian converter
    public class BigEndianFloatPrimative : Proton.IPrimativeFloatConverter
    {
        bool Proton.IPrimativeFloatConverter.IsBigEndian => true;

        float Proton.IPrimativeFloatConverter.DoubleConverter(ReadOnlyMemory<byte> memory) {
            float res = (float)BinaryPrimitives.ReadDoubleBigEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
        }

        float Proton.IPrimativeFloatConverter.SingleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = BinaryPrimitives.ReadSingleBigEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
        }
    }

    /// IEEE 754 floating point little-endian converter
    public class LittleEndianFloatPrimative : Proton.IPrimativeFloatConverter
    {
        bool Proton.IPrimativeFloatConverter.IsBigEndian => true;

        float Proton.IPrimativeFloatConverter.DoubleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = (float)BinaryPrimitives.ReadDoubleLittleEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
         
        }

        float Proton.IPrimativeFloatConverter.SingleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = BinaryPrimitives.ReadSingleLittleEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0 ;
        }
    }


}

/// <summary>
/// functions to convert floating point numbers from byte arrays in HP Alpha format
/// </summary>
public class HPAlphaConverter
{
    // tested on machine: HP Alpha ES40,  O.S.: OpenVMS v7.3. at Nottingham maternity QMC
    // little endian


    /// <summary>
    /// always returns false
    /// </summary>
    public bool IsBigEndian
    {
        get
        {
            return false;
        }
    }

    public float Binary2Single(ReadOnlyMemory<byte> memory)
    {
        // floating point single, up to 3 mantissa bytes
        // 1 sign bit + 7 exponent bits + 8*(lenghth-1) mantissa bits. Exponent bias 128, Mantissa implied most significant bit= 1. Normalised to fractional point to left of most signifcant bit (mantissa normalised fraction &lt;1).


        byte exp;
        int length = (memory.Length);
        if (length == 0)
        {
            return 0f;
        }
        else if (length == 1)
        {
            exp = (byte)(memory.Span[0] << 1);
            if (exp == 0)
            {
                return 0f;
            }
            else if (memory.Span[0] == 254)
            {
                return 0f;
            }
            else if (memory.Span[0] > 127)
            {
                return (float)-Math.Pow(2d, exp - 128);
            }
            else
            {
                return (float)Math.Pow(2d, exp - 128);
            }
        }

        else
        {
            // do everything we can as fast bitwise operations
            if (length > 4)
                length = 4;
            exp = (byte)((memory.Span[1] << 1) + (memory.Span[0] >> 7));
            uint mant = (uint)(memory.Span[0] | 128); // adds the implied leading binary '1'

            for (byte i = 2, loopTo = (byte)(length - 1); i <= loopTo; i++)
            {
                mant = mant << 8;
                mant += memory.Span[i];
            }

            // combine all the slow floating point arithmatic at the last step
            if (memory.Span[1] > 127)
            {
                return (float)(-mant * Math.Pow(2d, exp - 8 * (length - 1) - 128));
            }
            else
            {
                return (float)(mant * Math.Pow(2d, exp - 8 * (length - 1) - 128));
            }

        }

    }
    public double Binary2Double(ReadOnlyMemory<byte> memory)
    {
        // floating point double (same as single but up to 7 mantissa bytes)
        // 1 sign bit + 7 exponent bits + 8*(length-1) mantissa bits. Exponent bias 128, Mantissa implied most significant bit= 1, normalised to fractional point to left of most signifcant bit (1> mantissa normalised fraction >=0).

        byte exp;
        var length = memory.Length;
        if (length <= 0)
        {
            return 0d;
        }
        else if (length == 1)
        {
            exp = (byte)(memory.Span[0] << 1);
            if (exp == 0)
            {
                return 0d;
            }
            else if (memory.Span[0] == 254)
            {
                return 0d;
            }
            else if (memory.Span[0] > 127)
            {
                return -Math.Pow(2d, exp - 128);
            }
            else
            {
                return Math.Pow(2d, exp - 128);
            }
        }
        else
        {
            if (length > 8)
                length = 8;

            // do everything we can as fast bitwise operations
            exp = (byte)((memory.Span[1] << 1) + (memory.Span[0] >> 7));
            ulong mant = (ulong)(memory.Span[0] | 128); // adds the implied leading binary '1'
            for (byte i = 2, loopTo = (byte)(length - 1); i <= loopTo; i++)
            {
                mant = mant << 8;
                mant += memory.Span[i];
            }

            // combine all the slow floating point arithmatic at the last step
            if (memory.Span[1] > 127)
            {
                return -(double)mant * Math.Pow(2d, exp - 8 * (length - 1) - 128);
            }
            else
            {
                return mant * Math.Pow(2d, exp - 8 * (length - 1) - 128);
            }
        }
    }
}

/// <summary>
/// converters for machines where floating point numbers are held in big-endian VAX format
/// </summary>
public class FloatVaxBigEndian 
{
    // tested on machine: HP Alpha ES40,  O.S.: OpenVMS v7.3. at Nottingham maternity QMC
    // VAX floats 

    /// <summary>
    /// always returns true
    /// </summary>
    public bool IsBigEndian
    {
        get
        {
            return true;
        }
    }

    /// <inheritdoc/>
    public float Binary2Single(byte[] arry, ushort start, byte length)
    {
        // VAX floating point single, up to 3 mantissa bytes
        // 1 sign bit + 7 exponent bits + 8*(lenghth-1) mantissa bits. Exponent bias 128, Mantissa implied most significant bit= 1. Normalised to fractional point to left of most signifcant bit (mantissa normalised fraction < 1).

        byte exp;
        if (length <= 0)
        {
            return 0f;
        }

        else if (length == 1)
        {
            exp = (byte)(arry[start] << 1);
            if (exp == 0)
            {
                return 0f;
            }
            else if (exp == 254)
            {
                return 0f;
            }
            else if (arry[start] > 127)
            {
                return (float)-Math.Pow(2d, exp - 128);
            }
            else
            {
                return (float)Math.Pow(2d, exp - 128);
            }
        }
        else
        {
            if (length > 4)
                length = 4;

            // do everything we can as fast bitwise operations
            exp = (byte)((arry[start + 1] << 1) + (arry[start] >> 7));
            uint mant = (uint)(arry[start] | 128); // adds the implied leading binary '1'

            for (byte i = (byte)(length - 1); i <= 2; i++)
            {
                mant = mant << 8;
                mant = mant | arry[start + i];
            }

            // combine all the slow floating point arithmatic at the last step
            if (arry[start + 1] > 127)
            {
                return (float)(-mant * Math.Pow(2d, exp - 8 * (length - 1) - 128));
            }
            else
            {
                return (float)(mant * Math.Pow(2d, exp - 8 * (length - 1) - 128));
            }
        }
    }


    /// <inheritdoc/>
    public double Binary2Double(byte[] arry, ushort start, byte length)
    {
        // VAX floating point double (same as VAX single but up to 7 mantissa bytes)
        // 1 sign bit + 7 exponent bits + 8*(length-1) mantissa bits. Exponent bias 128, Mantissa implied most significant bit= 1, normalised to fractional point to left of most signifcant bit (1> mantissa normalised fraction >=0).

        byte exp;
        if (length <= 0)
        {
            return 0d;
        }

        else if (length == 1)
        {
            exp = (byte)(arry[start] << 1);
            if (exp == 0)
            {
                return 0d;
            }
            else if (exp == 254)
            {
                return 0d;
            }
            else if (arry[start] > 127)
            {
                return -Math.Pow(2d, exp - 128);
            }
            else
            {
                return Math.Pow(2d, exp - 128);
            }
        }
        else
        {
            if (length > 8)
                length = 8;

            // do everything we can as fast bitwise operations
            exp = (byte)((arry[start + 1] << 1) + (arry[start] >> 7));
            ulong mant = (ulong)(arry[start] | 128); // adds the implied leading binary '1'
            for (byte i = (byte)(length - 1); i <= 2; i++)
            {
                mant = mant << 8;
                mant = mant | arry[start + i];
            }

            // combine all the slow floating point arithmatic at the last step
            if (arry[start + 1] > 127)
            {
                return -(double)mant * Math.Pow(2d, exp - 8 * (length - 1) - 128);
            }
            else
            {
                return mant * Math.Pow(2d, exp - 8 * (length - 1) - 128);
            }
        }


    }
}
