using System.Text.Json;
using Rooby.Engine.Evaluation;

namespace Rooby.Engine.Tests.Evaluation;

/// <summary>Small fluent helpers for building test <see cref="Bundle"/>s without repetitive boilerplate.</summary>
public static class TestBundle
{
    public static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static BundleItem Item(string key, ItemType type, DataType dataType, string contentJson, params BundleLine[] lines) =>
        new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            ItemType = type,
            DataType = dataType,
            Content = ParseJson(contentJson),
            Lines = lines,
        };

    public static BundleLine Line(string contentJson, int sortOrder = 1000, LineValidity? validity = null) =>
        new() { Id = Guid.NewGuid(), SortOrder = sortOrder, Content = ParseJson(contentJson), Validity = validity };

    public static Bundle Bundle(params BundleItem[] items) =>
        new() { Project = "Test", Profile = "Test", TimeZone = "UTC", VersionId = 1, Items = items };
}
