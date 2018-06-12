using System;
using System.Collections.Generic;
using System.Text;

namespace CodePlexIssueImporter
{
    public class MigrationInfo
    {
        public string UserName { get; set; } = "unknown CodePlex user";
        public DateTimeOffset Timestamp { get; set; }
    }
}
