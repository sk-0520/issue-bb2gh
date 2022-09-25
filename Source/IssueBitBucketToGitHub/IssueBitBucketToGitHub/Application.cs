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

        /// <summary>
        /// 指定なしの場合のAPI発行通常待機時間。
        /// </summary>
        //private const string RawDelayTime = "0.00:00:10.0";
        private const string RawDelayTime = "0.00:00:10.0";
        /// <summary>
        /// 現在レート表示を行う感覚(課題のみ)。
        /// </summary>
        private const int DisplayApiInfoCount = 10;
        private const int SecondaryRateLimitRetryMaxCount = 3;

        #endregion

        #region property

        /// <summary>
        /// レート制限(二次)発生時の待機時間に対して追加する時間
        /// </summary>
        private static TimeSpan AddSecondaryRateLimitDelayTime { get; } = TimeSpan.FromMinutes(5);

        //DateTime LastApiUseTime { get; set; } = DateTime.MinValue;
        /// <summary>
        /// API発行通常待機時間。
        /// </summary>
        private TimeSpan DelayTime { get; set; }
        private bool NeedsSleep { get; set; } = false;
        /// <summary>
        /// <see cref="DisplayApiInfoCount"/> に対するAPI呼び出し回数。
        /// </summary>
        private int ApiCallCount { get; set; } = 0;
        /// <summary>
        /// アプリケーション稼働中のAPI使用回数（ただ加算してるだけ）。
        /// </summary>
        private int ApiTotalCallCount { get; set; } = 0;

        private string[] CloseStatus { get; } = new[] {
            "closed",
            "resolved",
            "wontfix",
            "duplicate",
        };

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

            var delayTime = apiInfo.RateLimit.Reset - DateTime.UtcNow + AddSecondaryRateLimitDelayTime;

            if(delayTime.TotalMilliseconds < 0) {
                ConsoleUtility.LogError($"待たないよ: {delayTime}");
                return Task.CompletedTask;
            }

            ConsoleUtility.LogWarning($"こんだけ待つよ: {delayTime}");
            return Task.Delay(delayTime);
        }

        private async Task<TResult> GitHubApiAsync<TResult>(GitHubClient gitHubClient, Func<GitHubClient, Task<TResult>> funcAsync)
        {
            await SleepAsync();

            for(var i = 0; i < SecondaryRateLimitRetryMaxCount; i++) {
                try {
                    ConsoleUtility.LogTrace($"API: {++ApiTotalCallCount}");

                    var result = await funcAsync(gitHubClient);

                    return result;
                } catch(SecondaryRateLimitExceededException ex) {
                    ConsoleUtility.LogWarning(ex.ToString());
                    if(i + 1 == SecondaryRateLimitRetryMaxCount) {
                        throw;
                    }
                    await DelayRateAsync(gitHubClient);
                }
            }

            throw new NotImplementedException();
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
            if(!setting.Bitbucket.Version.IsLabel && !setting.Bitbucket.Milestone.IsLabel) {
                throw new Exception("バージョン・マイルストーンの両方を非ラベル設定にするのは無理");
            }


            var rawDelayTime = GetSettingValue("delay", commandLine, commandKeys.DelayTime, RawDelayTime, true);
            var delayTime = TimeSpan.Parse(rawDelayTime);

            var accessToken = GetSettingValue("access token", commandLine, commandKeys.AccessToken, string.Empty, true);

            return (setting: setting, delayTime: delayTime, accessToken: accessToken);
        }

        public BitbucketDb LoadBitbucketDb(BitbucketSetting bitbucketSetting)
        {
            var issueFilePath = Path.Combine(bitbucketSetting.IssueDirectoryPath, "db-1.0.json");

            using var stream = OpenFileStream(issueFilePath);
            var result = JsonSerializer.Deserialize<BitbucketDb>(stream);
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
                ConsoleUtility.Title("OAuth アクセストークン取得");

                string token = string.Empty;

                using(var http = new HttpListener()) {
                    http.Prefixes.Add(gitHubSetting.ClientRedirectUrl);
                    ConsoleUtility.LogInformation($"HTTP({gitHubSetting.ClientRedirectUrl}) 受付開始...");
                    http.Start();

                    var loginUrl = client.Oauth.GetGitHubLoginUrl(new OauthLoginRequest(gitHubSetting.ClientId) {
                        RedirectUri = new Uri(gitHubSetting.ClientRedirectUrl),
                        State = Guid.NewGuid().ToString("N"),
                        Scopes = {
                        "repo",
                    }
                    });

                    Process.Start(new ProcessStartInfo {
                        UseShellExecute = true,
                        FileName = loginUrl.OriginalString,
                    });

                    var context = await http.GetContextAsync();

                    var code = context.Request.QueryString.Get("code");
                    if(code is null) {
                        throw new InvalidOperationException();
                    }

                    var responseBody = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, new[] {
                        "OK: OAuth アクセストークンをメモって下さい",
                        "アクセストークン: " + code
                    }));
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.AddHeader("Content-Type", "text/plain; charset=utf-8");
                    await context.Response.OutputStream.WriteAsync(responseBody, 0, responseBody.Length);
                    await context.Response.OutputStream.FlushAsync();
                    context.Response.Close();

                    token = code;
                }

                var oauthToken = await client.Oauth.CreateAccessToken(new OauthTokenRequest(
                    gitHubSetting.ClientId,
                    gitHubSetting.ClientSecret,
                    token
                ));

                accessToken = oauthToken.AccessToken;

                ConsoleUtility.LogInformation("OAuth アクセストークン取得完了 (※実行例 --access-token=\"...\" )");
                Console.WriteLine(">> {0}", accessToken);
                Console.WriteLine("Enterキー入力で継続します。次回起動時は上記アクセストークンをコマンドライン指定してください。");
                Console.ReadLine();
            }

            client.Credentials = new Credentials(accessToken);

            return client;
        }

        private string BuildVersionLabelName(string template, string version)
        {
            var map = new Dictionary<string, string>() {
                ["VERSION"] = version,
            };
            return TextUtility.ReplaceFromDictionary(template, map);
        }
        private string BuildMilestoneLabelName(string template, string milestone)
        {
            var map = new Dictionary<string, string>() {
                ["MILESTONE"] = milestone,
            };
            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private async Task<int> ApplyLabelAsync(GitHubClient client, Repository repository, LabelSetting labelSetting, VersionSetting versionSetting, MilestoneSetting milestoneSetting, BitbucketDb bitbucketDb)
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

            ConsoleUtility.Subject("ラベル生成");
            var newLabels = labelSetting.Items.ToList();
            if(versionSetting.IsLabel) {
                newLabels.AddRange(bitbucketDb.Versions.Select(i => BuildVersionLabelName(versionSetting.LabelTemplate, i.Name)));
            }
            if(milestoneSetting.IsLabel) {
                newLabels.AddRange(bitbucketDb.Milestones.Select(i => BuildMilestoneLabelName(milestoneSetting.LabelTemplate, i.Name)));
            }
            int createdCount = 0;
            foreach(var item in newLabels) {
                ConsoleUtility.LogInformation($"ラベル作成: {item}");
                var newLabel = new NewLabel(item, "cccccc"); // 色なんか後で変えてくれ
                var label = await GitHubApiAsync(client, c => c.Issue.Labels.Create(repository.Id, newLabel));
                ConsoleUtility.LogDebug($"ラベル結果: {label.Id}");
                createdCount += 1;
            }

            return createdCount;
        }

        private async Task ApplyMilestoneAsync(GitHubClient client, Repository repository, IEnumerable<string> items)
        {
            var miscellaneous = await GitHubApiAsync(client, c => c.Issue.Milestone.GetAllForRepository(repository.Id));
            if(miscellaneous.Any()) {
                ConsoleUtility.Subject("既存マイルストーン破棄");

                foreach(var miscellane in miscellaneous) {
                    ConsoleUtility.LogInformation($"マイルストーン削除: {miscellane.Id}, {miscellane.Title}");
                    await GitHubApiAsync(client, c => c.Issue.Milestone.Delete(repository.Id, miscellane.Number));
                }
            } else {
                ConsoleUtility.LogTrace("削除マイルストーンなし");
            }

            ConsoleUtility.Subject("マイルストーン生成");

            foreach(var item in items) {
                ConsoleUtility.LogInformation($"マイルストーン作成: {item}");
                var newMilestone = new NewMilestone(item); // 細かい処理はwebでどうぞ。
                var milestone = await GitHubApiAsync(client, c => c.Issue.Milestone.Create(repository.Id, newMilestone));
                ConsoleUtility.LogDebug($"マイルストーン結果: {milestone.Id}");
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
                ["CREATED_AT"] = issue.CreatedOn.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
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
                ["CREATED_AT"] = comment.CreatedOn.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["USER"] = comment.User,
            };

            return TextUtility.ReplaceFromDictionary(template, map);
        }

        private async Task CreateIssueAsync(GitHubClient client, Repository repository, BitbucketIssue bitbucketIssue, BitbucketComment[] comments, Setting setting, IReadOnlyDictionary<string, Milestone> milestoneMap)
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
            // バージョン設定
            if(bitbucketIssue.Version is not null) {
                if(setting.Bitbucket.Version.IsLabel) {
                    githubIssue.Labels.Add(BuildVersionLabelName(setting.Bitbucket.Version.LabelTemplate, bitbucketIssue.Version));
                } else {
                    githubIssue.Milestone = milestoneMap[bitbucketIssue.Version].Number;
                }
            }
            // マイルストーン設定
            if(bitbucketIssue.Milestone is not null) {
                if(setting.Bitbucket.Milestone.IsLabel) {
                    githubIssue.Labels.Add(BuildMilestoneLabelName(setting.Bitbucket.Version.LabelTemplate, bitbucketIssue.Milestone));
                } else {
                    githubIssue.Milestone = milestoneMap[bitbucketIssue.Milestone].Number;
                }
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


            if(CloseStatus.Contains(bitbucketIssue.Status)) {
                var githubUpdateIssue = new IssueUpdate() {
                    State = ItemState.Closed,
                };
                // マイルストーンの再設定(これしないとクローズの時点でマイルストーンが消える)
                if(bitbucketIssue.Version is not null && !setting.Bitbucket.Version.IsLabel) {
                    githubUpdateIssue.Milestone = milestoneMap[bitbucketIssue.Version].Number;
                } else if(bitbucketIssue.Milestone is not null && !setting.Bitbucket.Milestone.IsLabel) {
                    githubUpdateIssue.Milestone = milestoneMap[bitbucketIssue.Milestone].Number;
                }

                var issueCloseResult = await GitHubApiAsync(client, c => c.Issue.Update(repository.Id, issueResult.Number, githubUpdateIssue));
                ConsoleUtility.LogDebug($"課題クローズ: {issueCloseResult.Id}");
            }
        }

        private async Task ApplyIssuesAsync(GitHubClient client, Repository repository, BitbucketDb bitbucketDb, Setting setting)
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

            ConsoleUtility.Title("マイルストーン一覧取得");
            var milestone = await GitHubApiAsync(client, c => c.Issue.Milestone.GetAllForRepository(repository.Id, ApiOptions.None));
            var milestoneMap = milestone.ToDictionary(k => k.Title, v => v);

            foreach(var issue in bitbucketIssueList) {
                if(DisplayApiInfoCount <= ++ApiCallCount) {
                    var apiInfo = client.GetLastApiInfo();
                    ShowApiInfo(apiInfo);
                    ApiCallCount = 0;
                }

                if(!bitbucketIssueComments.TryGetValue(issue.Id, out var comments)) {
                    comments = Array.Empty<BitbucketComment>();
                }
                await CreateIssueAsync(client, repository, issue, comments, setting, milestoneMap);
            }
        }

        private async Task ExecuteCoreAsync(Setting setting, TimeSpan delayTime, string accessToken)
        {
            DelayTime = delayTime;

            var bitbucketDb = LoadBitbucketDb(setting.Bitbucket);

            var client = await CreateGitHubClientAsync(setting.GitHub, accessToken);

            //var repository = await GitHubApiAsync(client, c => c.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository));
            var repository = await client.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository);

            if(setting.Continue.BuildLabel) {
                ConsoleUtility.Title("ラベル構築");
                var labelCount = await ApplyLabelAsync(client, repository, setting.Label, setting.Bitbucket.Version, setting.Bitbucket.Milestone, bitbucketDb);
                ConsoleUtility.LogDebug($"ラベル数: {labelCount}");
            }

            if(setting.Continue.BuildVersion) {
                if(!setting.Bitbucket.Version.IsLabel) {
                    ConsoleUtility.Title("バージョンによるマイルストーン構築");
                    var items = bitbucketDb.Versions.Select(i => i.Name).ToArray();
                    await ApplyMilestoneAsync(client, repository, items);
                }
            }

            if(setting.Continue.BuildMilestone) {
                if(!setting.Bitbucket.Milestone.IsLabel) {
                    ConsoleUtility.Title("マイルストーン構築");
                    var items = bitbucketDb.Milestones.Select(i => i.Name).ToArray();
                    await ApplyMilestoneAsync(client, repository, items);
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
