using System.Text.Json;
using Celly.Checking;
using Celly.Types;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;
using Rooby.Engine.Dependencies;

namespace Rooby.Engine.Evaluation;

/// <summary>
/// A compiled, immutable, thread-safe snapshot of a <see cref="Bundle"/> (SPEC §5, §10.4): resolves
/// dependency order once, rejects cycles at construction, and evaluates items in dynamic mode
/// (<c>input: dyn</c> — the same evaluator the management API, publish gate and runner all share).
/// </summary>
public sealed partial class BundleEvaluator
{
    private readonly Bundle _bundle;
    private readonly CelCompiler _compiler;
    private readonly DependencyExtractor _dependencyExtractor = new();
    private readonly Dictionary<string, BundleItem> _itemsByKey;
    private readonly Dictionary<string, IReadOnlySet<string>> _dependencies = new(StringComparer.Ordinal);

    public BundleEvaluator(Bundle bundle, CelCompiler? compiler = null)
    {
        _bundle = bundle;
        _compiler = compiler ?? new CelCompiler();
        _itemsByKey = new Dictionary<string, BundleItem>(StringComparer.Ordinal);
        foreach (var item in bundle.Items)
        {
            _itemsByKey[item.Key] = item;
        }

        foreach (var item in bundle.Items)
        {
            var lineContents = item.Lines.Select(l => l.Content).ToList();
            _dependencies[item.Key] = _dependencyExtractor.Extract(item.ItemType, item.Content, lineContents);
        }

        DetectCycles();
    }

    public string Project => _bundle.Project;

    public string Profile => _bundle.Profile;

    public string TimeZone => _bundle.TimeZone;

    public int VersionId => _bundle.VersionId;

    public bool HasItem(string key) => _itemsByKey.ContainsKey(key);

    /// <summary>The item keys a given item directly depends on (§5.3) — extracted once at construction.</summary>
    public IReadOnlySet<string> DependenciesOf(string itemKey) =>
        _dependencies.TryGetValue(itemKey, out var deps) ? deps : throw UnknownItem(itemKey);

    /// <summary>
    /// Evaluates an item that produces a value from <c>input</c> (ExpressionRule, DecisionTable,
    /// DecisionTree, RuleList; also a probed Matrix, and trivially SingleValue/Basket which ignore
    /// input). Lookup and an un-probed Matrix have no input schema (§4) — use <see cref="LookupValue"/>
    /// / <see cref="MatrixValue"/> instead.
    /// </summary>
    public object? Evaluate(string itemKey, object? input, DateTimeOffset? now = null, EvalLimits? limits = null) =>
        EvaluateWithSession(itemKey, input, now, limits).Value;

    /// <summary>Same as <see cref="Evaluate"/> but also returns the session (test seam: verifying ref memoisation counts).</summary>
    internal (object? Value, EvaluationSession Session) EvaluateWithSession(string itemKey, object? input, DateTimeOffset? now = null, EvalLimits? limits = null)
    {
        var item = GetItem(itemKey);
        var context = EvaluationContext.Create(input, now ?? DateTimeOffset.UtcNow, TimeZone, limits);
        var value = EvaluateTopLevel(item, context);
        return (value, context.Session);
    }

    /// <summary>SingleValue's constant, or any other item type's <see cref="Evaluate"/> result with a null input.</summary>
    public object? GetValue(string itemKey, DateTimeOffset? now = null) => Evaluate(itemKey, input: null, now);

    public IReadOnlyList<object?> BasketValues(string itemKey, DateTimeOffset? now = null)
    {
        var result = Evaluate(itemKey, input: null, now);
        return result switch
        {
            null => [],
            IReadOnlyList<object?> list => list,
            System.Collections.IEnumerable e => e.Cast<object?>().ToList(),
            _ => throw new RoobyEvaluationException(itemKey, null, "expected a Basket item"),
        };
    }

    private object? EvaluateTopLevel(BundleItem item, EvaluationContext context)
    {
        context.Session.RecordEvalStart(item.Key);
        var value = EvaluateItemCore(item, context);
        context.Session.SetMemo(item.Key, value);
        return value;
    }

    /// <summary>Resolves item <paramref name="key"/> as seen through <c>ref.key</c>/a step or slot <c>ref</c> (SPEC §5.2, §5.3).</summary>
    internal object? EvaluateForReference(string key, EvaluationContext callerContext)
    {
        if (callerContext.Session.TryGetMemo(key, out var cached))
        {
            return cached;
        }

        var item = GetItem(key);
        var subContext = callerContext.ForReferencedItem();
        callerContext.Session.RecordEvalStart(key);
        var value = EvaluateItemCore(item, subContext);
        callerContext.Session.SetMemo(key, value);
        return value;
    }

