// this program reads data from a local C:/SierraChart/data directory
// it only reads files which start with ESmyy and have a .scid extension
// these are native binary Sierra Chart data files
// m is a futures contract month code: H, M, U, or Z
// yy is a 2 digit year

// the following struct definitions are taken from the Sierra Chart scdatetime.h file
// Times are in UTC

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace ReadSierraChartDataSharp {
    class Program {
        const string datafile_dir = "C:/SierraChart/Data/";
        const string datafile_outdir = "C:/Users/lel48/SierraChartData/";
        const string futures_root = "ES";
        static readonly Dictionary<char, int> futures_codes = new() { { 'H', 3 }, { 'M', 6 }, { 'U', 9 }, { 'Z', 12 } };
        static readonly DateTime SCDateTimeEpoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc); // Sierra Chart SCDateTime start
        static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("US Eastern Standard Time");

        // constructed from Sierra Charts IntradayRecord.h file
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct s_IntradayFileHeader {
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
                    return false;
                }

                return true;
            }
        }

        enum ReturnCodes {
            Successful,
            MalformedFuturesFileName,
            IOErrorReadingData
        }

        static int Main(string[] args) {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var logger = new Logger(datafile_outdir);
            if (logger.state != 0) {
                Console.WriteLine("Unable to creat Logger in directory:" + datafile_outdir);
                return -1;
            }

            string[] filenames = Directory.GetFiles(datafile_dir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);
            string[] existing_filenames = Directory.GetFiles(datafile_outdir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);
            Parallel.ForEach(filenames, filename => ProcessScidFile(futures_root, filename, logger));
            logger.close();

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time = {stopWatch.Elapsed}");

            return 0;
        }

        static int ProcessScidFile(string futures_root, string filepath, Logger logger) {
            Console.WriteLine("Processing " + filepath);
            string filename = Path.GetFileNameWithoutExtension(filepath);
            char futures_code = filename[futures_root.Length];
            if (!futures_codes.ContainsKey(futures_code))
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);

            string futures_two_digit_year_str = filename.Substring(futures_root.Length + 1, 2);
            if (!Char.IsDigit(futures_two_digit_year_str[0]) || !Char.IsDigit(futures_two_digit_year_str[1]))
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);

            int futures_year;
            bool parse_suceeded = Int32.TryParse(futures_two_digit_year_str, out futures_year);
            if (!parse_suceeded)
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);
            futures_year += 2000;

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

            string out_fn_base = futures_root + futures_code + futures_two_digit_year_str;
            string out_path = datafile_outdir + out_fn_base;
            string out_path_csv = out_path + ".csv"; // full path

            // only keep ticks between start_date and end_date. Kind is unspecified since it IS NOT Local...it is US/Eastern
            DateTime start_dt = new DateTime(start_year, start_month, 9, 18, 0, 0, DateTimeKind.Unspecified);
            DateTime end_dt = new DateTime(end_year, end_month, 9, 18, 0, 0, DateTimeKind.Unspecified);

            var ihr = new s_IntradayFileHeader();
            var ihr_size = Marshal.SizeOf(typeof(s_IntradayFileHeader));

            var ir = new s_IntradayRecord();
            using (var f = File.Open(filepath, FileMode.Open, FileAccess.Read)) {
                using (StreamWriter writer = new StreamWriter(out_path_csv)) {
                    BinaryReader io = new BinaryReader(f);

                    // skip 56 byte header
                    ihr.Read(io);
                    Debug.Assert(ihr.RecordSize == Marshal.SizeOf(typeof(s_IntradayRecord)));

                    int remaining_bytes = (int)ihr.HeaderSize - ihr_size;
                    try {
                        io.ReadBytes(remaining_bytes);
                    }
                    catch (IOException) {
                        Console.WriteLine("IO Error reading header: " + filepath);
                        return logger.log((int)ReturnCodes.IOErrorReadingData, "IO Error: " + filepath);
                    }

                    string prev_ts = "";
                    while (io.BaseStream.Position != io.BaseStream.Length) {
                        if (!ir.Read(io)) {
                            Console.WriteLine("IO Error reading data: " + filepath);
                            return logger.log((int)ReturnCodes.IOErrorReadingData, "IO Error: " + filepath);
                        }

                        // convert UTC SCDateTime to C# DateTime in Eastern US timezone
                        DateTime dt_et = GetEasternDateTimeFromSCDateTime(ir.SCDateTime);

                        // only keep ticks between specified start and end date/times...that is, for the 3 "active" months
                        if (dt_et < start_dt)
                            continue;
                        if (dt_et >= end_dt)
                            break;

                        // only keep 1 tick for each second
                        // note...using "s" is twice as fast as using a custom format string
                        string ts = dt_et.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                        if (ts == prev_ts)
                            continue;
                        prev_ts = ts;

                        // convert tick tuple to string
                        writer.WriteLine($"{ts},{ir.Close:F2}");
                    }
                }

                string out_path_zip = Path.ChangeExtension(out_path_csv, ".zip");
                File.Delete(out_path_zip); // needed or ZipFile.Open with ZipArchiveMode.Create could fail
                using (ZipArchive archive = ZipFile.Open(out_path_zip, ZipArchiveMode.Create)) {
                    archive.CreateEntryFromFile(out_path_csv, Path.GetFileName(out_path_csv));
                    File.Delete(out_path_csv);
                }

                return logger.log((int)ReturnCodes.Successful, filepath + " created.");
            }
        }

        // convert from Sierra Chart DateTime to C# DateTime in Easter US timezone
        static DateTime GetEasternDateTimeFromSCDateTime(Int64 scdt) {
            // SCDateTime is in microseconds (since 12/30/1899); C# DateTime is in 100 nanoseconds
            DateTime utc = SCDateTimeEpoch.AddTicks(scdt * 10L);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, EasternTimeZone);
        }
    }
}





