namespace Rooby.Api.Data.Entities;

/// <summary>Shared shape of item/item_line/test_case copy-on-write revision rows (SPEC §7).</summary>
public interface IVersionedRow
{
    Guid Id { get; }

    Guid ProfileId { get; }

    int VersionId { get; }

    bool IsDeleted { get; }
}
