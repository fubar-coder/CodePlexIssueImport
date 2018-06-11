using System;
using System.Collections.Generic;
using System.Text;
using CodePlexIssueImporter.CodePlexModels;

namespace CodePlexIssueImporter
{
    class CodePlexComponentComparer : IEqualityComparer<CodePlexComponent>
    {
        public bool Equals(CodePlexComponent x, CodePlexComponent y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(CodePlexComponent obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
