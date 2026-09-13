using Rooby.Api.Data.Entities;

namespace Rooby.Api.Versioning;

/// <summary>A reconstructed view of a profile at some point in time (SPEC §7.1): a published snapshot,
/// the working set (snapshot + draft overlay), or a stash preview.</summary>
public sealed record VersionSnapshot(IReadOnlyList<Item> Items, IReadOnlyList<ItemLine> Lines, IReadOnlyList<TestCase> TestCases);
