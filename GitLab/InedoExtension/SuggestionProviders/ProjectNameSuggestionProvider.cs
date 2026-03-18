namespace Inedo.Extensions.GitLab.SuggestionProviders;

internal sealed class ProjectNameSuggestionProvider : GitLabSuggestionProvider
{
    private protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken) => this.Client.GetProjectsAsync(this.ComponentConfiguration[nameof(IGitLabConfiguration.GroupName)], cancellationToken);
}
