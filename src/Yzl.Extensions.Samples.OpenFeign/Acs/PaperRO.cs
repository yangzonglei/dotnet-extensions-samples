namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class PaperRO : BaseEntity
{
    /// <summary>
    /// id
    /// </summary>
    public int id { get; set; }
    /// <summary>
    /// 试卷名称
    /// </summary>
    public string? title { get; set; }
    /// <summary>
    /// 试卷名称(html)
    /// </summary>
    public string? examTitle { get; set; }
    public int bankId { get; set; }
    /// <summary>
    /// 所属地区
    /// </summary>
    public AreaRO? area { get; set; }

    public CityRO? city { get; set; }

    /// <summary>
    /// 所属年级
    /// </summary>
    public GradeRO? grade { get; set; }
    /// <summary>
    /// 试卷类型
    /// </summary>
    public PaperTypeRO? paperType { get; set; }
    public PaperLevel? paperLevel { get; set; }
    /// <summary>
    /// 阅读数量 
    /// </summary>
    public int readSum { get; set; }

    /// <summary>
    /// 试卷年份
    /// </summary>
    public int? year { get; set; } = 0;
    /// <summary>
    /// 试卷收入日期
    /// </summary>
    public DateTime time { get; set; }
    /// <summary>
    /// 试题总数
    /// </summary>
    public int quesCount { get; set; }

    public List<QuestionRO>? quesList { get; set; }
    /// <summary>
    /// 学校ID
    /// </summary>
    public int schoolId { get; set; }
    /// <summary>
    /// 抢先版试卷
    /// </summary>
    public int isFreshPaper { get; set; }

    public SchoolTagRO? schoolTag { get; set; }
    /// <summary>
    /// qbmid
    /// </summary>
    public string qbmId { get; set; } = "";

    /// <summary>
    /// 试卷标签 1: 音频
    /// </summary>
    public List<int> paperTags { get; set; } = new List<int>();

    /**
     * 试卷审核状态
     */
    public bool auditResults { get; set; } = false;
    /// <summary>
    /// 试卷难度
    /// </summary>
    public string paperDifficulty { get; set; } = "";
}
