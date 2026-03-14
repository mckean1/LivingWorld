using System.Text.RegularExpressions;
using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleColorWriter
{
    private readonly ChronicleLineColorizer _colorizer = new();

    public void WriteLine(string line, World world)
    {
        ChronicleColorContext context = ChronicleColorContext.FromWorld(world);
        WriteLine(line, context);
    }

    public void WriteLine(string line, ChronicleColorContext context)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(line);
            return;
        }

        Write(line, context);
        Console.WriteLine();
        Console.ResetColor();
    }

    public void WriteLineAt(int left, int top, int width, string line, ChronicleColorContext context)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(line);
            return;
        }

        int bufferWidth = ResolveConsoleBufferWidth();
        int bufferHeight = ResolveConsoleBufferHeight();
        if (top < 0 || top >= bufferHeight || left >= bufferWidth)
        {
            return;
        }

        int drawableWidth = Math.Max(1, Math.Min(width, bufferWidth - Math.Max(0, left)));
        string fitted = FitToWidth(line, drawableWidth);
        Console.SetCursorPosition(left, top);
        Write(fitted, context);

        int remaining = Math.Max(0, drawableWidth - fitted.Length);
        if (remaining > 0)
        {
            Console.Write(new string(' ', remaining));
        }

        Console.ResetColor();
    }

    private void Write(string line, ChronicleColorContext context)
    {
        IReadOnlyList<ChronicleStyledSegment> segments = _colorizer.Colorize(line, context);
        foreach (ChronicleStyledSegment segment in segments)
        {
            if (segment.Semantic == ChronicleSemantic.Text)
            {
                Console.Write(segment.Text);
                continue;
            }

            Console.ForegroundColor = MapColor(segment.Semantic);
            Console.Write(segment.Text);
            Console.ResetColor();
        }
    }

    private static string FitToWidth(string line, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        return line.Length <= width
            ? line
            : line[..width];
    }

    private static int ResolveConsoleBufferWidth()
    {
        try
        {
            return Math.Max(1, Console.BufferWidth);
        }
        catch
        {
            return Math.Max(1, Console.WindowWidth > 0 ? Console.WindowWidth : 80);
        }
    }

    private static int ResolveConsoleBufferHeight()
    {
        try
        {
            return Math.Max(1, Console.BufferHeight);
        }
        catch
        {
            return Math.Max(1, Console.WindowHeight > 0 ? Console.WindowHeight : 26);
        }
    }

    private static ConsoleColor MapColor(ChronicleSemantic semantic)
    {
        return semantic switch
        {
            ChronicleSemantic.Subtle => ConsoleColor.DarkGray,
            ChronicleSemantic.YearHeader => ConsoleColor.Cyan,
            ChronicleSemantic.PolityName => ConsoleColor.Yellow,
            ChronicleSemantic.PlaceName => ConsoleColor.Blue,
            ChronicleSemantic.KnowledgeName => ConsoleColor.Magenta,
            ChronicleSemantic.Positive => ConsoleColor.Green,
            ChronicleSemantic.Warning => ConsoleColor.DarkYellow,
            ChronicleSemantic.Crisis => ConsoleColor.Red,
            _ => Console.ForegroundColor
        };
    }
}

public sealed class ChronicleColorContext
{
    public IReadOnlyList<string> PolityNames { get; }
    public IReadOnlyList<string> PlaceNames { get; }
    public IReadOnlyList<string> KnowledgeNames { get; }

    public ChronicleColorContext(
        IEnumerable<string> polityNames,
        IEnumerable<string> placeNames,
        IEnumerable<string> knowledgeNames)
    {
        PolityNames = PrepareNames(polityNames);
        PlaceNames = PrepareNames(placeNames);
        KnowledgeNames = PrepareNames(knowledgeNames);
    }

    public static ChronicleColorContext FromWorld(World world)
    {
        IEnumerable<string> polityNames = world.Polities
            .Select(polity => polity.Name);
        IEnumerable<string> placeNames = world.Regions
            .Select(region => region.Name);
        IEnumerable<string> knowledgeNames = AdvancementCatalog.All
            .Select(definition => definition.Name)
            .Concat(world.Polities.SelectMany(polity => polity.Discoveries).Select(discovery => discovery.Summary));

        return new ChronicleColorContext(polityNames, placeNames, knowledgeNames);
    }

    private static IReadOnlyList<string> PrepareNames(IEnumerable<string> names)
    {
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToList();
    }
}

