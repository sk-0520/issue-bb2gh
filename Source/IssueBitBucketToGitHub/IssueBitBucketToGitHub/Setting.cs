using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class ContinueSetting
    {
        #region proeprty

        public bool BuildLabel { get; set; }
        public int StartIssueNumber { get; set; }

        #endregion
    }

    public class GitHubSetting
    {
        #region property

        public string BaseUrl { get; set; } = "https://api.github.com/";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string ClientRedirectUrl { get; set; } = "http://localhost:9876/";
        public string Owner { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        
        #endregion
    }

    public class BitbucketSetting
    {
        #region property

        public string IssueDirectoryPath { get; set; } = string.Empty;
        public string IssueBaseUrl { get; set; } = string.Empty;

        #endregion
    }

    public class TemplateSetting
    {
        #region property

        /// <summary>
        /// 課題タイトル。
        /// <para><c>${KEY}</c>で文字列置き換え</para>
        /// <list type="table">
        ///  <listheader>
        ///   <term>キー</term>
        ///   <term>項目</term>
        ///  </listheader>
        ///  <item>
        ///   <description>TITLE</description>
        ///   <description>元タイトル。</description>
        ///  </item>
        ///  <item>
        ///   <description>NUMBER</description>
        ///   <description>元課題番号。</description>
        ///  </item>
        /// </list>
        /// </summary>
        public string IssueTitle { get; set; } = "<Bitbucket:${NUMBER}> ${TITLE}";
        /// <summary>
        /// 課題本文。
        /// <para><c>${KEY}</c>で文字列置き換え</para>
        /// <list type="table">
        ///  <listheader>
        ///   <term>キー</term>
        ///   <term>項目</term>
        ///  </listheader>
        ///  <item>
        ///   <description>TITLE</description>
        ///   <description>元タイトル。</description>
        ///  </item>
        ///  <item>
        ///   <description>NUMBER</description>
        ///   <description>元課題番号。</description>
        ///  </item>
        ///  <item>
        ///   <description>MARKDOWN</description>
        ///   <description>元本文。</description>
        ///  </item>
        ///  <item>
        ///  <item>
        ///   <description>QUOTE_MARKDOWN</description>
        ///   <description>(引用)元本文。</description>
        ///  </item>
        ///  <item>
        ///   <description>URL</description>
        ///   <description>元URL。</description>
        ///  </item>
        ///  <item>
        ///   <description>CREATED_AT</description>
        ///   <description>元生成日時。</description>
        ///  </item>
        ///  <item>
        ///   <description>USER</description>
        ///   <description>元ユーザー。</description>
        ///  </item>
        /// </list>
        /// </summary>
        public string IssueBody { get; set; } = @"${MARKDOWN}

---------------------

### BitBucket から移植

| 項目 | 内容 |
|:-:|:--|
| 元課題 | [${NUMBER}](${URL}) |
| 起票日時 | `${CREATED_AT}` |
| 起票者 | `${USER}` |

";
        /// <summary>
        /// たぶん <see cref="IssueBody"/> と同じ。
        /// </summary>
        public string Comment { get; set; } = @"${MARKDOWN}

---------------------

### BitBucket から移植

| 項目 | 内容 |
|:-:|:--|
| 元課題 | [${NUMBER}](${URL}) |
| 起票日時 | `${CREATED_AT}` |
| 起票者 | `${USER}` |

";

        #endregion
    }

    public class UserSetting
    {
        #region property

        /// <summary>
        /// ユーザー名のマッピング。
        /// <para>キー「<c>*</c>」はマッピング設定なしの割り当て先。</para>
        /// </summary>
        public Dictionary<string, string> Mapping { get; set; } = new();

        #endregion
    }

    public class LabelMappingSetting
    {
        #region property

        /// <summary>
        /// Bitbucket: ステータス と紐づけるラベル。
        /// </summary>
        public Dictionary<string, string> Status { get; set; } = new();
        /// <summary>
        /// Bitbucket: タイプ と紐づけるラベル。
        /// </summary>
        public Dictionary<string, string> Kinds { get; set; } = new();
        /// <summary>
        /// Bitbucket: コンポーネント と紐づけるラベル。
        /// </summary>
        public Dictionary<string, string> Components { get; set; } = new();

        #endregion
    }

    public class LabelSetting
    {
        #region property

        /// <summary>
        /// 生成するラベル名。
        /// <para>設定されている場合、既存のラベルは全部消す。</para>
        /// </summary>
        public string[] Items { get; set; } = Array.Empty<string>();

        public string Force { get; set; } = string.Empty;
        public string Omit { get; set; } = string.Empty;

        public LabelMappingSetting Mapping { get; set; } = new();

        #endregion
    }

    public class Setting
    {
        #region property

        public ContinueSetting Continue { get; set; } = new();

        /// <summary>
        /// GitHub 設定。
        /// </summary>
        public GitHubSetting GitHub { get; set; } = new();

        /// <summary>
        /// Bitbucket 設定。
        /// </summary>
        public BitbucketSetting Bitbucket { get; set; } = new();

        /// <summary>
        /// ユーザー名。
        /// </summary>
        public UserSetting User { get; set; } = new();

        /// <summary>
        /// マイグレーションした際のテンプレート。
        /// </summary>
        public TemplateSetting Template { get; set; } = new();

        public LabelSetting Label { get; set; } = new();

        #endregion
    }
}
