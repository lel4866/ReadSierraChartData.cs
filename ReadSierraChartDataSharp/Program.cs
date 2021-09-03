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

namespace ReadSierraChartDataSharp;

// warnings are greater than 0, errors are less than 0
enum ReturnCodes {
    Successful = 0,
    FileIgnored = 1,
    MalformedFuturesFileName = -1,
    IOErrorReadingData = -2
}

// reurns 0 if everything went OK, except for warnings, -1 if there were errors that needed attention (bad parameters, IO errors)
static class Program {
    const string output_time_zone = "ISODateTime(Eastern/US)";
    internal const string version = "ReadSierraChartDataSharp 0.1.0";
    internal static string futures_root = "ES";
    internal static bool update_only = true; // only process .scid files in datafile_dir which do not have counterparts in datafile_outdir

    const string datafile_dir = "C:/SierraChart/Data/";
    const string datafile_outdir = "C:/Users/lel48/SierraChartData/";
    static readonly Dictionary<char, int> futures_codes = new() { { 'H', 3 }, { 'M', 6 }, { 'U', 9 }, { 'Z', 12 } };

    static readonly TimeSpan four_thirty_pm = new(16, 30, 0); // session end (Eastern/US)
    static readonly TimeSpan six_pm = new(18, 0, 0); // session start (Eastern/US)

    static internal Logger logger = new(datafile_outdir); // this could call System.Environment.Exit
    static int return_code = 0;

    static HashSet<DateTime> Holidays = new();

    static int Main(string[] args) {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        CommandLine.ProcessCommandLineArguments(args); // calls System.Environment.Exit(-1) if bad command line arguments

        ReadMarketHolidays();

        try {
            string[] filenames = Directory.GetFiles(datafile_dir, futures_root + "*.scid", SearchOption.TopDirectoryOnly);
            Parallel.ForEach(filenames, filename => ProcessScidFile(filename));
        }
        finally {
            logger.close();
        }

        stopWatch.Stop();
        Console.WriteLine($"Elapsed time = {stopWatch.Elapsed}");

        return return_code;
    }

    // returns 0 if (success OR FileIgnored due to update_only mode), -1 for malformed file names, IO error 
    // also sets global return_code to -1 if return value is -1
    static int ProcessScidFile(string filepath) {
        // fn_base is futures file name without preceeding path or extension (i.e. "ESH20")
        string fn_base = Path.GetFileNameWithoutExtension(filepath);
        if (ValidateFuturesFilename(fn_base, out int futures_year, out char futures_code) != 0)
            return -1;

        // get filenames for temporary .csv output file and final .zip file
        string out_path = datafile_outdir + fn_base;
        string out_path_csv = out_path + ".csv"; // full path
        string out_path_zip = out_path + ".zip"; // full path

        // if update_only is true and file already exists in datafile_outdir, ignore it
        if (update_only) {
            if (File.Exists(out_path_zip))
                return log(ReturnCodes.FileIgnored, "Update only mode; file ignored: " + filepath);
        }
        Console.WriteLine("Processing " + filepath);

        // get start_year, start_month, end_year, end_month of futures contract data we want to keep
        (int start_year, int start_month, int end_year, int end_month) = getFuturesStartEndDates(futures_code, futures_year);

        // only keep ticks between start_date and end_date. Kind is unspecified since it IS NOT Local...it is US/Eastern
        DateTime start_dt = new DateTime(start_year, start_month, 9, 18, 0, 0, DateTimeKind.Unspecified);
        DateTime end_dt = new DateTime(end_year, end_month, 9, 18, 0, 0, DateTimeKind.Unspecified);

        var ihr = new s_IntradayFileHeader();
        var ir = new s_IntradayRecord();

        using (BinaryReader io = new BinaryReader(File.Open(filepath, FileMode.Open, FileAccess.Read))) {
            // skip 56 byte header
            if (!ihr.Read(io))
                return -1;
            Debug.Assert(ihr.RecordSize == Marshal.SizeOf(typeof(s_IntradayRecord)));

            using (StreamWriter writer = new StreamWriter(out_path_csv)) {
                // write header
                writer.WriteLine($"{output_time_zone},Close,BidVolume,AskVolume");

                string prev_ts = "";
                while (io.BaseStream.Position != io.BaseStream.Length) {
                    // read a Sierra Chart tick record
                    if (!ir.Read(io))
                        return -1;

                    // convert UTC SCDateTime to C# DateTime in Eastern US timezone
                    DateTime dt_et = Scid.GetEasternDateTimeFromSCDateTime(ir.SCDateTime);

                    // only keep ticks between specified start and end date/times...that is, for the 3 "active" months
                    if (dt_et < start_dt)
                        continue;
                    if (dt_et >= end_dt)
                        break;

                    // throw away ticks before 6pm if Saturday, Sunday, or holiday)
                    // throw away ticks after 4:30p if next day is not trading day (Saturday, Sunday, holiday)
                    if (!IsValidTickTime(dt_et))
                        continue;

                    // only keep 1 tick for each second
                    // note...using "s" is twice as fast as using a custom format string
                    string ts = dt_et.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                    if (ts == prev_ts)
                        continue;
                    prev_ts = ts;

                    // convert tick tuple to string
                    writer.WriteLine($"{ts},{ir.Close:F2},{ir.BidVolume},{ir.AskVolume}");
                }
            }
        }

        // create output zip file and add csv created above to it
        File.Delete(out_path_zip); // in case zio file already exists...needed or ZipFile.Open with ZipArchiveMode.Create could fail
        using (ZipArchive archive = ZipFile.Open(out_path_zip, ZipArchiveMode.Create)) {
            archive.CreateEntryFromFile(out_path_csv, Path.GetFileName(out_path_csv));
        }
        File.Delete(out_path_csv); // delete csv file

        return log(ReturnCodes.Successful, out_path_zip + " created.");
    }

