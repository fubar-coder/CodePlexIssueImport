using System;
using System.Collections.Generic;
using System.Text;

namespace CodePlexIssueImporter.CodePlexModels
{
    public class CodePlexWorkItem
    {
        public long Id { get; set; }
        public CodePlexWorkItemType Type { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public CodePlexStatus Status { get; set; }
        public CodePlexComponent AffectedComponent { get; set; }
        public string ClosedComment { get; set; }
        public DateTimeOffset? ClosedDate { get; set; }
        public int CommentCount { get; set; }
        public string Custom { get; set; }
        public DateTimeOffset LastUpdatedDate { get; set; }
        public string PlannedForRelease { get; set; }
        public bool ReleaseVisibleToPublic { get; set; }
        public CodePlexPriority Priority { get; set; }
        public string ProjectName { get; set; }
        public DateTimeOffset ReportedDate { get; set; }
        public CodePlexCloseReason ReasonClosed { get; set; }
        public int VoteCount { get; set; }
    }
}
