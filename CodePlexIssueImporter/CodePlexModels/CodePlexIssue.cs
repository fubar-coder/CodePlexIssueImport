using System;
using System.Collections.Generic;
using System.Text;

namespace CodePlexIssueImporter.CodePlexModels
{
    public class CodePlexIssue
    {
        public CodePlexWorkItem WorkItem { get; set; }
        public ICollection<CodePlexFileAttachment> FileAttachments { get; set; }
        public ICollection<CodePlexComment> Comments { get; set; }
    }
}
