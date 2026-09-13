using System.Text.Json;
using Rooby.Engine.Cel;
using Rooby.Engine.Content;
using Rooby.Engine.Schema;

namespace Rooby.Engine.Tests.Cel;

public sealed class CelCompilerTests
{
    private static RoobySchema BuildSchema()
    {
        const string json = """
            {"$v":1,"type":"object",
             "properties":{
               "product":{"type":"object","properties":{"type":{"type":"string"}},"required":["type"]},
               "trade":{"type":"object","properties":{"notional":{"type":"number"}}},
               "legs":{"type":"array","items":{"type":"object","properties":{"strike":{"type":"number"}}}}
             },
             "required":["product"]}
            """;
        return ItemContentSerializer.Deserialize<RoobySchema>(JsonDocument.Parse(json).RootElement);
    }

    [Fact]
    public void Checked_mode_fails_on_unknown_field()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileChecked("input.product.bogus == 'x'", BuildSchema(), "schema-1");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("bogus", StringComparison.Ordinal));
    }

    [Fact]
    public void Checked_mode_fails_on_type_mismatch()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileChecked("input.trade.notional == 'not a number'", BuildSchema(), "schema-1");

        Assert.False(result.Success);
    }

    [Fact]
    public void Checked_mode_succeeds_on_well_typed_expression()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileChecked("input.product.type == 'ELN' && input.trade.notional > 0.0", BuildSchema(), "schema-1");

        Assert.True(result.Success);
        Assert.NotNull(result.Program);
    }

    [Fact]
    public void Dynamic_mode_compiles_expression_that_fails_checked()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileDynamic("input.product.bogus == 'x'");

        Assert.True(result.Success);
    }

    [Fact]
    public void Cost_estimate_rejects_an_unbounded_comprehension()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileChecked("input.legs.all(l, l.strike > 0.0)", BuildSchema(), "schema-1");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cost_estimate_allows_simple_expressions()
    {
        var compiler = new CelCompiler();
        var result = compiler.CompileChecked("input.trade.notional + 1.0", BuildSchema(), "schema-1");

        Assert.True(result.Success);
    }
}
