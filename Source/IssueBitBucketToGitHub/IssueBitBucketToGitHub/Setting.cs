using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public record Setting(
        Uri BaseUrl,
        string ApiToken,
        string Owner,
        string Repository,
        string IssueDirectoryPath
    );
}
