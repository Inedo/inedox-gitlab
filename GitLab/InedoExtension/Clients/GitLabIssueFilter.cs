#nullable enable
using System;
using System.Text;
using Inedo.Extensibility.IssueTrackers;

namespace Inedo.Extensions.GitLab.Clients;

internal sealed class GitLabIssueFilter : IssuesQueryFilter
{
    public GitLabIssueFilter(string customFilterQueryString)
    {
        this.CustomFilterQueryString = customFilterQueryString;
    }
    public GitLabIssueFilter(string milestone, string? labels = null)
    {
        this.Milestone = milestone;
        this.Labels = labels; ;
    }
    public string? Milestone { get; }
    public string? Labels { get; }
    public string? CustomFilterQueryString { get; }

    public string ToQueryString()
    {
        if (!string.IsNullOrEmpty(this.CustomFilterQueryString))
            return this.CustomFilterQueryString;

        var buffer = new StringBuilder("?per_page=100", 128);
        if (!string.IsNullOrEmpty(this.Milestone))
            buffer.Append("&milestone=").Append(Uri.EscapeDataString(this.Milestone));
        if (!string.IsNullOrEmpty(this.Labels))
            buffer.Append("&labels=").Append(Uri.EscapeDataString(this.Labels));

        return buffer.ToString();
    }
}
