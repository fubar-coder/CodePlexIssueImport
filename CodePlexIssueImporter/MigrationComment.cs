using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mustache;

namespace CodePlexIssueImporter
{
    public class MigrationComment
    {
        private readonly string _template;
        private readonly Mustache.Template _formatter;
        private readonly Regex _matcher;

        public MigrationComment(string template = "*Form {{{user}}} on {{{timestamp}}}*\n\n")
        {
            _template = template;
            _formatter = Mustache.Template.Compile(template);
            var parser = new Mustache.Parser(_template);
            _matcher = CreateMatcher(parser);
        }

        public string AddMigrationComment(MigrationInfo migrationInfo, string message)
        {
            var formatted = _formatter.Render(new
            {
                user = migrationInfo.UserName,
                timestamp = migrationInfo.Timestamp.ToString("F", CultureInfo.InvariantCulture),
            });

            var body = new StringBuilder();
            body.Append(formatted)
                .Append(message);
            return body.ToString();
        }

        public MigrationInfo ParseMigrationComment(string text)
        {
            var match = _matcher.Match(text);
            if (!match.Success)
                return null;
            var user = match.Groups["user"].Value;
            var timestampAsString = match.Groups["timestamp"].Value;
            var timestamp = DateTimeOffset.ParseExact(timestampAsString, "F", CultureInfo.InvariantCulture);
            return new MigrationInfo
            {
                UserName = user,
                Timestamp = timestamp,
            };
        }

        private static Regex CreateMatcher(Parser parser)
        {
            var pattern = new StringBuilder()
                .Append("^");
            foreach (var item in parser.Parse())
            {
                switch (item)
                {
                    case Mustache.Elements.TextElement te:
                        pattern.Append(Regex.Escape(te.Text));
                        break;
                    case Mustache.Elements.Variable v:
                        pattern.AppendFormat("(?<{0}>.*?)", v.Key);
                        break;
                }
            }

            return new Regex(pattern.ToString(), RegexOptions.Compiled);
        }
    }
}
