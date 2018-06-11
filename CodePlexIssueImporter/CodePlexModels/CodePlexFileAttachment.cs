using System;
using System.Collections.Generic;
using System.Text;

namespace CodePlexIssueImporter.CodePlexModels
{
    public class CodePlexFileAttachment
    {
        public long FileId { get; set; }
        public string FileName { get; set; }
        public string DownloadUrl { get; set; }
    }
}
