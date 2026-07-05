using Newtonsoft.Json;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class UserSettingQo
{
    [JsonProperty("userId")] public int UserId { get; set; }

    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
}
