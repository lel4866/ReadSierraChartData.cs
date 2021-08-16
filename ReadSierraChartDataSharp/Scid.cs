using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// this class defines the Sierra Chart Intraday data class which implements reading of Sierra Chart intraday data
// Sierra Chart .scid files are binary files that consist of a header (s_IntradayFileHeader) followed by a a number
// of records (s_IntradayRecord)
// The structure definitions are based on the Sierra Chart IntradayRecord.h header file

namespace ReadSierraChartDataSharp {
    internal class Scid {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        internal struct s_IntradayFileHeader {
            const UInt32 UNIQUE_HEADER_ID = 0x44494353;  // "SCID"

            internal UInt32 FileTypeUniqueHeaderID;  // "SCID"
            internal UInt32 HeaderSize;
            internal UInt32 RecordSize;
            internal UInt16 Version;

            internal void Read(BinaryReader f) {
                FileTypeUniqueHeaderID = f.ReadUInt32();
                HeaderSize = f.ReadUInt32();
                RecordSize = f.ReadUInt32();
                Version = f.ReadUInt16();
            }
        }

        // constructed from Sierra Charts IntradayRecord.h file
        internal struct s_IntradayRecord {
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
                    return false;
                }

                return true;
            }
        }
    }
}
