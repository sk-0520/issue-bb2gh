//#define IS_ENABLED_LABEL

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using ContentTypeTextNet.Pe.Core.Models;
using Octokit;
using Octokit.Internal;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class Application
    {
        #region define

        private const string RawDelayTime = "0.00:00:10.0";
        private const int DisplayApiInfoCount = 10;

        #endregion

        #region property

        private static TimeSpan AddDelayTime { get; } = TimeSpan.FromMinutes(1);

        //DateTime LastApiUseTime { get; set; } = DateTime.MinValue;
        private TimeSpan DelayTime { get; set; }
        private bool NeedsSleep { get; set; } = false;
        private int ApiCallCount { get; set; } = 0;
        private int ApiTotalCallCount { get; set; } = 0;

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

            // 最初は待たない
            if(!NeedsSleep) {
                NeedsSleep = true;
                return Task.CompletedTask;
            }

            return Task.Delay(DelayTime);
        }

        private void ShowApiInfo(ApiInfo apiInfo)
        {
            using var color = ConsoleUtility.ChangeColor(ConsoleColor.Magenta, ConsoleColor.DarkBlue);

            ConsoleUtility.Subject("<<API INFO>>");
            Console.WriteLine($"\tRemaining: {apiInfo.RateLimit.Remaining}");
            Console.WriteLine($"\tLimit: {apiInfo.RateLimit.Limit}");
            Console.WriteLine($"\tReset: {apiInfo.RateLimit.Reset.ToLocalTime()}");
        }

        private Task DelayRateAsync(GitHubClient gitHubClient)
        {
            var apiInfo = gitHubClient.GetLastApiInfo();
            ShowApiInfo(apiInfo);

            var delayTime = apiInfo.RateLimit.Reset - DateTime.UtcNow + AddDelayTime;

            ConsoleUtility.LogWarning($"limit delay: {delayTime}");
            return Task.Delay(delayTime);
        }

        private async Task<TResult> GitHubApiAsync<TResult>(GitHubClient gitHubClient, Func<GitHubClient, Task<TResult>> funcAsync)
        {
            await SleepAsync();
            try {
                ConsoleUtility.LogTrace($"API: {++ApiTotalCallCount}");

                var result = await funcAsync(gitHubClient);

                return result;
            } catch(SecondaryRateLimitExceededException ex) {
                ConsoleUtility.LogWarning(ex.ToString());
                await DelayRateAsync(gitHubClient);
            }
            return await funcAsync(gitHubClient);
        }

        private Task GitHubApiAsync(GitHubClient gitHubClient, Func<GitHubClient, Task> funcAsync)
        {
            return GitHubApiAsync(gitHubClient, c => {
                return funcAsync(c).ContinueWith(_ => -1);
            });
        }

        private Stream OpenFileStream(string path, bool writable = false)
        {
            var mode = writable ? System.IO.FileMode.Create : System.IO.FileMode.Open;
            var access = writable ? FileAccess.Write : FileAccess.Read;
            return new FileStream(path, mode, access, FileShare.Read);
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

        private (Setting setting, TimeSpan delayTime, string accessToken) LoadSetting()
        {
            var commandLine = new CommandLine();
            var commandKeys = new {
                Setting = commandLine.Add(shortKey: 's', longKey: "setting", hasValue: true),
                DelayTime = commandLine.Add(shortKey: 'd', longKey: "delay", hasValue: true),
                //RateLimitTime = commandLine.Add(shortKey: 'r', longKey: "rate", hasValue: true),
                AccessToken = commandLine.Add(shortKey: 'a', longKey: "access-token", hasValue: true),
            };

            if(!commandLine.Parse()) {
                throw new InvalidOperationException();
            }

            var defaultPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()!.Location)!, "setting.json");
            var settingPath = GetSettingValue("setting", commandLine, commandKeys.Setting, defaultPath, false)!;

            using var stream = OpenFileStream(settingPath);
            var setting = JsonSerializer.Deserialize<Setting>(stream, new JsonSerializerOptions() {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            });
            if(setting is null) {
                throw new InvalidOperationException(settingPath);
            }

            var rawDelayTime = GetSettingValue("delay", commandLine, commandKeys.DelayTime, RawDelayTime, true);
            var delayTime = TimeSpan.Parse(rawDelayTime);

            var accessToken = GetSettingValue("access token", commandLine, commandKeys.AccessToken, string.Empty, true);

            return (setting: setting, delayTime: delayTime, accessToken: accessToken);
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

        private async Task<GitHubClient> CreateGitHubClientAsync(GitHubSetting gitHubSetting, string accessToken)
        {
            var productHeaderValue = new ProductHeaderValue(Assembly.GetExecutingAssembly().GetName().Name);
            var client = new GitHubClient(productHeaderValue, new Uri(gitHubSetting.BaseUrl));
            client.SetRequestTimeout(TimeZoneInfo.Local.BaseUtcOffset);

            if(string.IsNullOrWhiteSpace(accessToken)) {
                ConsoleUtility.Title("OAuth AccessToken");

                var http = new HttpListener();
                http.Prefixes.Add(gitHubSetting.ClientRedirectUrl);
                ConsoleUtility.LogInformation("HttpListener running...");
                http.Start();

                var loginUrl = client.Oauth.GetGitHubLoginUrl(new OauthLoginRequest(gitHubSetting.ClientId) {
                    RedirectUri = new Uri(gitHubSetting.ClientRedirectUrl),
                    State = Guid.NewGuid().ToString("N"),
                    Scopes = {
                        "repo",
                        "user",
                    }
                });

                Process.Start(new ProcessStartInfo {
                    UseShellExecute = true,
                    FileName = loginUrl.OriginalString,
                });

                var context = await http.GetContextAsync();
                var token = context.Request.QueryString.Get("code");

                var oauthToken = await client.Oauth.CreateAccessToken(new OauthTokenRequest(
                    gitHubSetting.ClientId,
                    gitHubSetting.ClientSecret,
                    token
                ));

                accessToken = oauthToken.AccessToken;

                ConsoleUtility.LogInformation("Access Token( --access-token=\"...\" )");
                Console.WriteLine(">> {0}", accessToken);
                Console.WriteLine("please enter");
                Console.ReadLine();
            }

            client.Credentials = new Credentials(accessToken);

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

        private string AdjustContent(string content, out bool isOmit)
        {
            const int maxLength = 65536 - 500; // 500にそこまで意味はない。テンプレ次第だけどまぁ500残しておけば大丈夫かと。。。

            content = Regex.Replace(content, @"→\s*<<cset\s+(?<HASH>[A-Fa-f0-9]+)>>", "→ 《cset $1》", RegexOptions.Multiline);

            if(content.Length < maxLength) {
                isOmit = false;
                return content;
            }

            isOmit = true;
            var s = content.Substring(0, maxLength);

            return "#マイグレーションにより切り落とし発生!\r\n----\r\n" + s;
        }

        private string BuildIssueBody(BitbucketIssue issue, string template, BitbucketSetting bitbucketSetting, out bool isOmit)
        {
            var content = AdjustContent(issue.Content, out isOmit);

            var map = new Dictionary<string, string>() {
                ["TITLE"] = issue.Title,
                ["NUMBER"] = issue.Id.ToString(),
                ["MARKDOWN"] = content,
                ["QUOTE_MARKDOWN"] = string.Join(Environment.NewLine, TextUtility.ReadLines(content).Select(i => "> " + i)),
                ["URL"] = BuildUrl(bitbucketSetting.IssueBaseUrl, issue.Id.ToString()),
                ["CREATED_AT"] = issue.CreatedOn.ToString("u"),
                ["USER"] = issue.Reporter,
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private string BuildCommentBody(BitbucketIssue issue, BitbucketComment comment, string template, BitbucketSetting bitbucketSetting, out bool isOmit)
        {
            var content = AdjustContent(comment.Content, out isOmit);

            var map = new Dictionary<string, string>() {
                ["TITLE"] = issue.Title,
                ["NUMBER"] = issue.Id.ToString(),
                ["MARKDOWN"] = content,
                ["QUOTE_MARKDOWN"] = string.Join(Environment.NewLine, TextUtility.ReadLines(content).Select(i => "> " + i)),
                ["URL"] = BuildUrl(bitbucketSetting.IssueBaseUrl, issue.Id.ToString() + "#comment-" + comment.Id),
                ["CREATED_AT"] = comment.CreatedOn.ToString("u"),
                ["USER"] = comment.User,
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private async Task CreateIssueAsync(GitHubClient client, Repository repository, BitbucketIssue bitbucketIssue, BitbucketComment[] comments, Setting setting)
        {
            ConsoleUtility.Subject($"課題生成 -> [{bitbucketIssue.Id}] {bitbucketIssue.Title}");

            var githubIssue = new NewIssue(BuildTitle(bitbucketIssue, setting.Template.IssueTitle, setting.Bitbucket));

            githubIssue.Body = BuildIssueBody(bitbucketIssue, setting.Template.IssueBody, setting.Bitbucket, out var issueBodyFailure);

            bool isOmit = false;
            if(issueBodyFailure) {
                ConsoleUtility.LogWarning("本文切り落とし発生");
                if(!string.IsNullOrWhiteSpace(setting.Label.Omit)) {
                    githubIssue.Labels.Add(setting.Label.Omit);
                    isOmit = true;
                }
            }

            // ラベル強制設定
            if(!string.IsNullOrWhiteSpace(setting.Label.Force)) {
                githubIssue.Labels.Add(setting.Label.Force);
            }
            // ステータス置き換え
            if(bitbucketIssue.Status is not null && setting.Label.Mapping.Status.TryGetValue(bitbucketIssue.Status, out var status)) {
                githubIssue.Labels.Add(status);
            }
            // タイプ置き換え
            if(bitbucketIssue.Kind is not null && setting.Label.Mapping.Kinds.TryGetValue(bitbucketIssue.Kind, out var kind)) {
                githubIssue.Labels.Add(kind);
            }
            // コンポーネント置き換え
            if(bitbucketIssue.Component is not null && setting.Label.Mapping.Components.TryGetValue(bitbucketIssue.Component, out var component)) {
                githubIssue.Labels.Add(component);
            }

            if(!string.IsNullOrWhiteSpace(bitbucketIssue.Assignee)) {
                // 担当は * を考慮しない
                if(setting.User.Mapping.TryGetValue(bitbucketIssue.Assignee, out var githubUser)) {
                    githubIssue.Assignees.Add(githubUser);
                }
            }

            var issueResult = await GitHubApiAsync(client, c => c.Issue.Create(repository.Id, githubIssue));
            ConsoleUtility.LogDebug($"課題結果 -> [{bitbucketIssue.Id}:{issueResult.Number}] {issueResult.Title}");

            if(comments.Any()) {
                ConsoleUtility.LogInformation($"課題コメント作成 [{bitbucketIssue.Id}:{issueResult.Number}] {comments.Length}");
                var num = 1;
                foreach(var comment in comments) {
                    var commentContent = BuildCommentBody(bitbucketIssue, comment, setting.Template.Comment, setting.Bitbucket, out var issueCommentFailure);

                    if(issueCommentFailure) {
                        ConsoleUtility.LogWarning("コメント切り落とし発生");
                        if(!isOmit && !string.IsNullOrWhiteSpace(setting.Label.Omit)) {
                            var issueAddLabelResult = await GitHubApiAsync(client, c => c.Issue.Labels.AddToIssue(repository.Id, issueResult.Number, new[] { setting.Label.Omit }));
                            isOmit = true;
                        }
                    }

                    var commentResult = await GitHubApiAsync(client, c => c.Issue.Comment.Create(repository.Id, issueResult.Number, commentContent));
                    ConsoleUtility.LogDebug($"{num++}/{comments.Length} 課題コメント結果: {commentResult.Id}");
                }
            } else {
                ConsoleUtility.LogTrace("課題コメント無し");
            }

            if(bitbucketIssue.Status == "closed" || bitbucketIssue.Status == "resolved") {
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
                .Where(i => setting.Continue.StartIssueNumber <= i.Id)
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
                if(DisplayApiInfoCount <= ++ApiCallCount) {
                    var apiInfo = client.GetLastApiInfo();
                    ShowApiInfo(apiInfo);
                    ApiCallCount = 0;
                }

                if(!bitbucketIssueComments.TryGetValue(issue.Id, out var comments)) {
                    comments = Array.Empty<BitbucketComment>();
                }
                await CreateIssueAsync(client, repository, issue, comments, setting);
            }
        }

        private async Task ExecuteCoreAsync(Setting setting, TimeSpan delayTime, string accessToken)
        {
            DelayTime = delayTime;

            var bitbucketDb = LoadBitbucketDb(setting.Bitbucket);

            var client = await CreateGitHubClientAsync(setting.GitHub, accessToken);

            var repository = await GitHubApiAsync(client, c => c.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository));

            if(setting.Continue.BuildLabel) {
                if(setting.Label.Items.Any()) {
                    ConsoleUtility.Title("ラベル構築");
                    await ApplyLabelAsync(client, repository, setting.Label);
                    ConsoleUtility.LogWarning("ラベル構築 未実施");
                }
            }

            ConsoleUtility.Title("課題構築");
            await ApplyIssuesAsync(client, repository, bitbucketDb, setting);

        }

        public Task ExecuteAsync()
        {
            var setting = LoadSetting();
            return ExecuteCoreAsync(setting.setting, setting.delayTime, setting.accessToken);
        }

        #endregion
    }
}
