# Rooby

## Objectives
To build a general purpose and version controlled business rule and configuration system and engine for application developers to build other applications with easily managed business rules.

## Requirement
- It is an independent system that provides a user-friendly web interface for users to manage its rules
- The rules and configuration should be versioned but avoid unnecessary duplication (copy-on-write overlays)
- Concurrent editing should be handled gracefully (optimistic concurrency; not live co-editing)
- There will be an API to access all the rules and configuration at any **published** version
- Allow users to publish all changes in one go (atomic per profile)
- Allow users to park in-progress drafts as stashes without publishing
- Rules should support different format, e.g. expression, decision tree, decision table, composite rule, matrix, schedule based rules, connected sub rules
- The top level of rule is a rule set that contains a list of rules (either reusable or ad hoc) and has a execution strategy
- The rule should support list type and allow defining baskets
- Configuration should support key values of different data types (with different input), look up table
- The client runner will execute a rule set, query a lookup or configuration by key
- A project can have multiple profiles
- A project can define many data schema for different configuration, rule, rule set
- Rule permission is profile level
- Configuration is profile level
- Output type of the rule set can be string, number, boolean or list

## Implementation Idea
- Use Google CEL as expression evaluator via [Celly](https://github.com/bsidio/celly) (`Celly` NuGet). Do not use Cel.NET or other CEL ports.
- One **Version** stream per profile, plus a project stream for schemas and project configuration
- Public Version Id: published `1, 2, 3, …`; exactly one draft at `-1`; stashes at `≤ -2` (`FromId` is the published base)
- Drafts are edited in place; publish flips `-1` to the next positive id atomically
- Copy-on-write: write an item revision only when that item changes; `snapshot(N)` is the latest row per item with `0 < VersionId ≤ N` (minus tombstones)
- Project-level schemas are **captured** into each profile version at publish so every published profile version is self-contained
- Stash parks the draft overlay; pop restores it onto an empty draft; apply copies into the current draft or conflicts
- For each profile, there can have a webhook to publish update or a filepath to save the published profile
- Optimistic concurrency should be used
- Use .NET 10, C#, EF Core, PostgreSQL, Docker

Normative detail: [VERSION_CONTROL.md](VERSION_CONTROL.md)