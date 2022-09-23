//#define IS_ENABLED_LABEL

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using ContentTypeTextNet.Pe.Core.Models;
using Octokit;
using Octokit.Internal;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class Application
    {
        #region property

        //DateTime LastApiUseTime { get; set; } = DateTime.MinValue;
        TimeSpan DelayTime { get; set; }
        TimeSpan RateDelayTime { get; set; }

        #endregion

        #region function

        private Task SleepAsync()
        {
            //var current = DateTime.UtcNow;
            //if((current - LastApiUseTime) < DelayTime) {
            //    return Task.Delay(DelayTime).ContinueWith(_ => {
            //        LastApiUseTime = DateTime.UtcNow;
            //    });
            //};
            //LastApiUseTime = DateTime.UtcNow;

            //return Task.CompletedTask;

            return Task.Delay(DelayTime).ContinueWith(_ => {
                //LastApiUseTime = DateTime.UtcNow;
            });
        }

        private async Task GitHubApiAsync(GitHubClient gitHubClient, Func<GitHubClient, Task> funcAsync)
        {
            await SleepAsync();
            try {
                await funcAsync(gitHubClient);
            } catch(SecondaryRateLimitExceededException ex) {
                ConsoleUtility.LogWarning(ex.Message);
            }
            await Task.Delay(RateDelayTime);
            await funcAsync(gitHubClient);
        }

        private async Task<TResult> GitHubApiAsync<TResult>(GitHubClient gitHubClient, Func<GitHubClient, Task<TResult>> funcAsync)
        {
            await SleepAsync();
            try {
                return await funcAsync(gitHubClient);
            } catch(SecondaryRateLimitExceededException ex) {
                ConsoleUtility.LogWarning(ex.Message);
            }
            await Task.Delay(RateDelayTime);
            return await funcAsync(gitHubClient);
        }

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

        private (Setting setting, TimeSpan delayTime, TimeSpan rateDelayTime) LoadSetting()
        {
            var commandLine = new CommandLine();
            var commandKeys = new {
                Setting = commandLine.Add(shortKey: 's', longKey: "setting", hasValue: true),
                DelayTime = commandLine.Add(shortKey: 'd', longKey: "delay", hasValue: true),
                RateLimitTime = commandLine.Add(shortKey: 'r', longKey: "rate", hasValue: true),
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

            var rawDelayTime = GetSettingValue("delay", commandLine, commandKeys.DelayTime, "0.00:00:05.0", true);
            var delayTime = TimeSpan.Parse(rawDelayTime);

            var rawRateDelayTime = GetSettingValue("rate", commandLine, commandKeys.RateLimitTime, "0.00:01:00.0", true);
            var rateDelayTime = TimeSpan.Parse(rawRateDelayTime);

            return (setting: setting, delayTime: delayTime, rateDelayTime: rateDelayTime);
        }

        public BitbucketDbV1 LoadBitbucketDb(BitbucketSetting bitbucketSetting)
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

        private async Task ApplyLabelAsync(GitHubClient client, Repository repository, LabelSetting labelSetting)
        {
            var labels = await GitHubApiAsync(client, c => c.Issue.Labels.GetAllForRepository(repository.Id));
            if(labels.Any()) {
                ConsoleUtility.Subject("既存ラベル破棄");

                foreach(var label in labels) {
                    ConsoleUtility.LogInformation($"ラベル削除: {label.Id}, {label.Name}");
                    await GitHubApiAsync(client, c => c.Issue.Labels.Delete(repository.Id, label.Name));
                }
            } else {
                ConsoleUtility.LogTrace("削除ラベルなし");
            }

            ConsoleUtility.Subject("既存ラベル生成");
            foreach(var item in labelSetting.Items) {
                ConsoleUtility.LogInformation($"ラベル作成: {item}");
                var newLabel = new NewLabel(item, "cccccc"); // 色なんか後で変えてくれ
                var label = await GitHubApiAsync(client, c => c.Issue.Labels.Create(repository.Id, newLabel));
                ConsoleUtility.LogDebug($"ラベル結果: {label.Id}");
            }
        }

        private string BuildUrl(string baseUrl, string addPath)
        {
            return baseUrl.TrimEnd('/') + "/" + addPath.TrimStart('/');
        }

        private string BuildTitle(BitbucketIssue issue, string template, BitbucketSetting bitbucketSetting)
        {
            var map = new Dictionary<string, string>() {
                ["TITLE"] = issue.Title,
                ["NUMBER"] = issue.Id.ToString(),
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private string BuildIssueBody(BitbucketIssue issue, string template, BitbucketSetting bitbucketSetting)
        {
            var map = new Dictionary<string, string>() {
                ["TITLE"] = issue.Title,
                ["NUMBER"] = issue.Id.ToString(),
                ["MARKDOWN"] = issue.Content,
                ["URL"] = BuildUrl(bitbucketSetting.IssueBaseUrl, issue.Id.ToString()),
                ["CREATED_AT"] = issue.CreatedOn.ToString("u"),
                ["USER"] = issue.Reporter,
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private string BuildCommentBody(BitbucketIssue issue, BitbucketComment comment, string template, BitbucketSetting bitbucketSetting)
        {
            var map = new Dictionary<string, string>() {
                ["TITLE"] = issue.Title,
                ["NUMBER"] = issue.Id.ToString(),
                ["MARKDOWN"] = comment.Content,
                ["URL"] = BuildUrl(bitbucketSetting.IssueBaseUrl, issue.Id.ToString() + "#comment-" + comment.Id),
                ["CREATED_AT"] = comment.CreatedOn.ToString("u"),
                ["USER"] = comment.User,
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private async Task CreateIssueAsync(GitHubClient client, Repository repository, BitbucketIssue issue, BitbucketComment[] comments, Setting setting)
        {
            ConsoleUtility.Subject($"課題生成 -> [{issue.Id}] {issue.Title}");

            var githubIssue = new NewIssue(BuildTitle(issue, setting.Template.IssueTitle, setting.Bitbucket));
            githubIssue.Body = BuildIssueBody(issue, setting.Template.IssueBody, setting.Bitbucket);

            var issueResult = await GitHubApiAsync(client, c => c.Issue.Create(repository.Id, githubIssue));
            ConsoleUtility.LogDebug($"課題結果 -> [{issue.Id}:{issueResult.Number}] {issueResult.Title}");

            if(comments.Any()) {
                ConsoleUtility.LogInformation("課題コメント作成");
                foreach(var comment in comments) {
                    var commentContent = BuildCommentBody(issue, comment, setting.Template.Comment, setting.Bitbucket);
                    var commentResult = await GitHubApiAsync(client, c => c.Issue.Comment.Create(repository.Id, issueResult.Number, commentContent));
                    ConsoleUtility.LogDebug($"課題コメント結果: {commentResult.Id}");
                }
            } else {
                ConsoleUtility.LogTrace("課題コメント無し");
            }

            if(issue.Status == "close") {
                var githubUpdateIssue = new IssueUpdate() {
                    State = ItemState.Closed,
                };

                var issueCloseResult = await GitHubApiAsync(client, c => c.Issue.Update(repository.Id, issueResult.Number, githubUpdateIssue));
                ConsoleUtility.LogDebug($"課題クローズ: {issueCloseResult.Id}");
            }
        }

        private async Task ApplyIssuesAsync(GitHubClient client, Repository repository, BitbucketDbV1 bitbucketDb, Setting setting)
        {
            var bitbucketIssueList = bitbucketDb.Issues
                .OrderBy(i => i.Id)
                .ToList()
            ;
            var bitbucketIssueComments = bitbucketDb.Comments
                .GroupBy(i => i.Issue)
                .ToDictionary(
                    k => k.Key,
                    v => v.OrderBy(i => i.CreatedOn).ToArray()
                )
            ;

            //test 100
            foreach(var issue in bitbucketIssueList.Take(100)) {
                if(!bitbucketIssueComments.TryGetValue(issue.Id, out var comments)) {
                    comments = Array.Empty<BitbucketComment>();
                }
                await CreateIssueAsync(client, repository, issue, comments, setting);
            }
        }

        private async Task ExecuteCoreAsync(Setting setting, TimeSpan delayTime, TimeSpan rateDelayTime)
        {
            DelayTime = delayTime;
            RateDelayTime = rateDelayTime;

            var bitbucketDb = LoadBitbucketDb(setting.Bitbucket);

            var client = CreateGitHubClient(setting.GitHub);

            var repository = await GitHubApiAsync(client, c => c.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository));

            if(setting.Label.Items.Any()) {
                ConsoleUtility.Title("ラベル構築");
#if IS_ENABLED_LABEL
                await ApplyLabelAsync(client, repository, setting.Label);
#else
                ConsoleUtility.LogWarning("ラベル構築 未実施");
#endif
            }

            ConsoleUtility.Title("課題構築");
            await ApplyIssuesAsync(client, repository, bitbucketDb, setting);

        }

        public Task ExecuteAsync()
        {
            var setting = LoadSetting();
            return ExecuteCoreAsync(setting.setting, setting.delayTime, setting.rateDelayTime);
        }

        #endregion
    }
}
