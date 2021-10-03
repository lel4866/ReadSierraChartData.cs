// this class defines the Sierra Chart Intraday data class which implements reading of Sierra Chart intraday data
// Sierra Chart .scid files are binary files that consist of a header (s_IntradayFileHeader) followed by a a number
// of records (s_IntradayRecord)
// The structure definitions are based on the Sierra Chart IntradayRecord.h header file

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ReadSierraChartDataSharp;

class Scid {
    static readonly DateTime SCDateTimeEpoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc); // Sierra Chart SCDateTime start
    static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("US Eastern Standard Time");

    // convert from Sierra Chart DateTime to C# DateTime in Easter US timezone
    internal static DateTime GetEasternDateTimeFromSCDateTime(Int64 scdt) {
        // SCDateTime is in microseconds (since 12/30/1899); C# DateTime is in 100 nanoseconds
        DateTime utc = SCDateTimeEpoch.AddTicks(scdt * 10L);
        int a = Marshal.SizeOf(typeof(s_IntradayFileHeader));
        return TimeZoneInfo.ConvertTimeFromUtc(utc, EasternTimeZone);
    }
}

// constructed from Sierra Charts IntradayRecord.h file
[StructLayout(LayoutKind.Sequential, Pack = 2)]
struct s_IntradayFileHeader {
    const UInt32 UNIQUE_HEADER_ID = 0x44494353;  // "SCID"

    internal UInt32 FileTypeUniqueHeaderID;  // "SCID"
    internal UInt32 HeaderSize;
    internal UInt32 RecordSize;
    internal UInt16 Version;
    // there's 24 more unused bytes after Version

    internal bool Read(BinaryReader f) {
        FileTypeUniqueHeaderID = f.ReadUInt32();
        HeaderSize = f.ReadUInt32();
        RecordSize = f.ReadUInt32();
        Version = f.ReadUInt16();

        // skip remaining bytes of header
        int remaining_bytes = (int)HeaderSize - Marshal.SizeOf(typeof(s_IntradayFileHeader));
        try {
            f.ReadBytes(remaining_bytes);
        }
        catch (IOException) {
            Console.WriteLine("IO Error reading header: " + f.ToString());
            Program.logger?.log(ReturnCodes.IOErrorReadingData, "IO Error: " + f.ToString());
            return false;
        }

        return true;
    }
}

// constructed from Sierra Charts IntradayRecord.h file
struct s_IntradayRecord {
    internal Int64 SCDateTime; // in microseconds since 1899-12-30 00:00:00 UTC
    internal float Open;
    internal float High;
    internal float Low;
    internal float Close;
    internal UInt32 NumTrades;
    internal UInt32 TotalVolume;
    internal UInt32 BidVolume;
    internal UInt32 AskVolume;

    internal bool Read(BinaryReader f) {
        try {
            SCDateTime = f.ReadInt64();
            Open = f.ReadSingle();
            High = f.ReadSingle();
            Low = f.ReadSingle();
            Close = f.ReadSingle();
            NumTrades = f.ReadUInt32();
            TotalVolume = f.ReadUInt32();
            BidVolume = f.ReadUInt32();
            AskVolume = f.ReadUInt32();
        }
        catch (IOException) {
            Console.WriteLine("IO Error reading header: " + f.ToString());
            Program.logger?.log(ReturnCodes.IOErrorReadingData, "IO Error: " + f.ToString());
            return false;
        }

        return true;
    }
}
