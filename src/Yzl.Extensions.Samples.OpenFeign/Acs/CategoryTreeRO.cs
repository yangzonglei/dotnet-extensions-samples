namespace Yzl.Extensions.Samples.OpenFeign.Acs;

/// <summary>
/// 目录树节点
/// </summary>
public class CategoryTreeRO
{
    public int id { get; set; }
    public int parentId { get; set; }
    public string? name { get; set; }

    /// <summary>
    /// 是否选中
    /// </summary>
    public bool? @checked { get; set; }

    public List<CategoryTreeRO> children { get; set; }
}