public enum ChronicleSemantic
{
    Text,
    Subtle,
    YearHeader,
    PolityName,
    PlaceName,
    KnowledgeName,
    Positive,
    Warning,
    Crisis
}

public readonly record struct ChronicleStyledSegment(string Text, ChronicleSemantic Semantic);

internal sealed class ChronicleLineColorizer
{
    private static readonly Regex YearHeaderRegex = new(@"^Year\s+\d+", RegexOptions.Compiled);
    private static readonly Regex StatusYearRegex = new(@"^\s*Year:\s+(\d+)$", RegexOptions.Compiled);
    private static readonly Regex MajorHeaderRegex = new(@"^Year\s+\d+\s+-\s+([A-Z][A-Z\s]+)$", RegexOptions.Compiled);
    private static readonly Regex PopulationRegex = new(@"^Population:\s+\d+\s+\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex RegionRegex = new(@"^Region:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex DiscoveryRegex = new(@"^Discoveries:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex LearnedRegex = new(@"^Learned:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex StatusRegex = new(@"^Status:\s+([A-Z]+)(?:\s+\|\s+View:\s+(.+))?$", RegexOptions.Compiled);
    private static readonly Regex FoodRegex = new(@"^Food:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex FoodStoresRegex = new(@"^Food Stores:\s+\d+\s+\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex SectionHeaderRegex = new(@"^(This Year|Notable Changes)$", RegexOptions.Compiled);

    private static readonly string[] PositivePhrases =
    [
        "enjoyed abundant harvests",
        "remained stable through internal redistribution",
        "harvest surplus",
        "prosperous season",
        "growth resumed",
        "trade route established",
        "settlement flourished"
    ];

    private static readonly string[] WarningPhrases =
    [
        "food shortages",
        "lean year",
        "dependent on imported food",
        "unstable food",
        "low supplies",
        "shortages"
    ];

    private static readonly string[] CrisisPhrases =
    [
        "starvation",
        "famine",
        "collapsed"
    ];

    public IReadOnlyList<ChronicleStyledSegment> Colorize(string line, ChronicleColorContext context)
    {
        if (string.IsNullOrEmpty(line))
        {
            return [new ChronicleStyledSegment(line, ChronicleSemantic.Text)];
        }

        List<SemanticSpan> spans = new();
        bool structuredLine = AddStructuralSpans(line, spans);
        if (!structuredLine)
        {
            AddPhraseSpans(line, PositivePhrases, ChronicleSemantic.Positive, 80, spans);
            AddPhraseSpans(line, WarningPhrases, ChronicleSemantic.Warning, 90, spans);
            AddPhraseSpans(line, CrisisPhrases, ChronicleSemantic.Crisis, 100, spans);
        }
        AddNameSpans(line, context.PolityNames, ChronicleSemantic.PolityName, 70, spans);
        AddNameSpans(line, context.PlaceNames, ChronicleSemantic.PlaceName, 65, spans);
        AddNameSpans(line, context.KnowledgeNames, ChronicleSemantic.KnowledgeName, 75, spans);

        List<SemanticSpan> selected = SelectNonOverlapping(spans);
        if (selected.Count == 0)
        {
            return [new ChronicleStyledSegment(line, ChronicleSemantic.Text)];
        }

        return BuildSegments(line, selected);
    }

    private static bool AddStructuralSpans(string line, List<SemanticSpan> spans)
    {
        bool structuredLine = false;

        Match section = SectionHeaderRegex.Match(line);
        if (section.Success)
        {
            spans.Add(new SemanticSpan(section.Index, section.Length, ChronicleSemantic.Subtle, 120));
            structuredLine = true;
        }

        Match year = YearHeaderRegex.Match(line);
        if (year.Success)
        {
            spans.Add(new SemanticSpan(year.Index, year.Length, ChronicleSemantic.YearHeader, 110));
        }

        Match statusYear = StatusYearRegex.Match(line.TrimStart());
        if (statusYear.Success)
        {
            int offset = line.Length - line.TrimStart().Length;
            spans.Add(new SemanticSpan(offset + statusYear.Groups[1].Index, statusYear.Groups[1].Length, ChronicleSemantic.YearHeader, 95));
            structuredLine = true;
        }

        Match majorHeader = MajorHeaderRegex.Match(line);
        if (majorHeader.Success)
        {
            ChronicleSemantic majorSemantic = ResolveMajorHeadlineSemantic(majorHeader.Groups[1].Value);
            if (majorSemantic != ChronicleSemantic.Text)
            {
                spans.Add(new SemanticSpan(
                    majorHeader.Groups[1].Index,
                    majorHeader.Groups[1].Length,
                    majorSemantic,
                    108));
            }
        }

        Match region = RegionRegex.Match(line.TrimStart());
        if (region.Success)
        {
            int offset = line.Length - line.TrimStart().Length;
            spans.Add(new SemanticSpan(offset + region.Groups[1].Index, region.Groups[1].Length, ChronicleSemantic.PlaceName, 95));
            structuredLine = true;
        }

        Match discoveries = DiscoveryRegex.Match(line.TrimStart());
        if (discoveries.Success)
        {
            int offset = line.Length - line.TrimStart().Length;
            spans.Add(new SemanticSpan(offset + discoveries.Groups[1].Index, discoveries.Groups[1].Length, ChronicleSemantic.KnowledgeName, 95));
            structuredLine = true;
        }

        Match learned = LearnedRegex.Match(line.TrimStart());
        if (learned.Success)
        {
            int offset = line.Length - line.TrimStart().Length;
            spans.Add(new SemanticSpan(offset + learned.Groups[1].Index, learned.Groups[1].Length, ChronicleSemantic.KnowledgeName, 95));
            structuredLine = true;
        }

        Match status = StatusRegex.Match(line.TrimStart());
        if (status.Success)
        {
            int offset = line.Length - line.TrimStart().Length;
            ChronicleSemantic statusSemantic = status.Groups[1].Value switch
            {
                "RUNNING" => ChronicleSemantic.Positive,
                "PAUSED" => ChronicleSemantic.Warning,
                _ => ChronicleSemantic.Text
            };

            if (statusSemantic != ChronicleSemantic.Text)
            {
                spans.Add(new SemanticSpan(offset + status.Groups[1].Index, status.Groups[1].Length, statusSemantic, 95));
            }

            if (status.Groups[2].Success)
            {
                spans.Add(new SemanticSpan(offset + status.Groups[2].Index, status.Groups[2].Length, ChronicleSemantic.Subtle, 90));
            }

            structuredLine = true;
        }

        Match food = FoodRegex.Match(line.TrimStart());
        if (food.Success)
        {
            string state = food.Groups[1].Value.Trim();
            ChronicleSemantic semantic = state switch
            {
                "Surplus" => ChronicleSemantic.Positive,
                "Hunger" => ChronicleSemantic.Warning,
                "Famine" => ChronicleSemantic.Crisis,
                "Stable" => ChronicleSemantic.Subtle,
                _ => ChronicleSemantic.Text
            };

            if (semantic != ChronicleSemantic.Text)
            {
                int offset = line.Length - line.TrimStart().Length;
                spans.Add(new SemanticSpan(offset + food.Groups[1].Index, food.Groups[1].Length, semantic, 95));
            }

            structuredLine = true;
        }

        Match foodStores = FoodStoresRegex.Match(line.TrimStart());
        if (foodStores.Success)
        {
            string state = foodStores.Groups[1].Value.Trim();
            ChronicleSemantic semantic = state switch
            {
                "Surplus" => ChronicleSemantic.Positive,
                "Hunger" => ChronicleSemantic.Warning,
                "Famine" => ChronicleSemantic.Crisis,
                "Stable" => ChronicleSemantic.Subtle,
                _ => ChronicleSemantic.Text
            };

            if (semantic != ChronicleSemantic.Text)
            {
                int offset = line.Length - line.TrimStart().Length;
                spans.Add(new SemanticSpan(offset + foodStores.Groups[1].Index, foodStores.Groups[1].Length, semantic, 95));
            }

            structuredLine = true;
        }

        Match population = PopulationRegex.Match(line.TrimStart());
        if (population.Success)
        {
            string delta = population.Groups[1].Value.Trim();
            ChronicleSemantic semantic = delta.StartsWith("+", StringComparison.Ordinal)
                ? ChronicleSemantic.Positive
                : delta.StartsWith("-", StringComparison.Ordinal)
                    ? ChronicleSemantic.Warning
                    : ChronicleSemantic.Text;

            if (semantic != ChronicleSemantic.Text)
            {
                int offset = line.Length - line.TrimStart().Length;
                spans.Add(new SemanticSpan(offset + population.Groups[1].Index, population.Groups[1].Length, semantic, 95));
            }

            structuredLine = true;
        }

        return structuredLine;
    }

    private static ChronicleSemantic ResolveMajorHeadlineSemantic(string headline)
    {
        if (headline.Contains("DISCOVERY", StringComparison.OrdinalIgnoreCase))
        {
            return ChronicleSemantic.KnowledgeName;
        }

        if (headline.Contains("COLLAPSE", StringComparison.OrdinalIgnoreCase)
            || headline.Contains("FAMINE", StringComparison.OrdinalIgnoreCase))
        {
            return ChronicleSemantic.Crisis;
        }

        if (headline.Contains("FRAGMENTATION", StringComparison.OrdinalIgnoreCase))
        {
            return ChronicleSemantic.Warning;
        }

        if (headline.Contains("FORMED", StringComparison.OrdinalIgnoreCase)
            || headline.Contains("SETTLEMENT", StringComparison.OrdinalIgnoreCase)
            || headline.Contains("TRADE", StringComparison.OrdinalIgnoreCase))
        {
            return ChronicleSemantic.Positive;
        }

        return ChronicleSemantic.Text;
    }

    private static void AddPhraseSpans(
        string line,
        IReadOnlyList<string> phrases,
        ChronicleSemantic semantic,
        int priority,
        List<SemanticSpan> spans)
    {
        foreach (string phrase in phrases)
        {
            int offset = 0;
            while (offset < line.Length)
            {
                int index = line.IndexOf(phrase, offset, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                if (IsBoundary(line, index, phrase.Length))
                {
                    spans.Add(new SemanticSpan(index, phrase.Length, semantic, priority));
                }

                offset = index + phrase.Length;
            }
        }
    }

    private static void AddNameSpans(
        string line,
        IReadOnlyList<string> names,
        ChronicleSemantic semantic,
        int priority,
        List<SemanticSpan> spans)
    {
        foreach (string name in names)
        {
            int offset = 0;
            while (offset < line.Length)
            {
                int index = line.IndexOf(name, offset, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                if (IsBoundary(line, index, name.Length))
                {
                    spans.Add(new SemanticSpan(index, name.Length, semantic, priority));
                }

                offset = index + name.Length;
            }
        }
    }

    private static bool IsBoundary(string line, int index, int length)
    {
        int before = index - 1;
        int after = index + length;

        bool beforeBoundary = before < 0 || !char.IsLetterOrDigit(line[before]);
        bool afterBoundary = after >= line.Length || !char.IsLetterOrDigit(line[after]);

        return beforeBoundary && afterBoundary;
    }

    private static List<SemanticSpan> SelectNonOverlapping(List<SemanticSpan> spans)
    {
        if (spans.Count == 0)
        {
            return [];
        }

        List<SemanticSpan> ordered = spans
            .OrderByDescending(span => span.Priority)
            .ThenByDescending(span => span.Length)
            .ThenBy(span => span.Start)
            .ToList();

        List<SemanticSpan> selected = [];
        foreach (SemanticSpan candidate in ordered)
        {
            bool overlaps = selected.Any(existing => candidate.Overlaps(existing));
            if (!overlaps)
            {
                selected.Add(candidate);
            }
        }

        return selected
            .OrderBy(span => span.Start)
            .ToList();
    }

    private static IReadOnlyList<ChronicleStyledSegment> BuildSegments(string line, IReadOnlyList<SemanticSpan> spans)
    {
        List<ChronicleStyledSegment> segments = [];
        int cursor = 0;

        foreach (SemanticSpan span in spans)
        {
            if (span.Start > cursor)
            {
                segments.Add(new ChronicleStyledSegment(
                    line[cursor..span.Start],
                    ChronicleSemantic.Text));
            }

            int end = span.Start + span.Length;
            segments.Add(new ChronicleStyledSegment(
                line[span.Start..end],
                span.Semantic));
            cursor = end;
        }

        if (cursor < line.Length)
        {
            segments.Add(new ChronicleStyledSegment(
                line[cursor..],
                ChronicleSemantic.Text));
        }

        return segments;
    }

    private readonly record struct SemanticSpan(
        int Start,
        int Length,
        ChronicleSemantic Semantic,
        int Priority)
    {
        public bool Overlaps(SemanticSpan other)
            => Start < (other.Start + other.Length) && other.Start < (Start + Length);
    }
}