    private object? EvaluateItemCore(BundleItem item, EvaluationContext context)
    {
        context.Session.ChargeStep(item.Key);
        object? raw = item.ItemType switch
        {
            ItemType.SingleValue => EvaluateSingleValue(item),
            ItemType.Basket => EvaluateBasket(item, context),
            ItemType.Lookup => null, // no input-driven evaluation; see LookupValue
            ItemType.ExpressionRule => EvaluateExpressionRule(item, context),
            ItemType.DecisionTable => EvaluateDecisionTable(item, context),
            ItemType.DecisionTree => EvaluateDecisionTree(item, context),
            ItemType.RuleList => EvaluateRuleList(item, context),
            ItemType.Matrix => EvaluateProbedMatrix(item, context),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
        return OutputCoercion.Coerce(raw, item.DataType, item.Key, null);
    }

    private static object? EvaluateSingleValue(BundleItem item)
    {
        var content = ItemContentSerializer.Deserialize<SingleValueContent>(item.Content);
        return JsonNativeConverter.ToNative(content.Value);
    }

    private static List<object?>? EvaluateBasket(BundleItem item, EvaluationContext context)
    {
        var values = new List<object?>();
        foreach (var line in item.Lines)
        {
            if (!IsLineActive(line.Validity, context))
            {
                continue;
            }

            var lineContent = ItemContentSerializer.Deserialize<BasketLine>(line.Content);
            values.Add(JsonNativeConverter.ToNative(lineContent.Value));
        }

        return values;
    }

    private object? EvaluateExpressionRule(BundleItem item, EvaluationContext context)
    {
        var content = ItemContentSerializer.Deserialize<ExpressionRuleContent>(item.Content);
        var vars = ResolveBindings(item.Key, content.Bindings, context);
        var boundContext = context with { Vars = vars };
        var result = RunExpression(content.Expression, boundContext, item.Key);
        return result ?? (content.Default is { } def ? JsonNativeConverter.ToNative(def) : null);
    }

    /// <summary>Resolves ExpressionRule/RuleList-bind bindings into a new <c>vars</c> map layered on the caller's.</summary>
    private Dictionary<string, object?> ResolveBindings(string itemKey, IReadOnlyList<ExpressionBinding>? bindings, EvaluationContext context)
    {
        var vars = new Dictionary<string, object?>(context.Vars, StringComparer.Ordinal);
        foreach (var binding in bindings ?? [])
        {
            var keys = binding.Keys.Select(k => RunExpression(k, context with { Vars = vars }, itemKey)).ToList();
            vars[binding.Name] = LookupValueByKeys(binding.Lookup, keys, context);
        }

        return vars;
    }

    private BundleItem GetItem(string key) => _itemsByKey.TryGetValue(key, out var item) ? item : throw UnknownItem(key);

    internal ItemType GetItemType(string key) => GetItem(key).ItemType;

    /// <summary>
    /// Evaluates an inline ad-hoc sub rule's content (SPEC §4.0: ExpressionRule, DecisionTree,
    /// DecisionTable or Matrix only) using a synthetic item scoped to the enclosing item's key, so
    /// step-charging/error attribution still points at the real item. <paramref name="expectedType"/>
    /// is the enclosing slot's declared type, needed so nested DecisionTree leaves/DecisionTable
    /// output cells/Matrix cells coerce correctly (an inline rule has no <c>DataType</c> of its own).
    /// </summary>
    internal object? EvaluateInlineContent(
        ItemType itemType,
        JsonElement content,
        IReadOnlyList<JsonElement> lines,
        DataType expectedType,
        string itemKey,
        string slotPath,
        EvaluationContext context)
    {
        var synthetic = new BundleItem
        {
            Id = Guid.Empty,
            Key = itemKey,
            ItemType = itemType,
            DataType = expectedType,
            Content = content,
            Lines = lines.Select((line, index) => new BundleLine { Id = Guid.Empty, SortOrder = index, Content = line }).ToList(),
        };
        return itemType switch
        {
            ItemType.ExpressionRule => EvaluateExpressionRule(synthetic, context),
            ItemType.DecisionTree => EvaluateDecisionTree(synthetic, context),
            ItemType.DecisionTable => EvaluateDecisionTable(synthetic, context),
            ItemType.Matrix => EvaluateProbedMatrix(synthetic, context),
            _ => throw new RoobyEvaluationException(itemKey, slotPath, $"{itemType} may not be used as an inline rule"),
        };
    }

    private static KeyNotFoundException UnknownItem(string key) => new($"Unknown item key '{key}'.");

    internal static bool IsLineActive(LineValidity? validity, EvaluationContext context)
    {
        if (validity is null)
        {
            return true;
        }

        var localDate = ProfileLocalDate(context);
        if (validity.From is { } from && localDate < from)
        {
            return false;
        }

        if (validity.To is { } to && localDate >= to)
        {
            return false;
        }

        return true;
    }

