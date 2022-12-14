using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public class BitBucketVersion
    {
        #region property

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        #endregion
    }

    public class BitBucketMilestone
    {
        #region property

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        #endregion
    }

    public class BitbucketIssue
    {
        #region property

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("reporter")]
        public string Reporter { get; set; } = string.Empty;

        [JsonPropertyName("assignee")]
        public string Assignee { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("created_on")]
        public DateTime CreatedOn { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("milestone")]
        public string? Milestone { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("component")]
        public string Component { get; set; } = string.Empty;

        #endregion
    }

    public class BitbucketComment
    {
        #region property

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("issue")]
        public int Issue { get; set; }

        [JsonPropertyName("user")]
        public string User { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("created_on")]
        public DateTime CreatedOn { get; set; }

        #endregion
    }

    public class BitbucketDb
    {
        #region property

        [JsonPropertyName("milestones")]
        public BitBucketMilestone[] Milestones { get; set; } = Array.Empty<BitBucketMilestone>();

        [JsonPropertyName("versions")]
        public BitBucketVersion[] Versions { get; set; } = Array.Empty<BitBucketVersion>();

        [JsonPropertyName("issues")]
        public BitbucketIssue[] Issues { get; set; } = Array.Empty<BitbucketIssue>();
        [JsonPropertyName("comments")]
        public BitbucketComment[] Comments { get; set; } = Array.Empty<BitbucketComment>();

        #endregion
    }
}
