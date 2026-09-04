# Creating a Git repository

The `iac` CLI creates source-control repositories and their branch policies from a YAML
file, using Pulumi under the hood. It is written in C# so that a .NET team can read and
extend the infrastructure code without learning another language.

Today it targets GitHub. The configuration file names its destination, so pointing the
same file at another host is a one-line change once that provider is implemented — see
[Adding a provider](#adding-a-provider) and
[GitHub vs Azure DevOps](#github-vs-azure-devops).

## Contents

- [What gets created](#what-gets-created)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Commands](#commands)
- [Configuration reference](#configuration-reference)
- [Why re-running is safe](#why-re-running-is-safe)
- [Manual steps after creation](#manual-steps-after-creation)
- [GitHub vs Azure DevOps](#github-vs-azure-devops)
- [Adding a provider](#adding-a-provider)
- [How the code is laid out](#how-the-code-is-laid-out)
- [Troubleshooting](#troubleshooting)
- [Known gaps](#known-gaps)

## What gets created

For each repository in the configuration file:

| Resource | Notes |
|---|---|
| The repository | Visibility, description, topics, features, merge policy, vulnerability alerts. Created with an initial commit so it has a default branch. |
| Default branch | Set via `github.BranchDefault` with rename enabled, so an organization whose initial branch is `master` still ends up on `main`. |
| `.github/CODEOWNERS` | Written from `approvers`. This is what makes named approvers enforceable. |
| `.github/pull_request_template.md` | Optional, off by default. |
| Default-branch ruleset | Requires a pull request, sets the approval count, blocks force pushes and deletion, and whatever else is configured. |
| Collaborators | Optional users and teams, which must already exist. |

The repository starts nearly empty: an initial commit, a README you are free to rewrite,
and the files above. This tool does not own your source.

## Prerequisites

1. **.NET 10 SDK.** `global.json` pins the 10.0.2xx band; a lower SDK fails fast with a
   clear message.
2. **The Pulumi CLI on `PATH`.** The Automation API drives the real `pulumi` binary rather
   than reimplementing it, so it must be installed even though you never invoke it
   yourself:

   ```powershell
   winget install Pulumi.Pulumi
   pulumi version
   ```

3. **A GitHub token** in `GITHUB_TOKEN`. Scopes:

   | Scope | Needed for |
   |---|---|
   | `repo` | Creating and configuring repositories, committing CODEOWNERS |
   | `admin:org` | Repository rulesets, team collaborators |
   | `delete_repo` | Only if you set `archiveOnDestroy: false` and intend to delete |

   A fine-grained token needs Administration (read/write), Contents (read/write) and
   Members (read) on the organization.

4. **State storage.** Pick one and set it in `backend.url`:

   | `backend.url` | Setup | Shared with the team? |
   |---|---|---|
   | `file://./.pulumi-state` | none | **No** — see the warning below |
   | `azblob://<container>` | `AZURE_STORAGE_ACCOUNT` plus `AZURE_STORAGE_KEY` or `AZURE_STORAGE_SAS_TOKEN` | Yes |
   | omitted | `PULUMI_ACCESS_TOKEN` (Pulumi Cloud) | Yes |

   > **The `file://` backend is for one person on one machine.** State is what makes a
   > re-run idempotent. If a teammate runs the same configuration against their own local
   > state directory, Pulumi sees no existing resources and tries to create the repository
   > again — which fails, because it already exists on GitHub. For anything shared, use
   > `azblob://` or Pulumi Cloud.

   Self-managed backends (`file://`, `azblob://`) encrypt stack secrets with a passphrase.
   This tool stores no secrets in stack configuration, so `PULUMI_CONFIG_PASSPHRASE` is
   defaulted to empty when unset. Set it explicitly if you would rather not rely on that.
   For concurrent runs against a shared backend also set
   `PULUMI_SELF_MANAGED_STATE_LOCKING=1`.

## Quick start

```powershell
# 1. Copy the example. repositories.yml is git-ignored on purpose.
cp examples/repositories.example.yml repositories.yml

# 2. Edit it: organization, repository names, approvers.

# 3. Check it without touching anything or needing a token.
dotnet run --project src/Iac.Cli -- config validate --config repositories.yml

# 4. See what would happen.
$env:GITHUB_TOKEN = "<token>"
dotnet run --project src/Iac.Cli -- repo create --config repositories.yml --preview

# 5. Do it.
dotnet run --project src/Iac.Cli -- repo create --config repositories.yml
```

To install it as a tool-like command, publish it and put the output on your `PATH`; the
assembly is named `iac`.

## Commands

```
iac config validate --config <path>
iac repo create     --config <path> [--repo <name>] [--preview] [--refresh]
iac repo destroy    --config <path>  --repo <name>  [--yes] [--refresh]
```

| Option | Meaning |
|---|---|
| `-c, --config` | Path to the YAML file. Defaults to `repositories.yml`. |
| `-r, --repo` | Act on one repository from the file. Required for `destroy`. |
| `-p, --provider` | Override the file's `provider` key. |
| `--preview` | Show the changes without making them. |
| `--refresh` | Reconcile state with GitHub first, picking up drift made in the web UI. |
| `--yes` | Skip the destroy confirmation. Required when stdin is redirected. |

`config validate` needs no credentials and contacts nothing — it parses the file, applies
inheritance, and prints what each repository would end up with. Use it in CI on the
configuration file itself.

`repo destroy` deliberately requires `--repo`. There is no "destroy everything in this
file" command. With `archiveOnDestroy: true` (the default) the repository is archived, not
deleted.

## Configuration reference

The example file at [`examples/repositories.example.yml`](../../examples/repositories.example.yml)
documents every setting inline with its default. The shape is:

```yaml
provider: github
organization: contoso
backend:
  url: file://./.pulumi-state
defaults:
  # any repository setting
repositories:
  - name: widget-api
    # overrides
```

**Inheritance.** Precedence is built-in default → `defaults:` → the repository entry.
Overriding one value inside a block (say `ruleset.minimumApprovals`) keeps the rest of that
block. **Lists replace rather than merge** — an entry's `topics` or `approvers` fully
supersede the inherited ones, so that a single repository can drop an inherited approver.

**Unknown settings are an error.** A typo like `minimumAprovals` fails the run instead of
silently leaving a protection rule unset.

**Combinations that are rejected**, because each would leave a repository permanently
unmergeable or unprotected:

- `ruleset.requireCodeOwnerReview: true` with an empty `approvers` list — CODEOWNERS would
  have no owners, so no pull request could ever be approved.
- `ruleset.requireCodeOwnerReview: true` with `files.codeowners: false` — same outcome by a
  different route.
- All three of `merge.allowSquash`, `allowMergeCommit`, `allowRebase` false.
- `ruleset.requireLinearHistory: true` together with `merge.allowMergeCommit: true`.

**Approvers vs required reviewers.** GitHub offers two mechanisms and they are not
equivalent:

| | `approvers` + `requireCodeOwnerReview` | `ruleset.requiredReviewers` |
|---|---|---|
| How it works | Writes CODEOWNERS; the ruleset requires an owner's approval | Names reviewers on the ruleset itself |
| Status | Stable, long-standing GitHub behaviour | Documented by the provider as **beta, subject to change** |
| Identifier | `@user` or `@org/team` | The **numeric** team id |
| Recommended | Yes | Only if you need approval requirements scoped to file patterns |

Get a numeric team id with `gh api /orgs/<org>/teams/<slug> --jq .id`.

## Why re-running is safe

Pulumi records the state of what it created. `iac repo create` declares the desired state;
Pulumi diffs that against the record and does only what is missing. Running it twice with
an unchanged file reports `no changes`.

Each repository gets **its own Pulumi stack**, named after the repository, inside a project
named `iac-github` (override with `backend.projectName`). Two consequences worth knowing:

- Adding a repository to the file never touches the others' state.
- Renaming a repository in the file looks like "destroy the old one, create a new one",
  because the stack name changed. Rename in GitHub and in the file, then use
  `pulumi stack rename` against the backend if you need the history to follow.

Changes made by hand in the GitHub UI are invisible until you pass `--refresh`, which
reconciles state first and then shows the drift as changes to be undone.

## Manual steps after creation

These cannot be done at creation time, by this tool or any other:

1. **Required status checks.** A check cannot be *required* until it has reported at least
   once — GitHub has nothing to reference before a workflow has run. There is no way around
   this, so the flow is two passes:

   1. `iac repo create` with `ruleset.requiredStatusChecks: []`.
   2. Add the workflow to the repository and push, so it runs once.
   3. Put the job names in `ruleset.requiredStatusChecks`.
   4. `iac repo create` again — the ruleset is updated in place.

2. **Actions secrets and variables.** Deliberately out of scope: they do not belong in a
   plain-text file in source control. Use `gh secret set`, the UI, or a secret store.

3. **Environments and deployment protection rules.** Not modelled yet.

4. **Teams.** A team must already exist in the organization before it can be a collaborator
   or a code owner. This tool does not create teams.

5. **Actions permissions.** Whether Actions can run, which actions are allowed, and default
   workflow token permissions are organization-level settings the repository inherits.

6. **Branch protection for non-default branches.** Only the default branch gets a ruleset.

## GitHub vs Azure DevOps

Kept here so that a configuration written for one is not silently misread when pointed at
the other.

| Setting | GitHub | Azure DevOps |
|---|---|---|
| `organization` | Organization or user | Organization; a **project** is also required, which this schema does not model yet |
| `visibility` | Per repository | Per **project** — individual repositories inherit it |
| `features.*` | Per repository (issues, wiki, projects, discussions) | Per project (Boards, Wiki); no per-repository equivalent |
| `topics` | Per repository | No equivalent |
| `ruleset.requirePullRequest` | Ruleset `pull_request` rule | Branch policy: *Require a minimum number of reviewers* |
| `ruleset.minimumApprovals` | `required_approving_review_count` | The same policy's minimum reviewer count |
| `ruleset.dismissStaleReviewsOnPush` | `dismiss_stale_reviews_on_push` | *Reset votes on source push* |
| `approvers` | CODEOWNERS file + `requireCodeOwnerReview` | **Required reviewers policy** — takes identities directly, so no file is needed |
| `ruleset.requireConversationResolution` | `required_review_thread_resolution` | *Comment requirements* policy |
| `ruleset.requiredStatusChecks` | Ruleset status-check rule | *Build validation* policy, which references a pipeline |
| `ruleset.requireLinearHistory` | `required_linear_history` | Merge-strategy policy (squash only) |
| `ruleset.blockForcePush` | `non_fast_forward` | Implicit; force push is a branch permission |
| `security.secretScanning` | GitHub Advanced Security | Advanced Security for Azure DevOps, licensed separately |
| Credentials | `GITHUB_TOKEN` | A PAT plus the organization service URL |

The practical difference: on GitHub, "who must approve" lives in a file in the repository;
on Azure DevOps it lives in the policy. That is why `approvers` writes CODEOWNERS here and
would become a policy there.

## Adding a provider

1. Implement `IResourceProvisioner` (in `Iac.Provisioning`) in a new project, e.g.
   `Iac.Provisioning.AzureDevOps`.
2. Return your provider key from `ProviderName`, the credentials you need from
   `RequiredEnvironmentVariables`, and your provider's Pulumi config from
   `BuildStackConfiguration`.
3. Report anything in the configuration you cannot honour from
   `DescribeUnsupportedSettings`. The CLI prints these as warnings, which is how a
   GitHub-shaped file stays honest when pointed elsewhere.
4. Declare your resources in `Provision`.
5. Register it in `RunPreparation.CreateRegistry()`.

Nothing else changes: the CLI, the configuration model and the Pulumi runner are all
provider-agnostic.

## How the code is laid out

| Project | Responsibility |
|---|---|
| `Iac.Cli` | Command-line surface (Spectre.Console.Cli) and the Pulumi Automation API runner. No provider knowledge beyond the registry. |
| `Iac.Provisioning` | Configuration model, YAML loading, validation, inheritance, and the `IResourceProvisioner` seam. No provider references. |
| `Iac.Provisioning.GitHub` | The GitHub implementation. The only project that references `Pulumi.Github`. |
| `Iac.Provisioning.Tests` | Tests for configuration loading, inheritance, validation and the provisioner's pure logic. |

Commands are grouped by resource (`iac repo ...`), not by provider, because the destination
is a configuration setting. Future resource types slot in as new branches (`iac dns ...`).

## Troubleshooting

**`error: no resource plugin 'github' found`** — Pulumi installs provider plugins on
demand; if that fails (offline, proxy), install it explicitly:

```powershell
pulumi plugin install resource github
```

**`Testing with VSTest target is no longer supported`** — you removed or broke
`global.json`. xUnit v3 runs on Microsoft.Testing.Platform, and the .NET 10 SDK requires
opting in through `global.json`:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

**`NU1902` / `NU1903` on restore** — `TreatWarningsAsErrors` promotes NuGet audit warnings
to errors. That is intentional. Fix it by pinning a patched version in
`Directory.Packages.props` (transitive pinning is enabled, so naming a transitive there
lifts it), not by suppressing the advisory. There is a worked example in that file for
OpenTelemetry.

**`passphrase must be set`** — a self-managed backend with no passphrase available in the
environment. Set `PULUMI_CONFIG_PASSPHRASE`.

**A ruleset value is rejected by GitHub** — string values (`enforcement`, `visibility`,
merge methods) are sent lowercase, matching the GitHub REST API. The Pulumi registry
documentation renders them capitalised in prose. If a future provider version validates
strictly on the capitalised form, that is the thing to check first.

**`repository already exists`** — almost always mismatched state: a different
`backend.url`, a different machine's `file://` directory, or a repository created by hand.
Import it instead of recreating:

```powershell
pulumi stack select <repo-name>
pulumi import github:index/repository:Repository <repo-name> <repo-name>
```

## Known gaps

- Azure DevOps is described here but not implemented; `provider: azuredevops` is rejected
  with a list of known providers.
- Azure DevOps projects are not modelled.
- No tests exercise the Pulumi resource graph itself. The configuration layer is covered;
  the resource shaping is verified with `--preview` against GitHub. Adding
  `Pulumi.Testing` with mocks would close this.
- Environments, deployment protection rules, Dependabot configuration and repository
  secrets are not modelled.
- Only the default branch gets a ruleset.
