using System;
using System.Collections.Generic;
using System.Text;

namespace CodePlexIssueImporter.CodePlexModels
{
    public class CodePlexComment
    {
        public int Id { get; set; }
        public DateTimeOffset PostedDate { get; set; }
        public string Message { get; set; }
    }
}
