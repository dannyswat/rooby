using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Rooby.Api.Validation;

namespace Rooby.Api.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("ValidCode", true)]
    [InlineData("valid_code_1", true)]
    [InlineData("1InvalidStart", false)]
    [InlineData("invalid-dash", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Identifier_validator_matches_the_spec_pattern(string? value, bool expected) =>
        Assert.Equal(expected, IdentifierValidator.IsValid(value));

    [Theory]
    [InlineData("Asia/Hong_Kong", true)]
    [InlineData("UTC", true)]
    [InlineData("Not/A_Zone", false)]
    [InlineData("", false)]
    public void Time_zone_validator_only_accepts_known_ids(string value, bool expected) =>
        Assert.Equal(expected, TimeZoneValidator.IsValid(value));

    [Fact]
    public void Publish_uri_validator_accepts_null()
    {
        var config = BuildConfiguration();
        Assert.True(PublishUriValidator.IsValid(null, config));
    }

    [Fact]
    public void Publish_uri_validator_rejects_https_host_not_on_allow_list()
    {
        var config = BuildConfiguration(allowedHosts: ["allowed.example.com"]);
        Assert.False(PublishUriValidator.IsValid("https://evil.example.com/webhook", config));
    }

    [Fact]
    public void Publish_uri_validator_accepts_https_host_on_allow_list()
    {
        var config = BuildConfiguration(allowedHosts: ["allowed.example.com"]);
        Assert.True(PublishUriValidator.IsValid("https://allowed.example.com/webhook", config));
    }

    [Fact]
    public void Publish_uri_validator_rejects_file_path_outside_export_root()
    {
        var config = BuildConfiguration(exportRoot: "/exports");
        Assert.False(PublishUriValidator.IsValid("file:///etc/passwd", config));
    }

    [Fact]
    public void Publish_uri_validator_accepts_file_path_under_export_root()
    {
        var config = BuildConfiguration(exportRoot: "/exports");
        Assert.True(PublishUriValidator.IsValid("file:///exports/profile.json", config));
    }

    [Fact]
    public void Schema_validator_accepts_the_spec_example_definition()
    {
        using var document = JsonDocument.Parse("""
            { "$v": 1, "type": "object", "properties": {
                "product": { "type": "object", "properties": {
                    "type": { "type": "string", "enum": ["ELN", "FCN"] },
                    "tenorMonths": { "type": "integer", "minimum": 1 } }, "required": ["type"] },
                "trade": { "type": "object", "properties": {
                    "tradeDate": { "type": "string", "format": "date" } } }
              }, "required": ["product"] }
            """);

        Assert.Empty(RoobySchemaValidator.Validate(document.RootElement));
    }

    [Fact]
    public void Schema_validator_rejects_unsupported_keyword()
    {
        using var document = JsonDocument.Parse("""{ "$v": 1, "type": "object", "pattern": "^x$" }""");

        var errors = RoobySchemaValidator.Validate(document.RootElement);

        Assert.Contains(errors, e => e.Contains("pattern", StringComparison.Ordinal));
    }

    [Fact]
    public void Schema_validator_rejects_unsupported_type()
    {
        using var document = JsonDocument.Parse("""
            { "$v": 1, "type": "object", "properties": { "x": { "type": "null" } } }
            """);

        var errors = RoobySchemaValidator.Validate(document.RootElement);

        Assert.Contains(errors, e => e.Contains("unsupported type", StringComparison.Ordinal));
    }

    private static IConfiguration BuildConfiguration(string[]? allowedHosts = null, string? exportRoot = null)
    {
        var data = new Dictionary<string, string?> { ["Publish:ExportRoot"] = exportRoot };
        var hosts = allowedHosts ?? [];
        for (var i = 0; i < hosts.Length; i++)
        {
            data[$"Publish:AllowedHosts:{i}"] = hosts[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }
}
