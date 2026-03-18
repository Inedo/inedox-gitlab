namespace Inedo.Extensions.GitLab.SuggestionProviders;

internal sealed class GroupNameSuggestionProvider : GitLabSuggestionProvider
{
    private protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken)
    {
        if (this.Credentials == null)
            return AsyncEnumerable.Empty<string>();

        return this.Client.GetGroupsAsync(cancellationToken);
    }
}
