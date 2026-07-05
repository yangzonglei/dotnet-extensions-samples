using Newtonsoft.Json;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class ZuJuanUserSettingDo : BaseEntity
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("settingKey")]
    public string? SettingKey { get; set; }

    [JsonProperty("settingValue")]
    public string? SettingValue { get; set; }

    [JsonProperty("updateTime")]
    public DateTime UpdateTime { get; set; }
}
