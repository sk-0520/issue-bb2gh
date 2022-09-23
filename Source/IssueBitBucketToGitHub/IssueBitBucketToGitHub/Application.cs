using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using ContentTypeTextNet.Pe.Core.Models;
using Octokit;
using Octokit.Internal;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class Application
    {
        #region function

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
                BaseUrl = commandLine.Add(shortKey: 'b', longKey: "base-url", hasValue: true),
                ApiToken = commandLine.Add(shortKey: 't', longKey: "api-token", hasValue: true),
                Owner = commandLine.Add(shortKey: 'o', longKey: "owner", hasValue: true),
                Repository = commandLine.Add(shortKey: 'r', longKey: "repository", hasValue: true),
                IssueDirectory = commandLine.Add(shortKey: 'i', longKey: "issue-directory", hasValue: true),
            };

            if(!commandLine.Parse()) {
                throw new InvalidOperationException();
            }

            var baseUrl = GetSettingValue("base-url", commandLine, commandKeys.BaseUrl, "https://api.github.com", true);
            var apiToken = GetSettingValue("api-token", commandLine, commandKeys.ApiToken, string.Empty, false);
            var owner = GetSettingValue("owner", commandLine, commandKeys.Owner, string.Empty, false);
            var repository = GetSettingValue("repository", commandLine, commandKeys.Repository, string.Empty, false);
            var issueDirectoryPath = GetSettingValue("issues dir", commandLine, commandKeys.IssueDirectory, string.Empty, false);

            var setting = new Setting(
                new Uri(baseUrl),
                apiToken,
                owner,
                repository,
                issueDirectoryPath
            );

            if(!Directory.Exists(setting.IssueDirectoryPath)) {
                throw new Exception(commandKeys.IssueDirectory.LongKey + ": " + setting.IssueDirectoryPath);
            }

            return setting;
        }

        private GitHubClient CreateGitHubClient(Setting setting)
        {
            var productHeaderValue = new ProductHeaderValue(Assembly.GetExecutingAssembly().GetName().Name);
            var credentials = new Credentials(setting.ApiToken);
            var client = new GitHubClient(productHeaderValue, new InMemoryCredentialStore(credentials), setting.BaseUrl);

            return client;
        }

        private async Task ExecuteCoreAsync(Setting setting)
        {
            // Bitbucket Issue を読み込む

            var client = CreateGitHubClient(setting);

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
