using System.Text.Json;
using Rooby.Engine.Content;
using Rooby.Engine.Dependencies;

namespace Rooby.Engine.Tests.Dependencies;

public sealed class DependencyExtractorTests
{
    private readonly DependencyExtractor _extractor = new();

    [Fact]
    public void ExpressionRule_collects_binding_lookup_and_ref_in_expression()
    {
        var content = Serialize(new ExpressionRuleContent
        {
            Bindings = [new ExpressionBinding { Name = "spread", Lookup = "TenorSpreadBps", Keys = ["input.product.tenor"] }],
            Expression = "vars.spread + ref.BaseMarkup",
        });

        var keys = _extractor.Extract(ItemType.ExpressionRule, content, lines: []);

        Assert.Contains("TenorSpreadBps", keys);
        Assert.Contains("BaseMarkup", keys);
    }

    [Fact]
    public void RuleList_collects_reference_and_bind_steps()
    {
        var lines = new[]
        {
            Serialize<RuleListStep>(new RuleListReferenceStep { Ref = "ClientTierMarkup" }),
            Serialize<RuleListStep>(new RuleListBindStep { Bind = "spread", Lookup = "TenorSpreadBps", Keys = ["input.product.tenor"] }),
        };

        var keys = _extractor.Extract(ItemType.RuleList, Serialize(new RuleListContent
        {
            Strategy = RuleListStrategy.FirstMatch,
            Output = new RuleListOutputSpec { Type = DataType.Number },
        }), lines);

        Assert.Contains("ClientTierMarkup", keys);
        Assert.Contains("TenorSpreadBps", keys);
    }

    [Fact]
    public void Matrix_collects_probe_and_cell_refs()
    {
        var content = Serialize(new MatrixContent
        {
            Rows = new MatrixAxis { Name = "notional", Type = DataType.Number, Match = MatrixAxisMatch.Range, Probe = "input.trade.notional" },
            Cols = new MatrixAxis
            {
                Name = "tenor",
                Type = DataType.String,
                Match = MatrixAxisMatch.Exact,
                Probe = "input.product.tenor",
                Headers = [new MatrixHeader { Key = ToElement("\"1Y\"") }],
            },
            Cell = new MatrixCellSpec { Type = DataType.Number },
        });

        var line = Serialize(new MatrixRow
        {
            Header = new MatrixHeader { From = ToElement("0"), To = ToElement("1000000") },
            Cells = new Dictionary<string, ValueSlot> { ["1Y"] = ValueSlot.FromRef("SmallTicket2Y") },
        });

        var keys = _extractor.Extract(ItemType.Matrix, content, [line]);

        Assert.Contains("SmallTicket2Y", keys);
    }

    [Fact]
    public void DecisionTable_collects_condition_column_refs_and_output_slot_refs()
    {
        var content = Serialize(new DecisionTableContent
        {
            Columns =
            [
                new DecisionTableColumn { Name = "tier", Kind = DecisionTableColumnKind.Condition, Expression = "input.client.tier in ref.EligibleTiers" },
                new DecisionTableColumn { Name = "markup", Kind = DecisionTableColumnKind.Output, Type = DataType.Number },
            ],
        });

        var line = Serialize(new DecisionTableRow
        {
            Cells = new Dictionary<string, string> { ["tier"] = "'Gold'" },
            Output = new Dictionary<string, ValueSlot> { ["markup"] = ValueSlot.FromRef("BaseMarkup") },
        });

        var keys = _extractor.Extract(ItemType.DecisionTable, content, [line]);

        Assert.Contains("EligibleTiers", keys);
        Assert.Contains("BaseMarkup", keys);
    }

    [Fact]
    public void Inline_rule_dependencies_are_collected_recursively_through_nested_slots()
    {
        // RuleList inline step -> Matrix (depth 1) -> cell inline rule -> ExpressionRule (depth 2) referencing ref.Deep.
        var innermostContent = Serialize(new ExpressionRuleContent { Expression = "ref.Deep" });
        var innerRule = new InlineRuleSpec { ItemType = ItemType.ExpressionRule, Content = innermostContent };

        var matrixContent = Serialize(new MatrixContent
        {
            Rows = new MatrixAxis { Name = "r", Type = DataType.String, Match = MatrixAxisMatch.Exact },
            Cols = new MatrixAxis { Name = "c", Type = DataType.String, Match = MatrixAxisMatch.Exact, Headers = [new MatrixHeader { Key = ToElement("\"A\"") }] },
            Cell = new MatrixCellSpec { Type = DataType.Number },
        });
        var matrixLine = Serialize(new MatrixRow
        {
            Header = new MatrixHeader { Key = ToElement("\"row1\"") },
            Cells = new Dictionary<string, ValueSlot> { ["A"] = ValueSlot.FromRule(innerRule) },
        });
        var outerRule = new InlineRuleSpec { ItemType = ItemType.Matrix, Content = matrixContent, Lines = [matrixLine] };

        var ruleListLine = Serialize<RuleListStep>(new RuleListInlineStep { Rule = outerRule });
        var keys = _extractor.Extract(ItemType.RuleList, Serialize(new RuleListContent
        {
            Strategy = RuleListStrategy.FirstMatch,
            Output = new RuleListOutputSpec { Type = DataType.Number },
        }), [ruleListLine]);

        Assert.Contains("Deep", keys);
    }

    private static JsonElement Serialize<T>(T value) => ItemContentSerializer.Serialize(value);

    private static JsonElement ToElement(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
