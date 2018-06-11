using System;
using System.Collections.Generic;
using System.Text;
using CodePlexIssueImporter.CodePlexModels;

namespace CodePlexIssueImporter
{
    class CodePlexPriorityComparer : IEqualityComparer<CodePlexPriority>
    {
        public bool Equals(CodePlexPriority x, CodePlexPriority y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(CodePlexPriority obj)
        {
            return obj.Id.GetHashCode();
        }
    }
}
