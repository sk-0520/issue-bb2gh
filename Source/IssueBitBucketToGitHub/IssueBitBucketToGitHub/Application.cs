using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ContentTypeTextNet.Pe.Core.Models;
using Octokit;
using Octokit.Internal;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class Application
    {
        #region function

        private Stream OpenFileStream(string path)
        {
            return new FileStream(path, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private string GetSettingValue(string subject, CommandLine commandLine, CommandLineKey commandLineKey, string defaultValue, bool emptyIsDefaultValue)
        {
            if(commandLine.Values.TryGetValue(commandLineKey, out var value)) {
                if(!string.IsNullOrEmpty(value.First)) {
                    return value.First;
                }
            }
            if(emptyIsDefaultValue) {
                return defaultValue;
            }

            return ConsoleUtility.ReadRetryDefault(subject, defaultValue);
        }

        private Setting LoadSetting()
        {
            var commandLine = new CommandLine();
            var commandKeys = new {
                Setting = commandLine.Add(shortKey: 's', longKey: "setting", hasValue: true),
            };

            if(!commandLine.Parse()) {
                throw new InvalidOperationException();
            }

            var defaultPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()!.Location)!, "setting.json");
            var settingPath = GetSettingValue("setting", commandLine, commandKeys.Setting, defaultPath, false)!;

            using var stream = OpenFileStream(settingPath);
            var setting = JsonSerializer.Deserialize<Setting>(stream);
            if(setting is null) {
                throw new InvalidOperationException(settingPath);
            }

            return setting;
        }

        public BitbucketDbV1 LoadBitbucketIssues(BitbucketSetting bitbucketSetting)
        {
            var issueFilePath = Path.Combine(bitbucketSetting.IssueDirectoryPath, "db-1.0.json");


            using var stream = OpenFileStream(issueFilePath);
            var result = JsonSerializer.Deserialize<BitbucketDbV1>(stream);
            if(result is null) {
                throw new InvalidOperationException(issueFilePath);
            }

            return result;
        }

        private GitHubClient CreateGitHubClient(GitHubSetting gitHubSetting)
        {
            var productHeaderValue = new ProductHeaderValue(Assembly.GetExecutingAssembly().GetName().Name);
            var credentials = new Credentials(gitHubSetting.ApiToken);
            var client = new GitHubClient(productHeaderValue, new InMemoryCredentialStore(credentials), new Uri(gitHubSetting.BaseUrl));

            return client;
        }

        private async Task ExecuteCoreAsync(Setting setting)
        {
            // Bitbucket Issue を読み込む
            LoadBitbucketIssues(setting.Bitbucket);

            var client = CreateGitHubClient(setting.GitHub);

            var repository = await client.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository);

            var issue = new NewIssue("TITLE");
            //issue.Assignees.Add("sk-0520");
            //issue.Assignees.Add("bot");
            issue.Body = "#abc\r\n* A\r\n* B\r\n* C";

            var x = await client.Issue.Create(repository.Id, issue);

            var y = await client.Issue.Comment.Create(repository.Id, x.Number, "**COMMENT**");

            var issue2 = new IssueUpdate() {
                State = ItemState.Closed,
            };
            var z = await client.Issue.Update(repository.Id, x.Number, issue2);

            await Task.CompletedTask;
        }

        public Task ExecuteAsync()
        {
            var setting = LoadSetting();
            return ExecuteCoreAsync(setting);
        }

        #endregion
    }
}
