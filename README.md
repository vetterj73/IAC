# IAC

Infrastructure as code for our estate, written in C# with [Pulumi](https://www.pulumi.com/)
so that a .NET team can read and extend it without picking up another language.

Everything is driven by one CLI, `iac`, which grows a command branch per resource type.

## What you can do today

| Task | Command | Documentation |
|---|---|---|
| Create a GitHub repository with a branch ruleset | `iac repo create --config repositories.yml` | [docs/git](docs/git/README.md) |
| Create an Azure DevOps repository with branch policies | the same command, with `provider: azuredevops` in the file | [docs/git](docs/git/README.md) |

The destination is a setting in the configuration file, not a different command. Pointing a
file at the other host prints a warning for every setting that host cannot honour rather
than dropping it silently.

## Getting started

```powershell
dotnet build
cp examples/repositories.example.yml repositories.yml   # then edit it
dotnet run --project src/Iac.Cli -- config validate --config repositories.yml
```

`repositories.yml` is git-ignored on purpose — it names real organizations, teams and
people. Only the examples are committed:

| Example | For |
|---|---|
| [`examples/repositories.example.yml`](examples/repositories.example.yml) | A GitHub organization, with every setting documented inline |
| [`examples/personal-repo.example.yml`](examples/personal-repo.example.yml) | A personal (user-owned) repository — start here if the owner is one person, because a required approval nobody can give locks them out |
| [`examples/azuredevops.example.yml`](examples/azuredevops.example.yml) | The same schema pointed at Azure DevOps |

See [docs/git](docs/git/README.md) for prerequisites (the Pulumi CLI and a `GITHUB_TOKEN`
are both required), the full configuration reference, the manual steps that cannot be
automated at creation time, and how the same configuration maps onto Azure DevOps.

## Repository layout

```
src/Iac.Cli                       the iac command line
src/Iac.Provisioning              configuration model and the provider-agnostic seam
src/Iac.Provisioning.GitHub       the GitHub implementation
src/Iac.Provisioning.AzureDevOps  the Azure DevOps implementation
tests/                            tests
docs/git                          creating Git repositories
examples/                         example configuration files
```

## Working in this repository

Build settings are centralised: `Directory.Build.props` sets the target framework and
turns warnings into errors, `Directory.Packages.props` holds every package version
(central package management — project files carry no versions), and `.editorconfig` holds
the style and analyzer rules. Project files are deliberately almost empty.

```powershell
dotnet build      # warnings are errors, code style is enforced in the build
dotnet test
```

`global.json` pins the SDK band and opts `dotnet test` into Microsoft.Testing.Platform,
which xUnit v3 requires. Do not delete it.
