using System.Threading;
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleWatchRenderer : IDisposable
{
    private const int MinimumChronicleViewportHeight = 6;
    private readonly SimulationOptions _options;
    private readonly ChronicleColorWriter _colorWriter;
    private readonly ChronicleEventFormatter _formatter;
    private readonly List<string> _chronicleEntries = [];
    private readonly HashSet<string> _displayedChronicleKeys = [];

    private IReadOnlyList<string> _lastStatusLines = [];
    private IReadOnlyList<string> _lastChronicleLines = [];
    private int _lastWidth = -1;
    private int _lastHeight = -1;
    private bool _cursorWasVisible = true;
    private bool _cursorHidden;

    public ChronicleWatchRenderer(
        SimulationOptions options,
        ChronicleColorWriter colorWriter,
        ChronicleEventFormatter formatter)
    {
        _options = options;
        _colorWriter = colorWriter;
        _formatter = formatter;
    }

    public void Render(World world, ChronicleFocus focus)
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return;
        }

        ChronicleLayout layout = BuildLayout(world, focus);
        Draw(layout, world);
    }

    public void Record(World world, ChronicleFocus focus, WorldEvent worldEvent)
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return;
        }

        if (!_formatter.TryFormat(worldEvent, focus, out string chronicleLine))
        {
            return;
        }

        if (!ShouldDisplay(worldEvent))
        {
            return;
        }

        _chronicleEntries.Insert(0, chronicleLine);
        TrimRetainedEntries();
        Render(world, focus);

        if (_options.ChroniclePlaybackDelayMilliseconds > 0)
        {
            Thread.Sleep(_options.ChroniclePlaybackDelayMilliseconds);
        }
    }

    public void Dispose()
    {
        if (Console.IsOutputRedirected || !_cursorHidden || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Console.CursorVisible = _cursorWasVisible;
        }
        catch
        {
            // Ignore terminal-specific cursor restoration failures.
        }
    }

    private ChronicleLayout BuildLayout(World world, ChronicleFocus focus)
    {
        Polity? polity = focus.ResolvePolity(world);
        int width = ResolveWindowWidth();
        List<string> statusLines = BuildStatusLines(world, polity, width);
        int viewportHeight = ResolveChronicleViewportHeight(statusLines.Count);
        List<string> chronicleLines = BuildChronicleViewport(viewportHeight);

        return new ChronicleLayout(width, ResolveWindowHeight(), statusLines, chronicleLines, SeparatorTop: statusLines.Count);
    }

    private List<string> BuildStatusLines(World world, Polity? polity, int width)
    {
        string border = new('=', width);
        List<string> lines = [border];

        if (polity is null)
        {
            lines.Add(" Chronicle Watch");
            lines.Add($" Year: {world.Time.Year}");
            lines.Add(" Focus: No surviving focal polity");
            lines.Add(border);
            return lines;
        }

        string regionName = world.Regions.FirstOrDefault(region => region.Id == polity.RegionId)?.Name ?? "Unknown Region";
        string knowledge = ChronicleTextFormatter.DescribeKnowledge(polity);
        string foodState = ChronicleTextFormatter.DescribeFoodState(polity);
        string foodStores = Math.Round(polity.FoodStores).ToString("F0");

        lines.Add($" {polity.Name} - {RenderStageName(polity.Stage)}");
        lines.Add($" Region: {regionName}");
        lines.Add($" Population: {polity.Population}");
        lines.Add($" Settlements: {polity.SettlementCount}");
        lines.Add($" Food Stores: {foodStores} ({foodState})");
        lines.Add($" Knowledge: {knowledge}");
        lines.Add($" Year: {world.Time.Year}");
        lines.Add(border);

        return lines;
    }

    private List<string> BuildChronicleViewport(int viewportHeight)
    {
        List<string> visible = _chronicleEntries
            .Take(viewportHeight)
            .ToList();

        if (visible.Count == 0)
        {
            visible.Add("The chronicle is quiet.");
        }

        while (visible.Count < viewportHeight)
        {
            visible.Add(string.Empty);
        }

        return visible;
    }

    private void Draw(ChronicleLayout layout, World world)
    {
        if (Console.IsOutputRedirected)
        {
            foreach (string line in layout.StatusLines)
            {
                _colorWriter.WriteLine(line, world);
            }

            Console.WriteLine();

            foreach (string line in layout.ChronicleLines)
            {
                _colorWriter.WriteLine(line, world);
            }

            return;
        }

        ChronicleColorContext context = ChronicleColorContext.FromWorld(world);
        HideCursor();

        bool dimensionsChanged = layout.Width != _lastWidth || layout.Height != _lastHeight;
        if (dimensionsChanged)
        {
            ClearWindow(layout.Width, layout.Height);
        }

        WriteRegion(0, layout.StatusLines, _lastStatusLines, layout.Width, context, forceAll: dimensionsChanged);
        WriteRegion(layout.SeparatorTop, [string.Empty], [], layout.Width, context, forceAll: true);
        int chronicleTop = layout.SeparatorTop + 1;
        WriteRegion(chronicleTop, layout.ChronicleLines, _lastChronicleLines, layout.Width, context, forceAll: dimensionsChanged);

        _lastStatusLines = layout.StatusLines.ToList();
        _lastChronicleLines = layout.ChronicleLines.ToList();
        _lastWidth = layout.Width;
        _lastHeight = layout.Height;

        int safeCursorTop = Math.Min(layout.Height - 1, chronicleTop + layout.ChronicleLines.Count);
        Console.SetCursorPosition(0, Math.Max(0, safeCursorTop));
    }

    private void WriteRegion(
        int top,
        IReadOnlyList<string> nextLines,
        IReadOnlyList<string> previousLines,
        int width,
        ChronicleColorContext context,
        bool forceAll)
    {
        int lineCount = Math.Max(nextLines.Count, previousLines.Count);
        for (int i = 0; i < lineCount; i++)
        {
            string next = i < nextLines.Count ? nextLines[i] : string.Empty;
            string previous = i < previousLines.Count ? previousLines[i] : string.Empty;
            if (!forceAll && string.Equals(next, previous, StringComparison.Ordinal))
            {
                continue;
            }

            _colorWriter.WriteLineAt(0, top + i, width, next, context);
        }
    }

    private void HideCursor()
    {
        if (_cursorHidden)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _cursorWasVisible = Console.CursorVisible;
            Console.CursorVisible = false;
            _cursorHidden = true;
        }
        catch
        {
            // Ignore terminals that do not support cursor visibility changes.
        }
    }

    private static void ClearWindow(int width, int height)
    {
        string blank = new(' ', width);
        for (int row = 0; row < height; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(blank);
        }
    }

    private void TrimRetainedEntries()
    {
        int retentionLimit = Math.Max(
            ResolveWindowHeight() * 4,
            Math.Max(MinimumChronicleViewportHeight * 4, _options.ChronicleVisibleEntryLimit));
        while (_chronicleEntries.Count > retentionLimit)
        {
            _chronicleEntries.RemoveAt(_chronicleEntries.Count - 1);
        }
    }

    private bool ShouldDisplay(WorldEvent worldEvent)
    {
        string key = worldEvent.Type switch
        {
            WorldEventType.Migration => $"migration:{worldEvent.PolityId}:{worldEvent.Year}",
            _ => $"event:{worldEvent.EventId}"
        };

        return _displayedChronicleKeys.Add(key);
    }

    private static int ResolveWindowWidth()
    {
        int width = Console.IsOutputRedirected
            ? 80
            : (Console.WindowWidth > 0 ? Console.WindowWidth : 80);

        return Math.Clamp(width - 1, 40, 160);
    }

    private static int ResolveWindowHeight()
    {
        if (Console.IsOutputRedirected)
        {
            return 20;
        }

        return Math.Max(12, Console.WindowHeight > 0 ? Console.WindowHeight : 20);
    }

    private int ResolveChronicleViewportHeight(int statusLineCount)
    {
        int totalHeight = ResolveWindowHeight();
        int reserved = statusLineCount + 1;
        int availableHeight = Math.Max(1, totalHeight - reserved);
        return availableHeight;
    }

    private static string RenderStageName(PolityStage stage)
        => stage switch
        {
            PolityStage.SettledSociety => "Settled Society",
            _ => stage.ToString()
        };

    private sealed record ChronicleLayout(
        int Width,
        int Height,
        IReadOnlyList<string> StatusLines,
        IReadOnlyList<string> ChronicleLines,
        int SeparatorTop);
}
