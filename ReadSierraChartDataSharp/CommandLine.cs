﻿//
// static class CommandLine
// Proess ReadSierraChartDataSharp command line
//

namespace ReadSierraChartDataSharp {
    static class CommandLine {
        internal static void ProcessCommandLineArguments(string[] args) {
            int rc = 0;
            string? arg_name = null;

            foreach (string arg in args) {
                if (arg_name == null) {
                    switch (arg) {
                        case "-v":
                        case "--version":
                            Console.WriteLine(Program.version);
                            break;
                        case "-r":
                        case "--replace":
                            Program.update_only = false;
                            Console.WriteLine(Program.version);
                            break;
                        case "-s":
                        case "--symbol":
                            arg_name = "-s";
                            break;
                        case "-h":
                        case "--help":
                            Console.WriteLine(Program.version);
                            Console.WriteLine("Convert Sierra Chart .scid files into compressed zip files with 3 months data.");
                            Console.WriteLine("Command line arguments:");
                            Console.WriteLine("    --version, -v : display version number");
                            Console.WriteLine("    --update, -u  : only process files input directory which do not have corresponding file in output directory");
                            Console.WriteLine("    --symbol, -s  : futures contract symbol; i.e. for CME SP500 e-mini: ES");
                            rc = 1;
                            break;

                        default:
                            Console.WriteLine("Invalid command line argument: " + arg);
                            System.Environment.Exit(-1);
                            break;
                    }
                }
                else {
                    switch (arg_name) {
                        case "-s":
                            if (Program.futures_root.Length > 3) {
                                Console.WriteLine("Invalid futures contract symbol: " + arg);
                                System.Environment.Exit(-1);
                            }
                            Program.futures_root = arg.ToUpper();
                            break;
                    }
                    arg_name = null;
                }
            }
        }
    }
}
