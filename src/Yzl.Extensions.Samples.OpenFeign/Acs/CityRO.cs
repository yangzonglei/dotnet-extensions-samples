namespace Yzl.Extensions.Samples.OpenFeign.Acs;

/// <summary>
/// 城市
/// </summary>
public class CityRO
{
    /// <summary>
    /// 城市Id
    /// </summary>
    public int id { get; set; }

    /// <summary>
    /// 父Id
    /// </summary>
    public int parentId { get; set; }

    /// <summary>
    /// 城市名称
    /// </summary>
    public string? name { get; set; }
}
