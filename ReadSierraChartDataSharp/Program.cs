// this program reads data from a local C:/SierraChart/data directory
// it only reads files which start with ESmyy and have a .scid extension
// these are native binary Sierra Chart data files
// m is a futures contract month code: H, M, U, or Z
// yy is a 2 digit year

// the following struct definitions are taken from the Sierra Chart scdatetime.h file
// Times are in UTC


using System;
using System.IO;
using System.Runtime.InteropServices;

void read_rec(BinaryReader f, ref s_IntradayRecord rec)
{
    rec.DateTime = f.ReadUInt64();
    rec.Open = f.ReadSingle(); 
    rec.High = f.ReadSingle();
    rec.Low = f.ReadSingle();
    rec.Close = f.ReadSingle();
    rec.NumTrades = f.ReadUInt32();
    rec.TotalVolume = f.ReadUInt32();
    rec.BidVolume = f.ReadUInt32();
    rec.AskVolume = f.ReadUInt32();
}

var datafile_dir = "C:/SierraChart/Data/ESZ20.scid";
var datafile_outdir = "C:/Users/lel48/SierraChartData/";
var futures_root = "ES";

var ifr1 = new s_IntradayRecord();
var xx2 = Marshal.SizeOf(typeof(s_IntradayRecord));

var f = File.Open(datafile_dir, FileMode.Open); 
BinaryReader io = new BinaryReader(f);

// skip 56 byte header
io.ReadBytes(56);

var count = 0;
while (io.BaseStream.Position != io.BaseStream.Length)
{
    read_rec(io, ref ifr1);
    count++;
}
var xxx = 1;

struct s_IntradayRecord
{
    internal UInt64 DateTime; // in microseconds
    internal float Open;
    internal float High;
    internal float Low;
    internal float Close;
    internal UInt32 NumTrades;
    internal UInt32 TotalVolume;
    internal UInt32 BidVolume;
    internal UInt32 AskVolume;
}





