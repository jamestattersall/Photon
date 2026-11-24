using ProtonConsole2.Proton;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace ProtonConsole2.Proton
{
    interface IProtonBase
    {
        static string? ProtonFolderPath { get;set; }
        static Dictionary<string, short> PageLengths { get; } = [];
    }

    public static class ProtonBase 
    {
        public static string? ProtonFolderPath { get; private set; }
        public static Dictionary<string, short> PageLengths { get; } = [];
        public static void SetProtonBase(string pathToBinaries)
        {
            ProtonFolderPath = pathToBinaries;

            using Base _base = new();

            PageLengths.Clear();

            for (int i = 1; i <= _base.NPages; i++)
            {
                if (_base.MoveToPage(i))
                {
                    PageLengths.Add(_base.Name, _base.DbPageLength);
                }
            }
        }
    }


    public abstract class ProtonDbFileReader : IDisposable
    {
        private readonly FileStream _fileStream;

        private bool _disposedValue;

        private readonly byte[] _pageBuffer;

        protected uint PagePtr { get; private set; }

        protected short PageLength { get; private set; }

        protected ReadOnlyMemory<byte> PageMemory;

        protected readonly static IPrimativeIntConverter IntConverter = new BigEndianIntegerPrimitive();

        private readonly static IPrimativeFloatConverter FloatConverter = new BigEndianFloatPrimative();

        protected const byte NULL = 0;

        
        public ProtonDbFileReader(string dbName, short pageLength = 0)
        {
            if (pageLength == 0)
            {
                PageLength = ProtonBase.PageLengths[dbName];
            }
            else
            {
                PageLength = pageLength;
            }

            if (ProtonBase.ProtonFolderPath is null)
                throw new InvalidOperationException("ProtonBase.ProtonFolderPath must be set before creating a ProtonDbFileReader.");

            _fileStream = new FileStream(Path.Combine(ProtonBase.ProtonFolderPath, dbName), FileMode.Open, FileAccess.Read, FileShare.Read, pageLength, false);
            FileLength = _fileStream.Length;
            NPages = (int)FileLength/PageLength;
            _pageBuffer = new byte[PageLength];
            DbName = dbName;
        }


        public bool ExamineBit(int offset, int index) => (PageMemory.Span[offset] & (1 << (index - 1))) != 0x0;

        public bool PageIsValid => PageMemory.Length > 0 && PageMemory.Span.ContainsAnyExcept(NULL, (byte)0);

        protected long FileLength { get; private set; }

        public int NPages { get; private set; }

        public string DbName { get; private set; }

        public DateTime GetFileDate()
        {
           return File.GetLastWriteTime(ProtonBase.ProtonFolderPath + DbName);
        }

        public virtual bool MoveToPage(int pagePtr)
        {
            //in theory, Proton metatdata int (Int32) ID could be > int.MaxValue as pointers to these are stored as 4 bytes and interpreted as unsigned
            //in this unlikely case, they will have been read as negative signed int, must be converted to uint UInt(32). 

            return MoveToPage((uint)(pagePtr < 0 ? Unsafe.As<int, uint>(ref pagePtr) : (uint)pagePtr));
        }

        public virtual bool MoveToPage(short pagePtr)
        {
            //in theory, Proton metatdata short (Int16) ID could be > short.MaxValue as pointers to these are stored as 2 bytes and interpreted as unsigned
            //in this unlikely case, they will have been read as negative signed short, must be converted to ushort (UInt16). 

            return MoveToPage((uint)(pagePtr < 0 ? Unsafe.As<short, ushort>(ref pagePtr): (uint)pagePtr));
        }

        public virtual bool MoveToPage(uint pagePtr)
        {
            if (pagePtr == 0 || pagePtr > NPages || PageLength == 0)
            {
                return false;
            }
            else
            {
                if (PagePtr != pagePtr)
                {

                    SetPage((pagePtr - 1) * PageLength);

                    PagePtr = pagePtr;
                }
                return PageIsValid;
            }
        }
        

        private void SetPage(long target)
        {
            _fileStream.Seek(target, SeekOrigin.Begin);

            _fileStream.ReadExactly(_pageBuffer);

            PageMemory = new ReadOnlyMemory<byte>(_pageBuffer);
        }

        public static string GetString(ReadOnlyMemory<byte> mem)
        {
            int l = mem.Span.LastIndexOfAnyExcept(NULL) + 1;
            if (l > 0) { return Encoding.ASCII.GetString(mem[..l].Span); }
            return string.Empty;
        }

        public string GetString(int offset, int length) => GetString(PageMemory.Slice(offset, length));

        public static sbyte GetInt8(ReadOnlyMemory<byte> mem) => (sbyte)mem.Span[0];
        public static byte GetUInt8(ReadOnlyMemory<byte> mem) => mem.Span[0];
        public static ushort GetUInt16(ReadOnlyMemory<byte> mem) => IntConverter.UInt16Converter(mem);
        public static UInt32 GetUInt32(ReadOnlyMemory<byte> mem) => IntConverter.UInt32Converter(mem);
        public static short GetInt16(ReadOnlyMemory<byte> mem) => IntConverter.Int16Converter(mem);
        public static Int32 GetInt32(ReadOnlyMemory<byte> mem) => IntConverter.Int32Converter(mem);  
        public static float GetDouble(ReadOnlyMemory<byte> mem) => FloatConverter.DoubleConverter(mem);
        public static float GetSingle(ReadOnlyMemory<byte> mem) => FloatConverter.SingleConverter(mem);

        public sbyte GetInt8(int offset) => GetInt8(PageMemory.Slice(offset, 1));
        public byte GetUInt8(int offset) => GetUInt8(PageMemory.Slice(offset, 1));
        public ushort GetUInt16(int Offset) => GetUInt16(PageMemory.Slice(Offset,2));
        public UInt32 GetUInt32(int Offset) => GetUInt32(PageMemory.Slice(Offset, 4));
        public short GetInt16(int Offset) =>  GetInt16(PageMemory.Slice(Offset, 2));
        public Int32 GetInt32(int Offset) => GetInt32(PageMemory.Slice(Offset, 4));
        public float GetDouble(int Offset) => (float)FloatConverter.DoubleConverter(PageMemory.Slice(Offset,8));
        public float GetSingle(int Offset) => FloatConverter.SingleConverter(PageMemory.Slice(Offset, 4));

        public static readonly DateOnly ProtonBaseDate = DateOnly.Parse("1860-01-01");
        public static readonly DateTime ProtonBaseDateTime = DateTime.Parse("1860-01-01");
        public static readonly SqlDateTime ProtonSqlBaseDate = SqlDateTime.Parse("1860-01-01");


        public static DateOnly GetDate(ReadOnlyMemory<byte> mem) => ProtonBaseDate.AddDays(GetUInt16(mem));
        public static TimeOnly GetTime(ReadOnlyMemory<byte> mem) => TimeOnly.MinValue.Add(new TimeSpan(0, 0, 0, 0,  GetInt32(mem)));
        public static DateTime GetDateTime(ReadOnlyMemory<byte> mem) => ProtonBaseDateTime.AddDays(GetUInt16(mem));

        public DateOnly  GetDate(short offset) => GetDate(PageMemory.Slice(offset, 2));
        public TimeOnly GetTime(short offset) => GetTime(PageMemory.Slice(offset, 4));
        public DateTime GetDateTime(short offset) => GetDateTime(PageMemory.Slice(offset, 4));

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _fileStream.Close();
                    _fileStream.Dispose();
                   

                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                PageMemory = null;

                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ProtonDbFileReader()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public abstract class ProtonDbFileReaderExt(string filename, short headerLength, short blockLength = 0) : ProtonDbFileReader(filename)
    {
        protected int BlockLength { get; set; } = blockLength;
        protected readonly short HeaderLength = headerLength;
        protected int Ptr;
        protected bool _hasReset = true;

        public int PageCounter { get; private set; } = 0;

        public void PageCounterReset()
        {
            PageCounter = 0;
        }

        public override bool MoveToPage(int PagePtr)
        {
            if (base.MoveToPage(PagePtr))
            {
                MoveToFirstBlock();
                PageCounter++;
                return true;
            }
            return false;
        }

        public override bool MoveToPage(short PagePtr)
        {
            if (base.MoveToPage(PagePtr))
            {
                MoveToFirstBlock();
                PageCounter++;
                return true;
            }
            return false;
        }

        public void MoveToFirstBlock()
        {
            _hasReset = true;
            Ptr = HeaderLength;
        }

        public virtual bool MoveToNextBlock()
        {
            if (_hasReset)
            {
                _hasReset = false;
                 return true;
            }
            if (Ptr + BlockLength + BlockLength > PageLength) return false;
            else Ptr += BlockLength;
            return true;
        }
    }
}
