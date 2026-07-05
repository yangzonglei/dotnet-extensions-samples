using Newtonsoft.Json;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

public class QuestionRO
{
    public string? quesCode { get; set; }
    public int id { set; get; }
    public int bankId { set; get; }
    public string? title { set; get; }
    public string? auditPath { get; set; }

    public string? from { set; get; }

    public string? quesGuid { set; get; }

    public string? qbmId { set; get; }

    public string? quesBody { set; get; }

    public string? knowledge { set; get; }

    public int useSum { set; get; }

    public Double quesDiffValue { set; get; }

    public Double quesScore { set; get; }

    public DateTime? time { set; get; }

    public int quesChildNum { set; get; }

    public QuesType? quesType { set; get; }

    public QuesDiff? quesDiff { set; get; }

    /// <summary>
    /// 章节知识点
    /// </summary>
    public List<List<CategoryTreeRO>>? categoriesList { get; set; }

    [JsonProperty("paperSource")] public List<PaperSourceRO>? paperSource { get; set; }

    /// <summary>
    /// 试题分类
    /// </summary>
    public QuesAttribute? quesAttribute { get; set; }

    public List<ChildQues>? childQues { get; set; }

    public List<CommonObj>? options { get; set; }

    /// <summary>
    /// 从前台设置的分值
    /// </summary>
    public List<Score>? score_list { get; set; }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

    public string? Key { get; set; }

    /// <summary>
    ///  1-典型题 2：压轴题 3：同步题 4：新题速递 5：新文化题 6：是否有变试题 7：真题 8：名校
    /// </summary>
    public List<int?>? tags { get; set; }

    /// <summary>
    /// 拓展阅读类型
    /// </summary>
    [JsonProperty("type")]
    public int ReadType { get; set; } = 0;

    /// <summary>
    /// 0-下线，1-上线，2-专题
    /// </summary>
    public int status { get; set; }

    /// <summary>
    /// 审核状态
    /// </summary>
    public bool auditResults { get; set; } = false;

    /// <summary>
    /// 试题视频
    /// </summary>
    public string? quesVideo { get; set; }

    /// <summary>
    /// 试题音频
    /// </summary>
    public string? quesAudio { get; set; }

    /// <summary>
    /// 试题大意
    /// </summary>
    public string? quesGist { set; get; }

    /// <summary>
    /// 试题上传来源 1：自主导题 2：委托导题 0：来源大库
    /// </summary>
    [JsonProperty("schoolQuesSource")]
    public int uploadSource { get; set; } = 0;

    public string? zxxkUserName { get; set; }

    /// <summary>
    /// 从前台设置的分值
    /// </summary>
    public List<Score>? scoreList { get; set; }

    /**
    * 变试题级别：基础、巩固、提高
    */
    public string? variantLevel { get; set; }
    public string? updateTime { get; set; }
    /**
     * 个人题库自定义目录
     */
    public int customTreeId { get; set; }

    /// <summary>
    /// 是否有附件 0：无 1：有
    /// </summary>
    public int hasAttachment { get; set; }
}

/// <summary>
/// 小题对象
/// </summary>
public class ChildQues
{
    public string? childAnswer { get; set; }
    public string? childBody { get; set; }
    public List<CommonObj>? childOptions { get; set; }
    public int number { get; set; }
}

public class CommonObj
{
    public int id { get; set; }
    public string? name { get; set; }
}

/// <summary>
/// 分值
/// </summary>
public class Score
{
    public int index { get; set; }
    public int score { get; set; }
}
