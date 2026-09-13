using Rooby.Engine.Cel;

namespace Rooby.Engine.Tests.Cel;

public sealed class DecisionTableCellCompilerTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData("-", null)]
    [InlineData("'Gold'", "__cell == ('Gold')")]
    [InlineData(">= 1000000", "__cell >= 1000000")]
    [InlineData("!= 'Gold'", "__cell != 'Gold'")]
    [InlineData("in ['A', 'B']", "__cell in ['A', 'B']")]
    [InlineData("in ref.EligibleTiers", "__cell in ref.EligibleTiers")]
    [InlineData("[0, 1000000)", "__cell >= 0 && __cell < 1000000")]
    [InlineData("(0, 100]", "__cell > 0 && __cell <= 100")]
    [InlineData("$ != null && $.startsWith('A')", "__cell != null && __cell.startsWith('A')")]
    public void Compiles_cell_grammar_per_spec(string cellText, string? expected)
    {
        Assert.Equal(expected, DecisionTableCellCompiler.Compile(cellText));
    }
}
