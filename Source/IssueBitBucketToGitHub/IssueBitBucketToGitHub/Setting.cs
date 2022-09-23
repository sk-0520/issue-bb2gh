using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class BasicSetting
    {
        #region property

        public string BaseUrl { get; set; } = "https://api.github.com";
        public string ApiToken { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string IssueDirectoryPath { get; set; } = string.Empty;

        #endregion
    }

    public class Setting
    {
        #region property

        public BasicSetting Basic { get; set; } = new();

        #endregion
    }
}
