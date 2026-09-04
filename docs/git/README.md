# Creating a Git repository

The `iac` CLI creates source-control repositories and their branch policies from a YAML
file, using Pulumi under the hood. It is written in C# so that a .NET team can read and
extend the infrastructure code without learning another language.

GitHub and Azure DevOps are both implemented. The configuration file names its
destination, so pointing the same file at the other host is a one-line change — and the
CLI tells you which settings that host cannot honour instead of dropping them quietly.
See [GitHub vs Azure DevOps](#github-vs-azure-devops).

## Contents

- [What gets created](#what-gets-created)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Commands](#commands)
- [Configuration reference](#configuration-reference)
- [Personal repositories and the approval trap](#personal-repositories-and-the-approval-trap)
- [Bypass actors](#bypass-actors)
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

3. **Credentials for your destination.**

   For **Azure DevOps**, a personal access token in `AZDO_PERSONAL_ACCESS_TOKEN` with
   *Code (read, write & manage)* and *Project and team (read)*. The organization comes
   from the configuration's `organization` (a bare name becomes
   `https://dev.azure.com/<name>`; a full URL is used as given, for Azure DevOps Server).
   Azure DevOps also needs `project` in the configuration — repositories live inside a
   project and this tool does not create projects.

   For **GitHub**, a token in `GITHUB_TOKEN`. Scopes:

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
provider: github            # github | azuredevops
organization: contoso
ownerType: organization     # organization | user
project: Platform           # Azure DevOps only
backend:
  url: file://./.pulumi-state
defaults:
  # any repository setting
repositories:
  - name: widget-api
    # overrides
```

Three example files are provided: `repositories.example.yml` (a GitHub organization, every
setting documented inline), `personal-repo.example.yml` (a user-owned repository) and
`azuredevops.example.yml` (the same schema pointed at Azure DevOps).

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
- On GitHub, an `approvers` entry without a leading `@` while a CODEOWNERS file is being
  written — CODEOWNERS ignores such an owner, so the review requirement would look
  configured and enforce nothing. (Azure DevOps is the opposite: it wants an identity or
  email, and a leading `@` is wrong there. Each provider checks its own, which is why the
  shared schema does not.)
- The user-owned approval trap, described next.

**Approvers vs required reviewers.** GitHub offers two mechanisms and they are not
equivalent:

| | `approvers` + `requireCodeOwnerReview` | `ruleset.requiredReviewers` |
|---|---|---|
| How it works | Writes CODEOWNERS; the ruleset requires an owner's approval | Names reviewers on the ruleset itself |
| Status | Stable, long-standing GitHub behaviour | Documented by the provider as **beta, subject to change** |
| Identifier | `@user` or `@org/team` | The **numeric** team id |
| Recommended | Yes | Only if you need approval requirements scoped to file patterns |

Get a numeric team id with `gh api /orgs/<org>/teams/<slug> --jq .id`.

## Personal repositories and the approval trap

`ownerType` says whether `organization` names an organization or a single user account:

```yaml
organization: octocat
ownerType: user        # organization (default) | user
```

It is not cosmetic. **Nobody can approve their own pull request on GitHub.** On a
repository owned by one person, with nobody else able to approve, `minimumApprovals: 1`
means the owner can never merge into their own default branch — the repository is
bricked by its own policy.

So `ownerType: user` changes two things:

1. **The default approval count becomes 0** instead of 1. The pull request is still
   required, so checks still run and the history stays reviewable; what goes away is an
   approval nobody could give.
2. **Combinations that cannot work become configuration errors** rather than a repository
   nobody can merge into:

   | Rejected on a user-owned repository | Why |
   |---|---|
   | `minimumApprovals` above 0 with no collaborators and no bypass | Nobody could ever approve |
   | `visibility: internal` | Needs an organization |
   | `collaborators.teams` | A user account has no teams |
   | `ruleset.enforcement: evaluate` | GitHub supports evaluate only for organizations |
   | `ruleset.allowAdminBypass` | There is no `OrganizationAdmin` actor to bypass with |
   | An `@org/team` approver | A user account has no teams |

If you *do* want an approval gate on a personal repository, three things satisfy it, and
the error message names all three:

```yaml
# 1. no approval required - the pull request still is
ruleset:
  minimumApprovals: 0

# 2. someone else can approve
ruleset:
  minimumApprovals: 1
collaborators:
  users:
    - name: a-trusted-friend
      permission: push

# 3. keep a break-glass bypass for yourself
#    gh api /user --jq .id
ruleset:
  minimumApprovals: 1
  bypassActors:
    - actorType: User
      actorId: 583231
      bypassMode: pull_request
```

`examples/personal-repo.example.yml` is a complete worked example.

On **Azure DevOps** the same problem has a different answer: its minimum-reviewers policy
supports `SubmitterCanVote`, so the author *can* count towards the approvals. That is
`ruleset.allowSelfApproval`, which GitHub reports as unsupported because it has no such
concept.

## Bypass actors

`ruleset.allowAdminBypass: true` is the shorthand: it adds an `OrganizationAdmin` bypass,
which is the usual break-glass path. Organization-owned repositories only.

For anything more specific, `ruleset.bypassActors` takes the full form. GitHub is precise
about the fields, and the CLI validates them before a run:

| Field | Values |
|---|---|
| `actorType` | `RepositoryRole`, `Team`, `Integration`, `OrganizationAdmin`, `DeployKey`, `EnterpriseOwner`, `User` |
| `actorId` | **Required** for `RepositoryRole`, `Team`, `Integration`, `User`. **Must be omitted** for `OrganizationAdmin`, `EnterpriseOwner`, `DeployKey` — those have no id and GitHub ignores one if sent |
| `bypassMode` | `always` (default), `pull_request`, `exempt` |

`actorType` is CamelCase and `bypassMode` is lowercase, because that is what the provider
validates against; the CLI accepts any casing in the file and normalises it.

Without any bypass, a ruleset with `requirePullRequest` genuinely applies to everyone —
including organization owners. That is usually what you want, but it is worth knowing
before an incident rather than during one.

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

Both providers are implemented. Point a file at the other host and
`iac config validate` prints a warning for every setting that host ignores or
reinterprets - nothing is dropped silently. This table is what those warnings say.

| Setting | GitHub | Azure DevOps |
|---|---|---|
| `organization` | Organization or user | Organization; `project` is also **required** |
| `ownerType` | Changes the approval default and rejects impossible combinations | Not applicable; `allowSelfApproval` plays that role |
| `visibility` | Per repository | Ignored - set on the **project**, inherited by its repositories |
| `features.*` | Per repository (issues, wiki, projects, discussions) | Ignored - Boards and Wiki are per project |
| `topics`, `license`, `gitignoreTemplate` | Repository settings / creation templates | Ignored - no equivalent |
| `ruleset.requirePullRequest` | Ruleset `pull_request` rule | Implicit: a **blocking** policy is what prevents direct pushes |
| `ruleset.minimumApprovals` | `required_approving_review_count`, may be 0 | Minimum-reviewers policy, which requires **at least 1** - a configured 0 becomes 1 reviewer plus self-approval |
| `ruleset.allowSelfApproval` | Not possible; GitHub never allows self-approval | `SubmitterCanVote` |
| `ruleset.dismissStaleReviewsOnPush` | `dismiss_stale_reviews_on_push` | `OnPushResetApprovedVotes` |
| `ruleset.requireLastPushApproval` | `require_last_push_approval` | `LastPusherCannotApprove` |
| `approvers` | CODEOWNERS file (needs `@user` / `@org/team`) | Automatic-reviewers policy (needs an **identity id or email**; no CODEOWNERS exists) |
| `ruleset.requireCodeOwnerReview` | Makes CODEOWNERS approval mandatory | Makes the automatic-reviewers policy *blocking* rather than advisory |
| `ruleset.requireConversationResolution` | `required_review_thread_resolution` | Comment-resolution policy |
| `ruleset.requiredStatusChecks` | Ruleset status-check rule, by check **name** | Ignored - use `buildValidationPipelineIds`, which takes a pipeline **id** |
| `ruleset.requireLinearHistory` | `required_linear_history` | Removes the merge-commit strategy from the merge-types policy |
| `ruleset.enforcement` | `active` / `evaluate` / `disabled` | `active` to blocking; `evaluate` to enabled but non-blocking; `disabled` to policy disabled |
| `ruleset.requireSignedCommits` | `required_signatures` | Ignored - no branch-policy equivalent |
| `ruleset.blockForcePush`, `blockDeletion` | `non_fast_forward`, `deletion` | Ignored - branch **permissions**, not policies |
| `ruleset.allowAdminBypass`, `bypassActors` | Ruleset bypass actors | Ignored - a *bypass policies* permission granted to identities |
| `security.*` | GitHub Advanced Security | Ignored - Advanced Security for Azure DevOps, licensed separately |
| `archiveOnDestroy` | Archives instead of deleting | Ignored - no archive; destroy disables the repository |
| Credentials | `GITHUB_TOKEN` | `AZDO_PERSONAL_ACCESS_TOKEN` |

The two differences that bite hardest:

- **"Who must approve" lives in different places.** On GitHub it is a file in the
  repository (CODEOWNERS); on Azure DevOps it is part of the policy. So the same
  `approvers` list needs `@org/team` for one and an identity or email for the other, and
  each provider rejects or warns about the other's form.
- **Azure DevOps cannot express "a pull request with no approvals".** Its minimum-reviewers
  policy insists on at least one reviewer, and a blocking policy is what forces the pull
  request in the first place. A configured `minimumApprovals: 0` therefore becomes one
  reviewer with self-approval enabled - protected branch, unblocked author - and the CLI
  says so when it does it.

## Adding a provider

`Iac.Provisioning.GitHub` and `Iac.Provisioning.AzureDevOps` are both worked examples of
the following. To add a third:

1. Implement `IResourceProvisioner` (in `Iac.Provisioning`) in a new project.
2. Return your provider key from `ProviderName`, the credentials you need from
   `RequiredEnvironmentVariables`, and your provider's Pulumi config from
   `BuildStackConfiguration`.
3. Report what you cannot honour:
   - `DescribeUnsupportedSettings` - **warnings**: settings you ignore or reinterpret. This
     is how a GitHub-shaped file stays honest when pointed elsewhere.
   - `DescribeConfigurationProblems` - **errors** at the file level, e.g. Azure DevOps
     needing a `project`.
   - `DescribeRepositoryProblems` - **errors** per repository, e.g. GitHub rejecting an
     approver that CODEOWNERS would ignore.
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
| `Iac.Provisioning.AzureDevOps` | The Azure DevOps implementation. The only project that references `Pulumi.AzureDevOps`. |
| `Iac.Provisioning.Tests` | Configuration loading, inheritance, validation, each provisioner's pure logic, and the resource graph both provisioners declare (via Pulumi's mocked engine). |

Commands are grouped by resource (`iac repo ...`), not by provider, because the destination
is a configuration setting. Future resource types slot in as new branches (`iac dns ...`).

## Troubleshooting

**`error: no resource plugin 'github' found`** — Pulumi installs provider plugins on
demand; if that fails (offline, proxy), install it explicitly:

```powershell
pulumi plugin install resource github
pulumi plugin install resource azuredevops
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

**Value casing** - the GitHub provider validates these case-sensitively: `enforcement`,
`bypass_mode` and merge methods are **lowercase** (`active`, `always`, `squash`), while
bypass `actor_type` is **CamelCase** (`OrganizationAdmin`). The CLI accepts any casing in
the configuration file and normalises it before sending, so this should not bite - but it
is why the two look inconsistent in the schema.

**`repository already exists`** — almost always mismatched state: a different
`backend.url`, a different machine's `file://` directory, or a repository created by hand.
Import it instead of recreating:

```powershell
pulumi stack select <repo-name>
pulumi import github:index/repository:Repository <repo-name> <repo-name>
```

## Known gaps

- **Azure DevOps projects are not created.** `project` must name one that already exists.
- **Azure DevOps repository deletion.** Destroy disables the repository rather than
  deleting it, and `archiveOnDestroy` has no effect there.
- **Environments, deployment protection rules, Dependabot configuration and repository
  secrets** are not modelled on either provider.
- **Only the default branch gets a ruleset or policy set.** Release branches and tag
  protection are not covered.
- **GitHub teams and Azure DevOps identities are not created.** They must exist first.
- **Neither provider's own settings are read back for drift** beyond what `--refresh`
  does; there is no "report what differs from the file" command.
