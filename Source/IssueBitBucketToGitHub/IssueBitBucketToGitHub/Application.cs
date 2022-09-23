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

        private async Task ClearLabelAsync(GitHubClient client, Repository repository, LabelSetting labelSetting)
        {
            var labels = await client.Issue.Labels.GetAllForRepository(repository.Id);
            if(labels.Any()) {
                foreach(var label in labels) {
                    ConsoleUtility.LogInformation($"ラベル削除: {label.Id}, {label.Name}");
                    await client.Issue.Labels.Delete(repository.Id, label.Name);
                }
            } else {
                ConsoleUtility.LogInformation("削除ラベルなし");
            }

            foreach(var item in labelSetting.Items) {
                ConsoleUtility.LogInformation($"ラベル作成: {item}");
                var newLabel = new NewLabel(item, "cccccc"); // 色なんか後で変えてくれ
                var label = await client.Issue.Labels.Create(repository.Id, newLabel);
                ConsoleUtility.LogInformation($"ラベル結果: {label.Id}");
            }
        }

        private async Task ExecuteCoreAsync(Setting setting)
        {
            // Bitbucket Issue を読み込む
            var bitbucketIssues = LoadBitbucketIssues(setting.Bitbucket);

            var client = CreateGitHubClient(setting.GitHub);

            var repository = await client.Repository.Get(setting.GitHub.Owner, setting.GitHub.Repository);

            if(setting.Label.Items.Any()) {
                ConsoleUtility.Title("ラベル構築");
                await ClearLabelAsync(client, repository, setting.Label);
            }

        }

        public Task ExecuteAsync()
        {
            var setting = LoadSetting();
            return ExecuteCoreAsync(setting);
        }

        #endregion
    }
}
