using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders;

internal abstract class GitLabSuggestionProvider : ISuggestionProvider
{
    public GitLabAccount Credentials { get; private set; }
    public GitLabRepository Resource { get; private set; }
    public IComponentConfiguration ComponentConfiguration { get; private set; }
    internal GitLabClient Client { get; private set; }

    private protected abstract IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken);

    public IAsyncEnumerable<string> GetSuggestionsAsync(IComponentConfiguration config, CancellationToken cancellationToken)
    {
        var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);

        // resource editors
        var credentialName = config[nameof(GitLabRepository.CredentialName)];
        if (!string.IsNullOrEmpty(credentialName))
            this.Credentials = SecureCredentials.TryCreate(credentialName, context) as GitLabAccount;

        var resourceName = config[nameof(IGitLabConfiguration.ResourceName)];
        if (!string.IsNullOrEmpty(resourceName))
            this.Resource = SecureResource.TryCreate(SecureResourceType.GitRepository, resourceName, context) as GitLabRepository;

        if (this.Credentials == null && this.Resource != null)
            this.Credentials = this.Resource.GetCredentials(context) as GitLabAccount;

        var groupName = AH.CoalesceString(config[nameof(IGitLabConfiguration.GroupName)], this.Resource?.GroupName);

        if (groupName == null && this.Credentials == null)
            return AsyncEnumerable.Empty<string>();

        this.ComponentConfiguration = config;

        this.Client = new GitLabClient(
            AH.CoalesceString(config[nameof(IGitLabConfiguration.ApiUrl)], this.Credentials?.ServiceUrl ?? this.Resource?.LegacyApiUrl), 
            AH.CoalesceString(config[nameof(IGitLabConfiguration.UserName)], this.Credentials?.UserName),
            string.IsNullOrEmpty(config[nameof(IGitLabConfiguration.Password)]) 
                ? this.Credentials?.PersonalAccessToken 
                : AH.CreateSecureString(config[nameof(IGitLabConfiguration.Password)]));

        return this.GetSuggestionsAsync(cancellationToken);
    }
}
