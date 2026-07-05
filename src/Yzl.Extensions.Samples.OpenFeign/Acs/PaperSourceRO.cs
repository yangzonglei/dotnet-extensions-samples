using Newtonsoft.Json;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class PaperSourceRO
{
    public int Id { get; set; }

    public string? PaperTitle { get; set; }

    public int LearnGrade { get; set; }
    public string gradeName { get; set; } = "";
    public int PaperType { get; set; }
    public string paperTypeName { get; set; } = "";
    public List<int>? PaperTypes { get; set; }
    public int Province { get; set; }
    public string? ProvinceName { get; set; }
    public int PaperYear { get; set; }

    public int PaperLevel { get; set; }

    public DateTime AddTimeOn { get; set; }
    public int SchoolId { get; set; }
    public string? Areas { get; set; }
    public string? AreaName { get; set; }
    public bool AuditResults { get; set; }
    /// <summary>
    /// 匹配度(试题来源字段与筛选条件的匹配度)
    /// </summary>
    [JsonIgnore]
    public int MatchingDegree { get; set; } = 1;
    /**
     * 试卷等级
    */
    public int examLevel { get; set; }
    public string examLevelName { get; set; } = "";
    /**
     * 学期标识
     * 1上
     * 2下
     */
    public int schoolTerm { get; set; }

    public SchoolTagRO? schoolTag { get; set; }
}
