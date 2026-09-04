using System;
using System.Collections.Generic;
using System.Linq;

namespace Iac.Provisioning.Configuration
{
    /// <summary>
    /// Collapses built-in defaults, the file's <c>defaults:</c> block and one repository entry
    /// into a <see cref="ResolvedRepository"/>. Precedence, lowest first: built-in, file
    /// defaults, entry.
    /// </summary>
    public static class RepositoryResolver
    {
        /// <summary>Resolves every repository in the configuration, in file order.</summary>
        public static IReadOnlyList<ResolvedRepository> ResolveAll(IacConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            IList<RepositoryOptions> entries = configuration.Repositories ?? new List<RepositoryOptions>();
            return entries.Select(entry => Resolve(configuration.Defaults, entry)).ToList();
        }

        /// <summary>Resolves a single entry against the supplied defaults.</summary>
        public static ResolvedRepository Resolve(RepositoryOptions? defaults, RepositoryOptions entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return new ResolvedRepository
            {
                Name = entry.Name ?? throw new ConfigurationException("A repository entry is missing 'name'."),
                Description = Pick(entry.Description, defaults?.Description),
                Homepage = Pick(entry.Homepage, defaults?.Homepage),
                Visibility = Pick(entry.Visibility, defaults?.Visibility) ?? "private",
                DefaultBranch = Pick(entry.DefaultBranch, defaults?.DefaultBranch) ?? "main",
                Topics = PickList(entry.Topics, defaults?.Topics),
                License = Pick(entry.License, defaults?.License),
                GitignoreTemplate = Pick(entry.GitignoreTemplate, defaults?.GitignoreTemplate),
                ArchiveOnDestroy = entry.ArchiveOnDestroy ?? defaults?.ArchiveOnDestroy ?? true,
                Features = ResolveFeatures(defaults?.Features, entry.Features),
                Merge = ResolveMerge(defaults?.Merge, entry.Merge),
                Security = ResolveSecurity(defaults?.Security, entry.Security),
                Ruleset = ResolveRuleset(defaults?.Ruleset, entry.Ruleset),
                Approvers = PickList(entry.Approvers, defaults?.Approvers),
                UserCollaborators = ResolveCollaborators(
                    entry.Collaborators?.Users,
                    defaults?.Collaborators?.Users,
                    "push"),
                TeamCollaborators = ResolveCollaborators(
                    entry.Collaborators?.Teams,
                    defaults?.Collaborators?.Teams,
                    "push"),
                Files = ResolveFiles(defaults?.Files, entry.Files),
            };
        }

        private static ResolvedFeatures ResolveFeatures(FeatureOptions? defaults, FeatureOptions? entry)
        {
            return new ResolvedFeatures
            {
                Issues = entry?.Issues ?? defaults?.Issues ?? true,
                Wiki = entry?.Wiki ?? defaults?.Wiki ?? false,
                Projects = entry?.Projects ?? defaults?.Projects ?? false,
                Discussions = entry?.Discussions ?? defaults?.Discussions ?? false,
            };
        }

        private static ResolvedMerge ResolveMerge(MergeOptions? defaults, MergeOptions? entry)
        {
            return new ResolvedMerge
            {
                AllowSquash = entry?.AllowSquash ?? defaults?.AllowSquash ?? true,
                AllowMergeCommit = entry?.AllowMergeCommit ?? defaults?.AllowMergeCommit ?? false,
                AllowRebase = entry?.AllowRebase ?? defaults?.AllowRebase ?? false,
                AllowAutoMerge = entry?.AllowAutoMerge ?? defaults?.AllowAutoMerge ?? true,
                DeleteBranchOnMerge = entry?.DeleteBranchOnMerge ?? defaults?.DeleteBranchOnMerge ?? true,
                AllowUpdateBranch = entry?.AllowUpdateBranch ?? defaults?.AllowUpdateBranch ?? true,
            };
        }

        private static ResolvedSecurity ResolveSecurity(SecurityOptions? defaults, SecurityOptions? entry)
        {
            return new ResolvedSecurity
            {
                VulnerabilityAlerts = entry?.VulnerabilityAlerts ?? defaults?.VulnerabilityAlerts ?? true,
                SecretScanning = entry?.SecretScanning ?? defaults?.SecretScanning ?? false,
                SecretScanningPushProtection =
                    entry?.SecretScanningPushProtection ?? defaults?.SecretScanningPushProtection ?? false,
            };
        }

