using System.Collections.Concurrent;
using System.Globalization;
using Celly;
using Celly.Checking;
using Celly.Extensions;
using Celly.Providers;
using Celly.Types;
using Rooby.Engine.Schema;

namespace Rooby.Engine.Cel;

/// <summary>
/// Compiles CEL expressions for Rooby's two modes (SPEC §5.1): checked (schema-typed <c>input</c>) and
/// dynamic (<c>input: dyn</c>). Declares the standard context variables (SPEC §5.2): <c>input</c>,
/// <c>now</c>, <c>tz</c>, <c>prev</c>, <c>vars</c>, <c>ref</c>. Enables OptionalsLibrary,
/// StringsLibrary, MathLibrary; enforces a static cost estimate ceiling; caches environments and
/// compiled programs.
/// </summary>
public sealed class CelCompiler(ulong maxCost = CelCompiler.DefaultMaxCost)
{
    public const ulong DefaultMaxCost = 100_000;

    private static readonly NullCostEstimator CostEstimator = new();

    private readonly ConcurrentDictionary<string, CelEnv> _envCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string EnvKey, string Expression), CelCompileResult> _programCache = new();

    /// <summary>Compiles in dynamic mode (<c>input: dyn</c>); used by the runner and delivery evaluate endpoint.</summary>
    public CelCompileResult CompileDynamic(string expression, IReadOnlyList<VariableDecl>? extraDeclarations = null, string envKeySuffix = "") =>
        CompileWithEnv($"dynamic{envKeySuffix}", expression, () => BuildEnv(CelType.Dyn, typeProvider: null, extraDeclarations));

    /// <summary>
    /// Compiles in checked mode against <paramref name="schema"/> (null → <c>input: dyn</c>, still checked
    /// for everything else). <paramref name="schemaCacheKey"/> must uniquely identify the schema (and any
    /// <paramref name="extraDeclarations"/> combination) for environment reuse across calls.
    /// </summary>
    public CelCompileResult CompileChecked(
        string expression,
        RoobySchema? schema,
        string schemaCacheKey,
        IReadOnlyList<VariableDecl>? extraDeclarations = null) =>
        CompileWithEnv($"checked:{schemaCacheKey}", expression, () =>
        {
            CelType inputType = CelType.Dyn;
            ITypeProvider? typeProvider = null;
            if (schema is not null)
            {
                (inputType, typeProvider) = CelDeclarationBuilder.Build(schema);
            }

            return BuildEnv(inputType, typeProvider, extraDeclarations);
        });

    private CelCompileResult CompileWithEnv(string envKey, string expression, Func<CelEnv> buildEnv)
    {
        var env = _envCache.GetOrAdd(envKey, _ => buildEnv());
        return _programCache.GetOrAdd((envKey, expression), _ => CompileCore(env, expression));
    }

    private CelCompileResult CompileCore(CelEnv env, string expression)
    {
        var parsed = env.Parse(expression);
        if (parsed.HasErrors)
        {
            return CelCompileResult.Fail(parsed.Issues.Select(i => i.ToString()).ToList());
        }

        // Ast is null only alongside parse errors, already handled above.
        var ast = parsed.Ast!;

        // EstimateCost requires a checked AST regardless of mode (Celly throws otherwise); dynamic
        // mode's permissiveness comes from `input: dyn` in its declarations, not from skipping this step.
        var checkResult = env.Check(ast);
        if (checkResult.HasErrors)
        {
            return CelCompileResult.Fail(checkResult.Issues.Select(i => i.ToString()).ToList());
        }

        ast.AnnotateChecked(checkResult.TypeMap!, checkResult.References);

        var cost = env.EstimateCost(ast, CostEstimator);
        if (cost.IsUnbounded || cost.Max > maxCost)
        {
            var costText = cost.IsUnbounded ? "unbounded" : cost.Max.ToString(CultureInfo.InvariantCulture);
            return CelCompileResult.Fail($"expression cost estimate ({costText}) exceeds the limit ({maxCost})");
        }

        return CelCompileResult.Ok(env.Program(ast));
    }

    private static CelEnv BuildEnv(CelType inputType, ITypeProvider? typeProvider, IReadOnlyList<VariableDecl>? extraDeclarations)
    {
        List<VariableDecl> declarations =
        [
            new VariableDecl("input", inputType),
            new VariableDecl("now", CelType.Timestamp),
            new VariableDecl("tz", CelType.String),
            new VariableDecl("prev", CelType.Dyn),
            new VariableDecl("vars", CelType.Map(CelType.String, CelType.Dyn)),
            new VariableDecl("ref", CelType.Map(CelType.String, CelType.Dyn)),
        ];
        if (extraDeclarations is not null)
        {
            declarations.AddRange(extraDeclarations);
        }

        return CelEnv.Create(new CelEnvSettings
        {
            // Program()/Planner requires a real ITypeProvider even with no schema-derived struct types.
            TypeProvider = typeProvider ?? EmptyTypeProvider.Instance,
            Declarations = declarations,
            Libraries =
            [
                OptionalsLibrary.Instance,
                StringsLibrary.Instance,
                MathLibrary.Instance,
            ],
        });
    }

    /// <summary>
    /// A bounded-string-length assumption keeps ordinary scalar operations (e.g. string equality on a
    /// schema field) from being flagged as "unbounded" merely for lack of a size hint — string/bytes
    /// comparison cost in CEL's model scales with length. Containers (list/map) still return null,
    /// leaving genuinely unbounded comprehensions (§ "cost estimate rejects an unbounded comprehension")
    /// correctly flagged.
    /// </summary>
    private sealed class NullCostEstimator : ICostEstimator
    {
        private static readonly SizeEstimate AssumedStringSize = new(0, 1024);

        public SizeEstimate? EstimateSize(string? path, CelType type) =>
            type.Kind is CelTypeKind.String or CelTypeKind.Bytes ? AssumedStringSize : null;
    }
}
