# IAC

Infrastructure as code for our estate, written in C# with Pulumi. See
[README.md](README.md) for what the `iac` CLI does and [docs/git](docs/git/README.md) for
creating repositories.

## Build conventions

Build settings are centralised and project files are deliberately almost empty:

- `Directory.Build.props` — target framework, nullability, `TreatWarningsAsErrors`,
  `EnforceCodeStyleInBuild`. Do not repeat these in a `.csproj`.
- `Directory.Packages.props` — every package version (central package management). A
  `PackageReference` never carries a `Version`.
- `tests/Directory.Build.props` — the test stack. A test `.csproj` holds only its
  `ProjectReference`s.
- `.editorconfig` — style and analyzer severities. `stylecop.json` configures StyleCop
  itself.

Warnings are errors here, and code style is enforced in the build, so `dotnet build` is the
arbiter: 0 warnings is the only acceptable result.

## C# file organization

**One type per file.** Each class, record, struct, interface, delegate and enum goes in its
own file, named after the type it contains. When adding a type, create a new file for it —
never append it to an existing file, and never group several small related types together
because they feel connected.

Nested types belong to their containing type and do not count as separate types.

**The only exception is a constants file.** A file whose purpose is to hold constant values
may contain more than one class, so that related constants can be grouped into several
static classes in one place.

This applies to new files and to any existing file you modify.

### Enforcement

`.editorconfig` sets `SA1402` ("File may only contain a single type") to `error`, so the
build fails on a second type in a file. `SA1649` ("File name should match first type name")
is also `error`. Both are active because `StyleCop.Analyzers` is referenced as a
`GlobalPackageReference` in `Directory.Packages.props`.

Note that `SA1402` and `SA1649` only police classes, so an enum or interface sharing a file
slips past the analyzer. The rule above still applies to them — follow the rule, not just
the analyzer.

`SA1402` has no notion of a constants file, so the exception needs an explicit suppression.
Put it at the top of the constants file, with the reason:

```csharp
#pragma warning disable SA1402 // constants file: grouped static classes are intentional

namespace Example
{
    internal static class RetryConstants { /* ... */ }

    internal static class TimeoutConstants { /* ... */ }
}
```

Suppress it in the constants file only. A pragma anywhere else means the rule is being
worked around rather than followed — put the type in its own file instead.

## Member order

StyleCop's `SA1201`/`SA1204` ordering is enforced, and it is stricter than most people
expect. Within a type: constants and fields, then constructors, then properties, then
methods, then **nested types last**. Within each of those, static members come before
instance members, and more accessible before less accessible.

The two that catch people out:

- A `private const` declared at the bottom of a class next to where it is used fails
  `SA1201`. Constants go at the top.
- A nested `Settings` class declared first (the natural place to read it) fails `SA1201`.
  Nested types go after the methods.
