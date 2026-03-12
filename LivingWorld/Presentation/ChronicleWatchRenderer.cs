using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleWatchRenderer : IDisposable
{
    private const int MinimumViewportHeight = 6;
    private readonly SimulationOptions _options;
    private readonly ChronicleColorWriter _colorWriter;
    private readonly ChronicleEventFormatter _formatter;
    private readonly List<string> _chronicleEntries = [];

    private IReadOnlyList<string> _lastStatusLines = [];
    private IReadOnlyList<string> _lastBodyLines = [];
    private IReadOnlyList<string> _lastFooterLines = [];
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

    public void Render(World world, ChronicleFocus focus, WatchUiState uiState)
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return;
        }

        ChronicleLayout layout = BuildLayout(world, focus, uiState);
        Draw(layout, world);
    }

    public bool Record(World world, ChronicleFocus focus, WatchUiState uiState, WorldEvent worldEvent)
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return false;
        }

        if (!_formatter.TryFormat(worldEvent, focus, out string chronicleLine))
        {
            return false;
        }

        _chronicleEntries.Insert(0, chronicleLine);
        uiState.OnChronicleEntryAdded();
        TrimRetainedEntries();
        return true;
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

    private ChronicleLayout BuildLayout(World world, ChronicleFocus focus, WatchUiState uiState)
    {
        Polity? polity = focus.ResolvePolity(world);
        int width = ResolveWindowWidth();
        List<string> statusLines = BuildStatusLines(world, polity, uiState, width);
        List<string> footerLines = WatchScreenBuilder.BuildFooterLines(uiState, width);
        int viewportHeight = ResolveViewportHeight(statusLines.Count, footerLines.Count);
        IReadOnlyList<string> rawBodyLines = uiState.ActiveView == WatchViewType.Chronicle
            ? BuildChronicleViewportEntries(uiState)
            : WatchScreenBuilder.BuildBodyLines(world, focus, uiState);
        List<string> bodyLines = BuildVisibleViewport(rawBodyLines, uiState, viewportHeight);

        return new ChronicleLayout(
            width,
            ResolveWindowHeight(),
            statusLines,
            bodyLines,
            footerLines,
            SeparatorTop: statusLines.Count,
            FooterTop: statusLines.Count + 1 + bodyLines.Count + 1);
    }

    private List<string> BuildStatusLines(World world, Polity? polity, WatchUiState uiState, int width)
        => BuildStatusLines(world, polity, uiState, width, WatchInspectionData.DescribeStage);

    public static List<string> BuildStatusLines(
        World world,
        Polity? polity,
        int width,
        Func<PolityStage, string> stageNameFormatter)
        => BuildStatusLines(world, polity, new WatchUiState(), width, stageNameFormatter);

    public static List<string> BuildStatusLines(
        World world,
        Polity? polity,
        WatchUiState uiState,
        int width,
        Func<PolityStage, string> stageNameFormatter)
    {
        string border = new('=', width);
        List<string> lines = [border];
        string status = uiState.IsPaused ? "PAUSED" : "RUNNING";
        string view = WatchScreenBuilder.DescribeView(uiState.ActiveView);

        if (polity is null)
        {
            lines.Add(" Chronicle Watch");
            lines.Add($" Status: {status} | View: {view}");
            lines.Add($" Year: {world.Time.Year}");
            lines.Add(" Focus: No surviving focal polity");
            lines.Add(border);
            return lines;
        }

        string regionName = world.Regions.FirstOrDefault(region => region.Id == polity.RegionId)?.Name ?? "Unknown Region";
        string speciesName = ChronicleTextFormatter.DescribeSpeciesName(polity, world.Species);
        ChronicleTextFormatter.StatusKnowledgeSummary knowledgeSummary = ChronicleTextFormatter.BuildStatusKnowledgeSummary(polity);
        string foodState = ChronicleTextFormatter.DescribeFoodState(polity);
        string foodStores = Math.Round(polity.FoodStores).ToString("F0");

        lines.Add($" {polity.Name} - {stageNameFormatter(polity.Stage)}");
        lines.Add($" Status: {status} | View: {view}");
        lines.Add($" Species: {speciesName}");
        lines.Add($" Region: {regionName}");
        lines.Add($" Population: {polity.Population}");
        lines.Add($" Settlements: {polity.SettlementCount}");
        lines.Add($" Food Stores: {foodStores} ({foodState})");
        lines.Add($" Discoveries: {knowledgeSummary.Discoveries}");
        lines.Add($" Learned: {knowledgeSummary.Learned}");
        lines.Add($" Year: {world.Time.Year}");
        lines.Add(border);

        return lines;
    }

    private List<string> BuildChronicleViewportEntries(WatchUiState uiState)
    {
        if (_chronicleEntries.Count == 0)
        {
            return ["The chronicle is quiet."];
        }

        int scrollOffset = Math.Clamp(uiState.GetScrollOffset(WatchViewType.Chronicle), 0, Math.Max(0, _chronicleEntries.Count - 1));
        return _chronicleEntries.Skip(scrollOffset).ToList();
    }

    private static List<string> BuildVisibleViewport(IReadOnlyList<string> rawLines, WatchUiState uiState, int viewportHeight)
    {
        int maxOffset = Math.Max(0, rawLines.Count - viewportHeight);
        int offset = WatchScreenBuilder.ResolveViewportOffset(rawLines.Count, viewportHeight, uiState, maxOffset);
        List<string> visible = rawLines.Skip(offset).Take(viewportHeight).ToList();

        if (visible.Count == 0)
        {
            visible.Add("Nothing to inspect yet.");
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

            foreach (string line in layout.BodyLines)
            {
                _colorWriter.WriteLine(line, world);
            }

            Console.WriteLine();

            foreach (string line in layout.FooterLines)
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
        int bodyTop = layout.SeparatorTop + 1;
        WriteRegion(bodyTop, layout.BodyLines, _lastBodyLines, layout.Width, context, forceAll: dimensionsChanged);
        WriteRegion(layout.FooterTop - 1, [string.Empty], [], layout.Width, context, forceAll: true);
        WriteRegion(layout.FooterTop, layout.FooterLines, _lastFooterLines, layout.Width, context, forceAll: dimensionsChanged);

        _lastStatusLines = layout.StatusLines.ToList();
        _lastBodyLines = layout.BodyLines.ToList();
        _lastFooterLines = layout.FooterLines.ToList();
        _lastWidth = layout.Width;
        _lastHeight = layout.Height;

        int safeCursorTop = Math.Min(layout.Height - 1, layout.FooterTop + layout.FooterLines.Count);
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
            Math.Max(MinimumViewportHeight * 4, _options.ChronicleVisibleEntryLimit));
        while (_chronicleEntries.Count > retentionLimit)
        {
            _chronicleEntries.RemoveAt(_chronicleEntries.Count - 1);
        }
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
            return 26;
        }

        return Math.Max(16, Console.WindowHeight > 0 ? Console.WindowHeight : 26);
    }

    private int ResolveViewportHeight(int statusLineCount, int footerLineCount)
    {
        int totalHeight = ResolveWindowHeight();
        int reserved = statusLineCount + footerLineCount + 2;
        return Math.Max(1, totalHeight - reserved);
    }

    private sealed record ChronicleLayout(
        int Width,
        int Height,
        IReadOnlyList<string> StatusLines,
        IReadOnlyList<string> BodyLines,
        IReadOnlyList<string> FooterLines,
        int SeparatorTop,
        int FooterTop);
}
