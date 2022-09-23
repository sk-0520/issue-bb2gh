using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using ContentTypeTextNet.Pe.Core.Models;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class Application
    {
        #region function

        private Setting LoadSetting()
        {
            var commandLine = new CommandLine();
            commandLine.Add(shortKey: 'b', longKey: "base-url", hasValue: true);
            commandLine.Add(shortKey: 't', longKey: "api-token", hasValue: true);
            commandLine.Add(shortKey: 'o', longKey: "owner", hasValue: true);
            commandLine.Add(shortKey: 'r', longKey: "repository", hasValue: true);
            var issueDirKey = commandLine.Add(shortKey: 'i', longKey: "issue-directory", hasValue: true);

            if(!commandLine.Parse()) {
                throw new InvalidOperationException();
            }

            var baseUrl = ConsoleUtility.ReadRetryDefault("base-url", commandLine.GetValue("base-url", "https://api.github.com"));
            var apiToken = ConsoleUtility.ReadRetryDefault("api-token", commandLine.GetValue("api-token", string.Empty));
            var owner = ConsoleUtility.ReadRetryDefault("owner", commandLine.GetValue("owner", string.Empty));
            var repository = ConsoleUtility.ReadRetryDefault("repository", commandLine.GetValue("repository", string.Empty));
            var issueDirectoryPath = ConsoleUtility.ReadRetryDefault("issues dir", commandLine.GetValue("issue-directory", string.Empty));

            var setting = new Setting {
                BaseUrl = baseUrl,
                ApiToken = apiToken,
                Owner = owner,
                Repository = repository,
                IssueDirectoryPath = issueDirectoryPath,
            };

            if(!Directory.Exists(setting.IssueDirectoryPath)) {
                throw new Exception(issueDirKey.LongKey + ": " + setting.IssueDirectoryPath);
            }

            return setting;
        }

        private void ExecuteCore(Setting setting)
        {

        }

        public void Execute()
        {
            var setting = LoadSetting();
            ExecuteCore(setting);
        }

        #endregion
    }
}
