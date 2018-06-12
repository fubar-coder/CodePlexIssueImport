using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CodePlexIssueImporter.CodePlexModels;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Octokit;

namespace CodePlexIssueImporter
{
    class Program
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        private static readonly MigrationComment _migrationComment = new MigrationComment();

        private static GitHubRequestThrottler _throttler;

        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .AddUserSecrets<Program>()
                .Build();

            var secrets = config.GetSection("SECRETS");
            var userName = secrets?["GITHUB_USERNAME"];
            var password = secrets?["GITHUB_PASSWORD"];

            var loadedIssues = LoadIssues(@"c:\Users\MarkJunker\Downloads\quickgraph\issues\").ToList();

            var credentialStore = new Octokit.Internal.InMemoryCredentialStore(new Credentials(userName, password));
            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("codeplex-issue-import", "0.0.1"), credentialStore);

            _throttler = new GitHubRequestThrottler(client);

            var repository = await client.Repository.Get("FubarDevelopment", "QuickGraph");

            var labels = loadedIssues.Select(x => x.issue.WorkItem.AffectedComponent)
                .Where(x => !string.IsNullOrEmpty(x?.Name))
                .Distinct(new CodePlexComponentComparer());

            await ImportComponentLabelsAsync(client, repository, labels);

            var priorities = loadedIssues.Select(x => x.issue.WorkItem.Priority)
                .Where(x => !string.IsNullOrEmpty(x?.Name))
                .Distinct(new CodePlexPriorityComparer());

            await ImportPriorityLabelsAsync(client, repository, priorities);

            var closeReasons = loadedIssues.Select(x => x.issue.WorkItem.ReasonClosed)
                .Where(x => !string.IsNullOrEmpty(x?.Name))
                .Select(x => x.Name)
                .Distinct();