    /// <summary>The calendar date of <c>now</c> in the bundle's time zone (SPEC §5.6).</summary>
    internal static DateOnly ProfileLocalDate(EvaluationContext context)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(context.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Now, tz).Date);
    }

    /// <summary>Runs a CEL expression with the standard context variables (§5.2) bound for <paramref name="itemKey"/>.</summary>
    internal object? RunExpression(string celExpression, EvaluationContext context, string itemKey, string? extraName = null, object? extraValue = null)
    {
        context.Session.ChargeStep(itemKey);
        IReadOnlyList<VariableDecl>? extraDeclarations = extraName is null ? null : [new VariableDecl(extraName, CelType.Dyn)];
        var compiled = _compiler.CompileDynamic(celExpression, extraDeclarations, extraName is null ? string.Empty : $":{extraName}");
        if (!compiled.Success)
        {
            throw new RoobyEvaluationException(itemKey, null, "CEL compile error: " + string.Join("; ", compiled.Errors));
        }

        var bindings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["input"] = context.Input,
            ["now"] = context.Now,
            ["tz"] = context.TimeZone,
            ["prev"] = context.Prev,
            ["vars"] = context.Vars,
            ["ref"] = BuildRefMap(itemKey, context),
        };
        if (extraName is not null)
        {
            bindings[extraName] = extraValue;
        }

        var limits = new Celly.Interpreter.EvalLimits
        {
            MaxIterations = context.Session.Limits.MaxIterations,
            CancellationToken = context.Session.Limits.CancellationToken,
        };
        var result = compiled.Program!.Eval(bindings, limits);
        if (result.IsError)
        {
            throw new RoobyEvaluationException(itemKey, null, "CEL evaluation error: " + result);
        }

        return result.ToNative();
    }

    /// <summary>
    /// Builds the <c>ref</c> map for <paramref name="itemKey"/>'s expressions: only its statically
    /// extracted direct dependencies (§5.3), each resolved (and memoised) through
    /// <see cref="EvaluateForReference"/> — never the whole bundle, so this stays cheap regardless of
    /// bundle size and matches "resolved lazily" without requiring a lazily-indexable CEL map type.
    /// </summary>
    private Dictionary<string, object?> BuildRefMap(string itemKey, EvaluationContext context)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var depKey in DependenciesOf(itemKey))
        {
            map[depKey] = ResolveRefEntry(depKey, context);
        }

        return map;
    }

    /// <summary>Builds one <c>ref.X</c> entry per SPEC §5.2's per-ItemType shape (nested maps for exact Lookup/Matrix).</summary>
    private object? ResolveRefEntry(string key, EvaluationContext context)
    {
        var item = GetItem(key);
        var subContext = context.ForReferencedItem();
        return item.ItemType switch
        {
            ItemType.Lookup => BuildExactLookupRefMap(item, subContext),
            ItemType.Matrix => BuildMatrixRefEntry(item, subContext),
            _ => EvaluateForReference(key, context),
        };
    }

    /// <summary>Stringifies a native value the way it would appear as a JSON-authored key column value (shared by Lookup/Matrix exact matching).</summary>
    internal static string KeyText(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Compares a probe/explicit key value against a range bound (Number or Date), for Lookup/Matrix range matching.</summary>
    internal static int CompareRangeValue(object? value, object? bound)
    {
        var (a, b) = (ToComparableDouble(value), ToComparableDouble(bound));
        if (a is not null && b is not null)
        {
            return a.Value.CompareTo(b.Value);
        }

        var (da, db) = (ToComparableDate(value), ToComparableDate(bound));
        if (da is not null && db is not null)
        {
            return da.Value.CompareTo(db.Value);
        }

        throw new RoobyEvaluationException("<range>", null, $"cannot compare '{value}' to range bound '{bound}'");
    }

    private static double? ToComparableDouble(object? value) => value switch
    {
        double d => d,
        long l => l,
        int i => i,
        float f => f,
        decimal m => (double)m,
        _ => null,
    };

    private static DateTimeOffset? ToComparableDate(object? value) => value switch
    {
        DateTimeOffset dt => dt,
        DateTime dt => new DateTimeOffset(dt),
        DateOnly d => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        string s when DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) => parsed,
        _ => null,
    };

    private void DetectCycles()
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0=unvisited,1=in-progress,2=done
        foreach (var key in _itemsByKey.Keys)
        {
            Visit(key, []);
        }

        void Visit(string key, List<string> path)
        {
            if (state.TryGetValue(key, out var s))
            {
                if (s == 2)
                {
                    return;
                }

                if (s == 1)
                {
                    throw new RoobyCycleException([.. path, key]);
                }
            }

            if (!_dependencies.TryGetValue(key, out var deps))
            {
                return; // dangling ref: reported by publish validation (§8.2), not bundle load
            }

            state[key] = 1;
            path.Add(key);
            foreach (var dep in deps)
            {
                Visit(dep, path);
            }

            path.RemoveAt(path.Count - 1);
            state[key] = 2;
        }
    }
}
