#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Variables;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueTrackers;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.IssueTrackers;

public sealed class GitLabIssueTrackerService : IssueTrackerService<GitLabIssueTrackerProject, GitLabAccount>
{
    public override string ServiceName => "GitLab";
    public override string DefaultVersionFieldName => "Milestone";
    public override string? NamespaceDisplayName => "Group";

    protected override IAsyncEnumerable<string> GetNamespacesAsync(GitLabAccount credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var client = new GitLabClient(credentials);
        return client.GetGroupsAsync(cancellationToken);
    }

    protected override IAsyncEnumerable<string> GetProjectNamesAsync(GitLabAccount credentials, string? serviceNamespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceNamespace);
        var client = new GitLabClient(credentials);
        return client.GetProjectsAsync(serviceNamespace, cancellationToken);
    }

}

[DisplayName("GitLab Issue Tracker")]
[Description("Work with issues on a GitLab Repository")]
public sealed class GitLabIssueTrackerProject : IssueTrackerProject<GitLabAccount>
{
    [Persistent]
    [Category("Advanced Mapping")]
    [DisplayName("Labels")]
    [PlaceholderText("Any")]
    [Description("A list of comma separated label names. Example: bug,ui,@high, $ReleaseNumber")]
    public string? Labels { get; set; }

    [Persistent]
    [FieldEditMode(FieldEditMode.Multiline)]
    [Category("Advanced Mapping")]
    [DisplayName("Custom filter query")]
    [PlaceholderText("Use above fields")]
    [Description("If a custom filter query string is set, the Milestone and Labels are ignored; see "
        + "<a href=\"https://docs.gitlab.com/ce/api/issues.html#list-project-issues\" target=\"_blank\">GitLab API List Issues for a Project</a> "
        + "for more information.<br /><br />"
        + "For example, to filter by all issues with no labels that contain the word 'cheese' in their title or description:<br /><br />"
        + "<pre>labels=No+Label&amp;search=cheese&amp;milestone=$ReleaseNumber</pre>")]
    public string? CustomFilterQueryString { get; set; }

    private GitLabProjectId ProjectId => new (this.Namespace, this.ProjectName);
    private static readonly HashSet<string> validStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Open", "Closed" };

    public override async Task<IssuesQueryFilter> CreateQueryFilterAsync(IVariableEvaluationContext context)
    {
        if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
        {
            try
            {
                var query = (await ProcessedString.Parse(this.CustomFilterQueryString).EvaluateValueAsync(context).ConfigureAwait(false)).AsString();
                if (string.IsNullOrEmpty(query))
                    throw new InvalidOperationException("resulting query is an empty string");
                return new GitLabIssueFilter(query);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not parse the Issue mapping query \"{this.CustomFilterQueryString}\": {ex.Message}");
            }
        }

        try
        {
            var milestone = (await ProcessedString.Parse(AH.CoalesceString(this.SimpleVersionMappingExpression, "$ReleaseNumber")).EvaluateValueAsync(context).ConfigureAwait(false)).AsString();
            if (string.IsNullOrEmpty(milestone))
                throw new InvalidOperationException("milestone expression is an empty string");

            var labels = string.IsNullOrEmpty(this.Labels)
                ? null
                : (await ProcessedString.Parse(AH.CoalesceString(this.SimpleVersionMappingExpression, "$ReleaseNumber")).EvaluateValueAsync(context).ConfigureAwait(false)).AsString();

            return new GitLabIssueFilter(milestone, labels);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not parse the simple mapping expression \"{this.SimpleVersionMappingExpression}\": {ex.Message}");
        }
    }

    public override async Task EnsureVersionAsync(IssueTrackerVersion version, ICredentialResolutionContext context, CancellationToken cancellationToken = default)
    {
        var client = this.CreateClient(context);
        var milestone = await client.FindMilestoneAsync(version.Version, this.ProjectId, cancellationToken).ConfigureAwait(false);
        if (milestone == null)
        {
            await client.CreateMilestoneAsync(version.Version, this.ProjectId, cancellationToken).ConfigureAwait(false);
            if (version.IsClosed)
                await client.CloseMilestoneAsync(version.Version, this.ProjectId, cancellationToken).ConfigureAwait(false);
        }
        else if (version.IsClosed && milestone.State != "closed")
            await client.UpdateMilestoneAsync(milestone.Id, this.ProjectId, new { state_event = "close" }, cancellationToken).ConfigureAwait(false);
        
        else if (!version.IsClosed && milestone.State == "closed")
            await client.UpdateMilestoneAsync(milestone.Id, this.ProjectId, new { state_event = "open" }, cancellationToken).ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<IssueTrackerIssue> EnumerateIssuesAsync(IIssuesEnumerationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var issue in this.CreateClient(context).GetIssuesAsync(this.ProjectId, (GitLabIssueFilter)context.Filter, cancellationToken).ConfigureAwait(false))
            yield return new IssueTrackerIssue(issue.Id, issue.Status, issue.Type, issue.Title, issue.Description, issue.Submitter, issue.SubmittedDate, issue.IsClosed, issue.Url);
    }

    public override async IAsyncEnumerable<IssueTrackerVersion> EnumerateVersionsAsync(ICredentialResolutionContext context, [EnumeratorCancellation]CancellationToken cancellationToken = default)
    {
        await foreach (var milestone in this.CreateClient(context).GetMilestonesAsync(this.ProjectId, null, cancellationToken))
            yield return new IssueTrackerVersion(milestone.Title, milestone.State == "closed");
    }

    public override RichDescription GetDescription() => new($"{this.Namespace}/{this.ProjectName}");

    public override async Task TransitionIssuesAsync(string? fromStatus, string toStatus, string? comment, IIssuesEnumerationContext context, CancellationToken cancellationToken = default)
    {
        if (!validStates.Contains(toStatus))
            throw new ArgumentOutOfRangeException($"GitLab Issue status cannot be set to \"{toStatus}\", only Open or Closed.");
        if (!string.IsNullOrEmpty(fromStatus) && !validStates.Contains(fromStatus))
            throw new ArgumentOutOfRangeException($"GitLab Issue status cannot be to \"{toStatus}\", only Open or Closed.");

        var client = this.CreateClient(context);
        await foreach (var issue in client.GetIssuesAsync(this.ProjectId, (GitLabIssueFilter)context.Filter, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(toStatus, issue.Status, StringComparison.OrdinalIgnoreCase))
                continue;
            if (fromStatus != null && !string.Equals(fromStatus, issue.Status, StringComparison.OrdinalIgnoreCase))
                continue;

            await client.UpdateIssueAsync(int.Parse(issue.Id), this.ProjectId, new { state_event = "close" }, cancellationToken).ConfigureAwait(false);

        }
    }

    private GitLabClient CreateClient(ICredentialResolutionContext context)
    {
        var creds = this.GetCredentials(context) as GitLabAccount
            ?? throw new InvalidOperationException("Credentials are required to query GitLab API.");

        return new GitLabClient(creds, this);
    }

}