            await ImportIssuesAsync(client, repository, loadedIssues);
        }

        private static async Task ImportLabelsAsync<T>(GitHubClient client, Repository repository, IEnumerable<T> labels, Func<T, NewLabel> CreateLabelFunc)
        {
            var repoLabels = (await client.Issue.Labels.GetAllForRepository(repository.Id))
                .ToDictionary(x => x.Name);
            foreach (var label in labels)
            {
                var gitHubLabel = CreateLabelFunc(label);
                if (repoLabels.ContainsKey(gitHubLabel.Name))
                    continue;

                await _throttler.Throttle();
                await client.Issue.Labels.Create(repository.Id, gitHubLabel);
                _throttler.UpdateLastRequest();
            }
        }

        private static Task ImportPriorityLabelsAsync(GitHubClient client, Repository repository, IEnumerable<CodePlexPriority> labels)
        {
            var nameToColorMap = new Dictionary<string, string>
            {
                ["High"] = "ff3c00",
                ["Medium"] = "f97923",
                ["Low"] = "ff9900",
                ["Unassigned"] = "0000c0",
            };

            return ImportLabelsAsync(client, repository, labels, l => new NewLabel($"priority:{l.Name}", nameToColorMap[l.Name]));
        }

        private static Task ImportComponentLabelsAsync(GitHubClient client, Repository repository, IEnumerable<CodePlexComponent> labels)
        {
            return ImportLabelsAsync(client, repository, labels, l => new NewLabel($"component:{l.Name}", "f6d612"));
        }

        private static async Task ImportIssuesAsync(GitHubClient client, Repository repository, List<(Uri url, CodePlexIssue issue)> loadedIssues)
        {
            var oldIssues = (await client.Issue.GetAllForRepository(
                    repository.Id,
                    new RepositoryIssueRequest() { State = ItemStateFilter.All },
                    new ApiOptions() { PageSize = 1000 }))
                .ToLookup(x => x.Title);

            foreach (var (url, issue) in loadedIssues)
            {
                Console.WriteLine(issue.WorkItem.Id);
                var gitHubIssue = await GetOrCreateIssueAsync(client, repository, issue, oldIssues);
                CopyAttachments(url, issue);
                await AddIssueComments(client, repository, gitHubIssue, issue);
            }
        }

        private static void CopyAttachments(Uri codePlexIssueUrl, CodePlexIssue issue)
        {
            if ((issue.FileAttachments?.Count ?? 0) == 0)
                return;

            var outputPath = Path.Combine(new Uri(codePlexIssueUrl, "attachments").LocalPath);
            Directory.CreateDirectory(outputPath);
            foreach (var attachment in issue.FileAttachments)
            {
                var attachmentUrl = new Uri(codePlexIssueUrl, attachment.DownloadUrl);
                var path = Path.Combine(outputPath, attachment.FileName);
                if (!File.Exists(path))
                    File.Copy(attachmentUrl.LocalPath, path);
            }
        }

        private static async Task AddIssueComments(GitHubClient client, Repository repository, Issue gitHubIssue, CodePlexIssue codePlexIssue)
        {
            if (codePlexIssue.Comments == null || codePlexIssue.Comments.Count == 0)
                return;

            await _throttler.Throttle();
            var gitHubIssueComments = await client.Issue.Comment.GetAllForIssue(repository.Id, gitHubIssue.Number);
            var timestampToComments =
                (from comment in gitHubIssueComments
                 let info = _migrationComment.ParseMigrationComment(comment.Body)
                 where info != null
                 select new { info.Timestamp, comment })
                 .ToDictionary(x => x.Timestamp, x => x.comment);

            var wasClosed = gitHubIssue.ClosedAt != null;
            foreach (var comment in codePlexIssue.Comments)
            {
                var textDate = string.Format(CultureInfo.InvariantCulture, "{0:F}", comment.PostedDate);
                var parsedDate = DateTimeOffset.ParseExact(textDate, "F", CultureInfo.InvariantCulture);
                Console.WriteLine("\t{0}", textDate);
                if (timestampToComments.ContainsKey(parsedDate))
                    continue;

                var isEmpty = string.IsNullOrWhiteSpace(comment.Message);
                var isClosingIssue = codePlexIssue.WorkItem.ClosedDate != null && codePlexIssue.WorkItem.ClosedDate.Value == comment.PostedDate;

                if (!isEmpty || (isClosingIssue && !wasClosed))
                {
                    await _throttler.Throttle();
                    var migrationInfo = new MigrationInfo { Timestamp = comment.PostedDate, };
                    var message = _migrationComment.AddMigrationComment(migrationInfo, comment.Message);
                    var gitHubIssueComment = await client.Issue.Comment.Create(repository.Id, gitHubIssue.Number, message);
                    _throttler.UpdateLastRequest();
                }

                if (!wasClosed && isClosingIssue)
                {
                    wasClosed = true;

                    var issueUpdate = new IssueUpdate()
                    {
                        State = ItemState.Closed,
                    };

                    await _throttler.Throttle();
                    await client.Issue.Update(repository.Id, gitHubIssue.Number, issueUpdate);
                    _throttler.UpdateLastRequest();
                }
            }

            if (!wasClosed && codePlexIssue.WorkItem.ClosedDate != null)
            {
                var issueUpdate = new IssueUpdate()
                {
                    State = ItemState.Closed,
                };

                await _throttler.Throttle();
                await client.Issue.Update(repository.Id, gitHubIssue.Number, issueUpdate);
                _throttler.UpdateLastRequest();
            }
        }

        private static async Task<Issue> GetOrCreateIssueAsync(GitHubClient client, Repository repository, CodePlexIssue issue, ILookup<string, Issue> oldIssues)
        {
            var title = $"CP-{issue.WorkItem.Id}: {issue.WorkItem.Summary}";
            if (oldIssues.Contains(title))
                return oldIssues[title].First();

            var migrationInfo = new MigrationInfo { Timestamp = issue.WorkItem.ReportedDate };
            var body = _migrationComment.AddMigrationComment(migrationInfo, issue.WorkItem.Description);
            var newIssue = new NewIssue(title)
            {
                Body = body,
            };

            var component = issue.WorkItem.AffectedComponent?.Name;
            if (!string.IsNullOrEmpty(component))
            {
                newIssue.Labels.Add($"component:{component}");
            }

            var prio = issue.WorkItem.Priority?.Name;
            if (!string.IsNullOrEmpty(prio))
            {
                newIssue.Labels.Add($"priority:{prio}");
            }

            await _throttler.Throttle();
            var result = await client.Issue.Create(repository.Id, newIssue);
            _throttler.UpdateLastRequest();

            return result;
        }

        private static IEnumerable<(Uri url, CodePlexIssue issue)> LoadIssues(string path)
        {
            var issuesFileName = Path.Combine(path, "issues.json");
            var data = File.ReadAllText(issuesFileName);
            var issueRefs = JsonConvert.DeserializeObject<ICollection<CodePlexIssueReference>>(data, _serializerSettings);
            foreach (var issueRef in issueRefs)
            {
                var issueId = issueRef.Id.ToString(CultureInfo.InvariantCulture);
                //Console.WriteLine(issueId);
                var issuePath = Path.Combine(path, issueId, $"{issueId}.json");
                var issueJson = File.ReadAllText(issuePath);
                var issue = JsonConvert.DeserializeObject<CodePlexIssue>(issueJson, _serializerSettings);
                var url = new Uri(issuePath);
                yield return (url, issue);
            }
        }
    }
}
