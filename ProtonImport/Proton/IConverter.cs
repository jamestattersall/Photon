using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.Proton
{
    public interface IPrimativeIntConverter
    {
        bool IsBigEndian { get; }

        ushort UInt16Converter(ReadOnlyMemory<byte> memory);

        short Int16Converter(ReadOnlyMemory<byte> memory);

        UInt32 UInt32Converter(ReadOnlyMemory<byte> memory);

        Int32 Int32Converter(ReadOnlyMemory<byte> memory);

    }


    public interface IPrimativeFloatConverter
    {
        bool IsBigEndian { get; }

        float SingleConverter(ReadOnlyMemory<byte> memory);

        float DoubleConverter(ReadOnlyMemory<byte> memory);

    }

    /// <summary>
    /// functions to convert integers from portions of byte arrays 
    /// </summary>
    public interface IProtonBinaryToInteger
    {
        //integers in Proton .DBS files are byte arrays 1-4 bytes long and may be signed or unsigned
        //4 byte unsigned bytes need to be converted to long (8-byte) as Windows has no underlying type for unsigned uint  
        //In DATA.dbs block data and page numbers could be 4-byte unsigned.
        //Number of pages in DATA.dbs could exceeed max for signed 4-byte integer,

        bool IsBigEndian { get; }

        /// <summary>
        /// converts byte array to 64-bit integer (windows long) which is always positive
        /// required for page ID in DATA.DBS  
        /// </summary>
        /// <param name="arry">the byte array</param>
        /// <param name="start">the start of sequence</param>
        /// <param name="length">number of bytes to convert (1-4)</param>
        uint BinaryToUInt(ReadOnlyMemory<byte> pageMemory, uint start, uint length);
        uint BinaryToUInt(ReadOnlyMemory<byte> pageMemory, uint start, uint length, uint inputBytes);

        /// <summary>
        /// converts byte array to 32-bit signed integer
        /// </summary>
        /// <param name="arry">the byte array</param>
        /// <param name="start">the start of sequence</param>
        /// <param name="length">number of bytes to convert (1-4)</param>

        uint BinaryToInt(ReadOnlyMemory<byte> pageMemory, uint start, uint length);
        uint BinaryToInt(ReadOnlyMemory<byte> pageMemory, uint start, uint length, uint inputBytes);

    }

    /// <summary>
    /// Functions to convert portion of byte array to floating point number
    /// </summary>
    public  interface IProtonBinaryToFloat
    {
        /// <summary>
        /// byte array to float
        /// </summary>
        /// <param Name="arry">Byte array</param>
        /// <param Name="start">ordinal position of first byte to convert </param>
        /// <param Name="length">Nunber of bytes to convert</param>
        /// <returns></returns>
        float BinaryToSingle(ReadOnlyMemory<byte> arry, uint start, uint inputBytes);

        /// <summary>
        /// byte array to double
        /// </summary>
        /// <param Name="arry">Byte array</param>
        /// <param Name="start">ordinal position of first byte to convert</param>
        /// <param Name="length">Nunber of bytes to convert</param>
        /// <returns></returns>
        double BinaryToDouble(ReadOnlyMemory<byte> arry, uint start, uint inputBytes);

        /// <summary>
        /// returns true if endianness is big endian
        /// </summary>
        bool IsBigEndian { get; }
    }
}

