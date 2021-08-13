// this program reads data from a local C:/SierraChart/data directory
// it only reads files which start with ESmyy and have a .scid extension
// these are native binary Sierra Chart data files
// m is a futures contract month code: H, M, U, or Z
// yy is a 2 digit year

// the following struct definitions are taken from the Sierra Chart scdatetime.h file
// Times are in UTC


using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

const string datafile_dir = "C:/SierraChart/Data/";
const string datafile_outdir = "C:/Users/lel48/SierraChartData/";
const string futures_root = "ES";
Dictionary<char, int> futures_codes = new() { { 'H', 3 }, { 'M', 6 }, { 'U', 9 }, { 'Z', 12 } };

string[] filenames = Directory.GetFiles(datafile_dir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);
string[] existing_filenames = Directory.GetFiles(datafile_outdir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);

foreach (string filename in filenames) {
    processScidFile(futures_root, filename);
}

void processScidFile(string futures_root, string filepath) {

    string filename = Path.GetFileName(filepath);
    char futures_code = filename[futures_root.Length - 1];
    if (!futures_codes.ContainsKey(futures_code))
        return;
    string futures_two_digit_year_str = filename.Substring(futures_root.Length, 2);
    if (!Char.IsDigit(futures_two_digit_year_str[0]) || !Char.IsDigit(futures_two_digit_year_str[1]))
        return;
    var futures_year = 2000 + Int32.Parse(futures_two_digit_year_str);

    int end_month = futures_codes[futures_code];
    int start_month = end_month - 3;
    int start_year, end_year;
    start_year = end_year = futures_year;
    switch (futures_code) {
        case 'H':
            start_month = 12;
            start_year = end_year - 1;
            break;
        case 'Z':
            end_month = 3;
            end_year++;
            break;
    }

    string out_path = datafile_outdir + futures_root + futures_code + futures_two_digit_year_str + ".zip";

    var ihr = new s_IntradayFileHeader();
    var ihr_size = Marshal.SizeOf(typeof(s_IntradayFileHeader));

    var ifr = new s_IntradayRecord();
    using (var f = File.Open(filename, FileMode.Open)) {
        BinaryReader io = new BinaryReader(f);

        // skip 56 byte header
        ihr.read(io);
        Debug.Assert(ihr.RecordSize == Marshal.SizeOf(typeof(s_IntradayRecord)));

        int remaining_bytes = (int)ihr.HeaderSize - ihr_size;
        io.ReadBytes(remaining_bytes);

        var count = 0;
        while (io.BaseStream.Position != io.BaseStream.Length) {
            ifr.read(io);
            count++;
        }
        var xxx = 1;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
struct s_IntradayFileHeader {
    const UInt32 UNIQUE_HEADER_ID = 0x44494353;  // "SCID"

    internal UInt32 FileTypeUniqueHeaderID;  // "SCID"
    internal UInt32 HeaderSize;
    internal UInt32 RecordSize;
    internal UInt16 Version;

    internal void read(BinaryReader f) {
        FileTypeUniqueHeaderID = f.ReadUInt32();
        HeaderSize = f.ReadUInt32();
        RecordSize = f.ReadUInt32();
        Version = f.ReadUInt16();
    }
}

struct s_IntradayRecord {
    internal UInt64 DateTime; // in microseconds
    internal float Open;
    internal float High;
    internal float Low;
    internal float Close;
    internal UInt32 NumTrades;
    internal UInt32 TotalVolume;
    internal UInt32 BidVolume;
    internal UInt32 AskVolume;

    internal void read(BinaryReader f) {
        DateTime = f.ReadUInt64();
        Open = f.ReadSingle();
        High = f.ReadSingle();
        Low = f.ReadSingle();
        Close = f.ReadSingle();
        NumTrades = f.ReadUInt32();
        TotalVolume = f.ReadUInt32();
        BidVolume = f.ReadUInt32();
        AskVolume = f.ReadUInt32();
    }
}