        private static ResolvedRuleset ResolveRuleset(RulesetOptions? defaults, RulesetOptions? entry)
        {
            return new ResolvedRuleset
            {
                Enforcement = Pick(entry?.Enforcement, defaults?.Enforcement) ?? "active",
                RequirePullRequest = entry?.RequirePullRequest ?? defaults?.RequirePullRequest ?? true,
                MinimumApprovals = entry?.MinimumApprovals ?? defaults?.MinimumApprovals ?? 1,
                DismissStaleReviewsOnPush =
                    entry?.DismissStaleReviewsOnPush ?? defaults?.DismissStaleReviewsOnPush ?? true,
                RequireCodeOwnerReview =
                    entry?.RequireCodeOwnerReview ?? defaults?.RequireCodeOwnerReview ?? false,
                RequireLastPushApproval =
                    entry?.RequireLastPushApproval ?? defaults?.RequireLastPushApproval ?? false,
                RequireConversationResolution =
                    entry?.RequireConversationResolution ?? defaults?.RequireConversationResolution ?? true,
                RequireLinearHistory = entry?.RequireLinearHistory ?? defaults?.RequireLinearHistory ?? false,
                RequireSignedCommits = entry?.RequireSignedCommits ?? defaults?.RequireSignedCommits ?? false,
                BlockForcePush = entry?.BlockForcePush ?? defaults?.BlockForcePush ?? true,
                BlockDeletion = entry?.BlockDeletion ?? defaults?.BlockDeletion ?? true,
                RequiredStatusChecks = PickList(entry?.RequiredStatusChecks, defaults?.RequiredStatusChecks),
                BypassActors = ResolveBypassActors(entry?.BypassActors ?? defaults?.BypassActors),
                RequiredReviewers = ResolveRequiredReviewers(
                    entry?.RequiredReviewers ?? defaults?.RequiredReviewers),
            };
        }

        private static IReadOnlyList<ResolvedBypassActor> ResolveBypassActors(IList<BypassActorOptions>? actors)
        {
            if (actors is null)
            {
                return Array.Empty<ResolvedBypassActor>();
            }

            return actors
                .Select(static actor => new ResolvedBypassActor
                {
                    ActorId = actor.ActorId
                        ?? throw new ConfigurationException("A ruleset bypass actor is missing 'actorId'."),
                    ActorType = actor.ActorType
                        ?? throw new ConfigurationException("A ruleset bypass actor is missing 'actorType'."),
                    BypassMode = actor.BypassMode ?? "always",
                })
                .ToList();
        }

        private static IReadOnlyList<ResolvedRequiredReviewer> ResolveRequiredReviewers(
            IList<RequiredReviewerOptions>? reviewers)
        {
            if (reviewers is null)
            {
                return Array.Empty<ResolvedRequiredReviewer>();
            }

            return reviewers
                .Select(static reviewer => new ResolvedRequiredReviewer
                {
                    Id = reviewer.Id
                        ?? throw new ConfigurationException(
                            "A required reviewer is missing 'id' (the numeric team id)."),
                    Type = reviewer.Type ?? "Team",
                    MinimumApprovals = reviewer.MinimumApprovals ?? 1,
                    FilePatterns = reviewer.FilePatterns is null
                        ? new[] { "**/*" }
                        : reviewer.FilePatterns.ToArray(),
                })
                .ToList();
        }

        private static IReadOnlyList<ResolvedCollaborator> ResolveCollaborators(
            IList<CollaboratorEntry>? entry,
            IList<CollaboratorEntry>? defaults,
            string defaultPermission)
        {
            IList<CollaboratorEntry>? source = entry ?? defaults;
            if (source is null)
            {
                return Array.Empty<ResolvedCollaborator>();
            }

            return source
                .Select(collaborator => new ResolvedCollaborator
                {
                    Name = collaborator.Name
                        ?? throw new ConfigurationException("A collaborator entry is missing 'name'."),
                    Permission = collaborator.Permission ?? defaultPermission,
                })
                .ToList();
        }

        private static ResolvedFiles ResolveFiles(GeneratedFileOptions? defaults, GeneratedFileOptions? entry)
        {
            return new ResolvedFiles
            {
                Readme = entry?.Readme ?? defaults?.Readme ?? true,
                Codeowners = entry?.Codeowners ?? defaults?.Codeowners ?? true,
                PullRequestTemplate =
                    entry?.PullRequestTemplate ?? defaults?.PullRequestTemplate ?? false,
            };
        }

        private static string? Pick(string? entry, string? fallback)
        {
            return string.IsNullOrWhiteSpace(entry) ? fallback : entry;
        }

        /// <summary>
        /// Lists replace rather than concatenate. Concatenation would make it impossible to
        /// remove an inherited topic or approver from a single repository.
        /// </summary>
        private static IReadOnlyList<string> PickList(IList<string>? entry, IList<string>? fallback)
        {
            IList<string>? source = entry ?? fallback;
            return source is null ? Array.Empty<string>() : source.ToArray();
        }
    }
}
