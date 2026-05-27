namespace Inedo.Extensions.GitLab.SuggestionProviders;

internal sealed class MilestoneSuggestionProvider : GitLabSuggestionProvider
{
    private protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken)
    {
        var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitLabConfiguration.ProjectName)], this.Resource?.ProjectName);
        if (string.IsNullOrEmpty(projectName))
            return AsyncEnumerable.Empty<string>();

        var groupName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitLabConfiguration.GroupName)], this.Resource?.GroupName);

        return this.Client.GetMilestonesAsync(new GitLabProjectId(groupName, projectName), "open", cancellationToken)
            .OrderByDescending(m => m.Id)
            .Select(m => m.Title);
    }
}
