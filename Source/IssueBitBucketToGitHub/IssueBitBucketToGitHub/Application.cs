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

        private void ExecuteCore(string apiBase, string apiToken, string owner, string repository, string issueDirectoryPath)
        {

        }

        public void Execute()
        {
            var commandLine = new CommandLine();
            commandLine.Add(shortKey: 'b', longKey: "api-base", hasValue: true);
            commandLine.Add(shortKey: 't', longKey: "api-token", hasValue: true);
            commandLine.Add(shortKey: 'o', longKey: "owner", hasValue: true);
            commandLine.Add(shortKey: 'r', longKey: "repository", hasValue: true);
            var issueDirKey = commandLine.Add(shortKey: 'i', longKey: "issue-directory", hasValue: true);

            if(!commandLine.Parse()) {
                throw new InvalidOperationException();
            }

            var apiBase = ConsoleUtility.ReadRetryDefault("api-base", commandLine.GetValue("api-base", "https://api.github.com"));
            var apiToken = ConsoleUtility.ReadRetryDefault("api-token", commandLine.GetValue("api-token", string.Empty));
            var owner = ConsoleUtility.ReadRetryDefault("owner", commandLine.GetValue("owner", string.Empty));
            var repository = ConsoleUtility.ReadRetryDefault("repository", commandLine.GetValue("repository", string.Empty));
            var issueDirectoryPath = ConsoleUtility.ReadRetryDefault("issues dir", commandLine.GetValue("issue-directory", string.Empty));

            if(!Directory.Exists(issueDirectoryPath)) {
                throw new Exception(issueDirKey.LongKey + ": " + issueDirectoryPath);
            }

            ExecuteCore(apiBase, apiToken, owner, repository, issueDirectoryPath);
        }

        #endregion
    }
}
