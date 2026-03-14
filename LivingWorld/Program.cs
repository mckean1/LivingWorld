
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Presentation;

namespace LivingWorld;

class Program
{
    static void Main(string[] args)
    {
        const int monthsInYear = 12;
        ProgramOptions programOptions = ProgramOptions.Parse(args);

        if (programOptions.ShowHelp)
        {
            PrintUsage();
            return;
        }

        int seed = ResolveSeed(programOptions.Seed);
        WorldGenerator generator = new(seed);
        World world = generator.Generate();

        SimulationOptions options = CreateSimulationOptions(programOptions);
        using Simulation simulation = new(world, options);

        if (ShouldPromptBeforeStart(options))
        {
            Console.WriteLine("Press any key to start simulation.");
            Console.ReadKey();
        }

        simulation.RunMonths(programOptions.YearsToSimulate * monthsInYear);
        if (options.WriteStructuredHistory)
        {
            Console.WriteLine($"History file: {Path.GetFullPath(options.HistoryFilePath)}");
        }
        Console.WriteLine("Simulation complete.");
    }

    private static SimulationOptions CreateSimulationOptions(ProgramOptions programOptions)
    {
        SimulationOptions defaults = programOptions.DebugOutput
            ? new SimulationOptions
            {
                OutputMode = OutputMode.Debug,
                WriteStructuredHistory = !programOptions.DisableHistory
            }
            : SimulationOptions.ChronicleWatch(
                programOptions.ChroniclePlaybackDelayMilliseconds,
                programOptions.ChronicleVisibleEntryLimit);

        return new SimulationOptions
        {
            OutputMode = defaults.OutputMode,
            ChroniclePlaybackDelayMilliseconds = defaults.ChroniclePlaybackDelayMilliseconds,
            ChronicleVisibleEntryLimit = defaults.ChronicleVisibleEntryLimit,
            PauseBeforeStart = programOptions.PauseBeforeStart,
            PauseAfterEachYear = programOptions.PauseAfterEachYear,
            FocusedPolityId = programOptions.FocusedPolityId,
            WriteStructuredHistory = !programOptions.DisableHistory,
            HistoryFilePath = programOptions.HistoryFilePath ?? defaults.HistoryFilePath,
            EnablePerformanceInstrumentation = programOptions.EnablePerformanceInstrumentation
        };
    }

    internal static bool ShouldPromptBeforeStart(SimulationOptions options)
        => options.PauseBeforeStart && options.OutputMode != OutputMode.Watch;

    internal static int ResolveSeed(int? requestedSeed)
        => requestedSeed ?? Random.Shared.Next(1, int.MaxValue);

    private static void PrintUsage()
    {
        Console.WriteLine("LivingWorld");
        Console.WriteLine("  --years <n>         Years to simulate (default: 120)");
        Console.WriteLine("  --seed <n>          World seed (default: random)");
        Console.WriteLine("  --debug             Use developer/debug output");
        Console.WriteLine("  --delay-ms <n>      Chronicle playback delay in watch mode");
        Console.WriteLine("  --fast              Alias for --delay-ms 0");
        Console.WriteLine("  --focus-polity <n>  Follow a specific polity id");
        Console.WriteLine("  --buffer-size <n>   Minimum retained chronicle history entries");
        Console.WriteLine("  --no-history        Disable JSONL history output");
        Console.WriteLine("  --history-path <p>  Override JSONL history output path");
        Console.WriteLine("  --perf              Enable lightweight performance instrumentation");
        Console.WriteLine("  --pause-before-start");
        Console.WriteLine("  --pause-after-year");
        Console.WriteLine("  --help");
    }

    private sealed class ProgramOptions
    {
        public int? Seed { get; private set; }
        public int YearsToSimulate { get; private set; } = 120;
        public bool DebugOutput { get; private set; }
        public int ChroniclePlaybackDelayMilliseconds { get; private set; } = 500;
        public int ChronicleVisibleEntryLimit { get; private set; } = 8;
        public int? FocusedPolityId { get; private set; }
        public bool DisableHistory { get; private set; }
        public string? HistoryFilePath { get; private set; }
        public bool EnablePerformanceInstrumentation { get; private set; }
        public bool PauseBeforeStart { get; private set; }
        public bool PauseAfterEachYear { get; private set; }
        public bool ShowHelp { get; private set; }

        public static ProgramOptions Parse(string[] args)
        {
            ProgramOptions options = new();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--years":
                        options.YearsToSimulate = ParseInt(args, ref i, "--years", minimum: 1);
                        break;
                    case "--seed":
                        options.Seed = ParseInt(args, ref i, "--seed");
                        break;
                    case "--debug":
                        options.DebugOutput = true;
                        break;
                    case "--delay-ms":
                        options.ChroniclePlaybackDelayMilliseconds = ParseInt(args, ref i, "--delay-ms", minimum: 0);
                        break;
                    case "--fast":
                        options.ChroniclePlaybackDelayMilliseconds = 0;
                        break;
                    case "--focus-polity":
                        options.FocusedPolityId = ParseInt(args, ref i, "--focus-polity", minimum: 0);
                        break;
                    case "--buffer-size":
                        options.ChronicleVisibleEntryLimit = ParseInt(args, ref i, "--buffer-size", minimum: 1);
                        break;
                    case "--no-history":
                        options.DisableHistory = true;
                        break;
                    case "--history-path":
                        options.HistoryFilePath = ParseString(args, ref i, "--history-path");
                        break;
                    case "--perf":
                        options.EnablePerformanceInstrumentation = true;
                        break;
                    case "--pause-before-start":
                        options.PauseBeforeStart = true;
                        break;
                    case "--pause-after-year":
                        options.PauseAfterEachYear = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {arg}");
                }
            }

            return options;
        }

        private static int ParseInt(string[] args, ref int index, string optionName, int minimum = int.MinValue)
        {
            string raw = ParseString(args, ref index, optionName);
            if (!int.TryParse(raw, out int value) || value < minimum)
            {
                throw new ArgumentException($"{optionName} expects an integer value >= {minimum}.");
            }

            return value;
        }

        private static string ParseString(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{optionName} expects a value.");
            }

            index++;
            return args[index];
        }
    }
}


