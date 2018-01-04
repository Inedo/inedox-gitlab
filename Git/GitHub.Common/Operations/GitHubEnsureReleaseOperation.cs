﻿using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Configurations;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
using IOperationCollectionContext = Inedo.Otter.Extensibility.Operations.IOperationExecutionContext;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Ensure GitHub Release")]
    [Description("Creates or updates a tagged release in a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-GitHub-Release")]
    [ScriptNamespace("GitHub", PreferUnqualified = true)]
    public sealed class GitHubEnsureReleaseOperation : EnsureOperation<GitHubReleaseConfiguration>
    {
#if !BuildMaster
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var github = new GitHubClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.OrganizationName);

            var ownerName = AH.CoalesceString(this.Template.OrganizationName, this.Template.UserName);

            var release = await github.GetReleaseAsync(ownerName, this.Template.RepositoryName, this.Template.Tag, context.CancellationToken);

            if (release == null)
            {
                return new GitHubReleaseConfiguration { Exists = false };
            }

            return new GitHubReleaseConfiguration
            {
                Tag = (string)release["tag_name"],
                Target = (string)release["target_commitish"],
                Title = (string)release["name"],
                Description = (string)release["body"],
                Draft = (bool)release["draft"],
                Prerelease = (bool)release["prerelease"]
            };
        }
#endif

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var github = new GitHubClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.OrganizationName);

            var ownerName = AH.CoalesceString(this.Template.OrganizationName, this.Template.UserName);

            await github.EnsureReleaseAsync(ownerName, this.Template.RepositoryName, this.Template.Tag, this.Template.Target, this.Template.Title, this.Template.Description, this.Template.Draft, this.Template.Prerelease, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.Template.RepositoryName)], config[nameof(this.Template.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Ensure GitHub Release"),
               new RichDescription("in ", new Hilite(source), " for tag ", new Hilite(config[nameof(this.Template.Tag)]))
            );
        }
    }
}
