//
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
    static internal class Program {
        const string version = "ReadSierraChartDataSharp 0.1.0";
        const string futures_root = "ES";
        const bool update_only = true; // only process .scid files in datafile_dir which do not have counterparts in datafile_outdir

        const string datafile_dir = "C:/SierraChart/Data/";
        const string datafile_outdir = "C:/Users/lel48/SierraChartData/";
        static readonly Dictionary<char, int> futures_codes = new() { { 'H', 3 }, { 'M', 6 }, { 'U', 9 }, { 'Z', 12 } };

        static internal Logger logger = new Logger(datafile_outdir);

        internal enum ReturnCodes {
            Successful,
            Ignored,
            MalformedFuturesFileName,
            IOErrorReadingData
        }

        static int Main(string[] args) {
            if (logger.state != 0)
                return -1;

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            int rc = CommandLine.ProcessCommandLineArguments(args);
            if (rc != 0) 
                return rc;

            string[] filenames = Directory.GetFiles(datafile_dir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);
            Parallel.ForEach(filenames, filename => ProcessScidFile(filename));
            logger.close();

            stopWatch.Stop();
            Console.WriteLine($"Elapsed time = {stopWatch.Elapsed}");

            return 0;
        }

        static int ProcessScidFile(string filepath) {
            string filename = Path.GetFileNameWithoutExtension(filepath);

            // make sure filename has a valid futures code: 'H', 'M', 'U', 'Z'
            char futures_code = filename[futures_root.Length];
            if (!futures_codes.ContainsKey(futures_code))
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);

            // get 4 digit futures year from .scid filename (which has 2 digit year)
            string futures_two_digit_year_str = filename.Substring(futures_root.Length + 1, 2);
            if (!Char.IsDigit(futures_two_digit_year_str[0]) || !Char.IsDigit(futures_two_digit_year_str[1]))
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);
            int futures_year;
            bool parse_suceeded = Int32.TryParse(futures_two_digit_year_str, out futures_year);
            if (!parse_suceeded)
                return logger.log((int)ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + filepath);
            futures_year += 2000;

            // get filenames for temporary .csv output file and final .zip file
            string out_fn_base = futures_root + futures_code + futures_two_digit_year_str;
            string out_path = datafile_outdir + out_fn_base;
            string out_path_csv = out_path + ".csv"; // full path
            string out_path_zip = out_path + ".zip"; // full path

            // if update_only is true and file already exists in datafile_outdir, ignore it
            if (update_only) {
                if (File.Exists(out_path_zip))
                    return logger.log((int)ReturnCodes.Ignored, "Update only mode; file ignored: " + filepath);
            }
            Console.WriteLine("Processing " + filepath);

            // get start_year, start_month, end_year, end_month of futures contract data we want to keep
            (int start_year, int start_month, int end_year, int end_month) = getFuturesStartEndDates(futures_code, futures_year);

            // only keep ticks between start_date and end_date. Kind is unspecified since it IS NOT Local...it is US/Eastern
            DateTime start_dt = new DateTime(start_year, start_month, 9, 18, 0, 0, DateTimeKind.Unspecified);
            DateTime end_dt = new DateTime(end_year, end_month, 9, 18, 0, 0, DateTimeKind.Unspecified);

            using (var f = File.Open(filepath, FileMode.Open, FileAccess.Read)) {
                using (StreamWriter writer = new StreamWriter(out_path_csv)) {
                    var ihr = new Scid.s_IntradayFileHeader();
                    var ir = new Scid.s_IntradayRecord();
                    BinaryReader io = new BinaryReader(f);

                    // skip 56 byte header
                    ihr.Read(io);
                    Debug.Assert(ihr.RecordSize == Marshal.SizeOf(typeof(Scid.s_IntradayRecord)));

                    string prev_ts = "";
                    while (io.BaseStream.Position != io.BaseStream.Length) {
                        // read a Sierra Chart tick record
                        if (!ir.Read(io)) {
                            Console.WriteLine("IO Error reading data: " + filepath);
                            return logger.log((int)ReturnCodes.IOErrorReadingData, "IO Error: " + filepath);
                        }

                        // convert UTC SCDateTime to C# DateTime in Eastern US timezone
                        DateTime dt_et = Scid.GetEasternDateTimeFromSCDateTime(ir.SCDateTime);

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

                File.Delete(out_path_zip); // needed or ZipFile.Open with ZipArchiveMode.Create could fail
                using (ZipArchive archive = ZipFile.Open(out_path_zip, ZipArchiveMode.Create)) {
                    archive.CreateEntryFromFile(out_path_csv, Path.GetFileName(out_path_csv));
                    File.Delete(out_path_csv);
                }

                return logger.log((int)ReturnCodes.Successful, out_path_zip + " created.");
            }
        }

        // get the months and years of the 3 months of futures data we want
        static (int start_year, int start_month, int end_year, int end_month) getFuturesStartEndDates(char futures_code, int futures_year) {
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

            return (start_year, start_month, end_year, end_month);
        }
    }
}