    // make sure filename is of form: {futures_root}{month_code}{2 digit year}
    static int ValidateFuturesFilename(string fn_base, out int futures_year, out char futures_code) {
        futures_year = 0;

        // make sure filename has a valid futures code: 'H', 'M', 'U', 'Z'
        futures_code = fn_base[futures_root.Length];
        if (!futures_codes.ContainsKey(futures_code))
            return log(ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + fn_base + ".scid");

        // get 4 digit futures year from .scid filename (which has 2 digit year)
        string futures_two_digit_year_str = fn_base.Substring(futures_root.Length + 1, 2);
        if (!Char.IsDigit(futures_two_digit_year_str[0]) || !Char.IsDigit(futures_two_digit_year_str[1]))
            return log(ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + fn_base + ".scid");

        bool parse_suceeded = Int32.TryParse(futures_two_digit_year_str, out futures_year);
        if (!parse_suceeded)
            return log(ReturnCodes.MalformedFuturesFileName, "Malformed futures file name: " + fn_base + ".scid");
        futures_year += 2000;
        return 0;
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

    // throw away ticks before 6pm if Saturday, Sunday, or holiday)
    // throw away ticks after 4:30p if next day is not trading day (Saturday, Sunday, holiday)
    static bool IsValidTickTime(DateTime dt) {
        bool holiday = IsMarketHoliday(dt);
        DateTime next_day = dt.AddDays(1);
        bool next_day_is_holiday = IsMarketHoliday(next_day);

        if (holiday) {
            if (dt.TimeOfDay < six_pm)
                return false;
            if (next_day_is_holiday)
                return false;
        }

        if (next_day_is_holiday)
            if (dt.TimeOfDay >= four_thirty_pm)
                return false;

        return true;
    }

    static void ReadMarketHolidays() {
        DateTime prev_dt = new();
        string[] lines = System.IO.File.ReadAllLines("../../../../MarketHolidays.txt");
        foreach (string line in lines) {
            string tline = line.Trim();
            if (tline.Length == 0)
                continue;
            bool rc = DateTime.TryParse(tline, out DateTime dt);
            if (!rc) {
                Console.WriteLine("Invalid date in ReadMarketHolidays().txt: " + line);
                System.Environment.Exit(-1);
            }
            if (dt < prev_dt) {
                Console.WriteLine("Out of order date in ReadMarketHolidays().txt: " + line);
                System.Environment.Exit(-1);
            }
            if (Holidays.Contains(dt)) {
                Console.WriteLine("Duplicate date in ReadMarketHolidays().txt: " + line);
                System.Environment.Exit(-1);
            }

            Holidays.Add(dt.Date);
            prev_dt = dt;
        }
    }

    // This also tries to exclude days that are half days due to next day being a holiday
    static bool IsMarketHoliday(DateTime dt) {
        if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
            return true;
        return Holidays.Contains(dt.Date);
    }

    // thread safe setting of return_code when logging
    static int log(ReturnCodes code, string message) {
        logger.log(code, message);
        int rc = code < 0 ? -1 : 0;
        if (rc < 0)
            Interlocked.Exchange(ref return_code, rc);
        return rc;
    }
}





