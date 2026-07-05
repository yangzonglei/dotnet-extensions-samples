using Newtonsoft.Json;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

/// <summary>
/// 学段对象
/// </summary>
public class Edu
{
    public int ID { set; get; }

    public string? Name { set; get; }

    public List<QuesBankVO>? QuesBankList { set; get; }
}

/// <summary>
/// 题库对象
/// </summary>
public class QuesBankVO : BaseEntity
{
    public int ID { set; get; }
    public string? Name { set; get; }
    public int Subject { set; get; }

    public string? subjectName { get; set; }

    public int EduID { set; get; }

    public string? EduName { set; get; }

    public List<QuesType>? QuesTypeList { set; get; }

    public List<QuesAbility>? QuesAbilityList { set; get; }

    public List<Category>? CategoryList { set; get; }

    public List<CategorySynchronize>? CategorySynchronizeList { set; get; }

    public List<LearnGrade>? LearnGradeList { set; get; }
    public List<QuesAttributeModel>? QuesAttributeList { get; set; }
    /// <summary>
    /// 是否是同步试题
    /// </summary>
    public QuesSyncModel? QuesSync { get; set; }

    /// <summary>
    /// 课程id
    /// </summary>
    [JsonProperty("courseId")]
    public int CourseId { get; set; }

    /// <summary>
    /// 课程类型 1:公共课；2:专业课
    /// </summary>
    [JsonProperty("courseType")]
    public int CourseType { get; set; } = 1;

    /// <summary>
    /// 课程简拼
    /// </summary>
    [JsonProperty("courseIdPy")]
    public string CourseIdPy { get; set; } = string.Empty;
}

/// <summary>
/// 试题是否同步
/// </summary>
public class QuesSyncModel
{
    public int ID { get; set; }

    public int BankID { get; set; }

    public string? Sync { get; set; }

    public string? NoSync { get; set; }
}

/// <summary>
/// 试题分类
/// </summary>
public class QuesAttributeModel
{
    public int ID { get; set; }

    public string? Name { get; set; }
    public int isShow { get; set; }
}

/// <summary>
/// 题型对象
/// </summary>
public class QuesType
{
    public int ID { set; get; }

    public string? Name { set; get; }

    [JsonProperty("selectType")]
    public bool IsSelectType { set; get; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// 新增二级题型 [2017-12-07 13:48:48 by:yzl]
    /// </summary>
    [JsonProperty("pId")]
    public int ParentId { get; set; }

    /// <summary>
    /// 题型解析价格
    /// </summary>
    public decimal? money { get; set; }

    /// <summary>
    /// 子集题型
    /// </summary>
    public QuesType? childType { get; set; }
    /// <summary>
    ///  试题类型名称 (题型-业务特征/结构特征)
    /// </summary>
    public string typeName { get; set; } = "";
}

/// <summary>
/// 试题能力层次对象
/// </summary>
public class QuesAbility
{
    public int ID { set; get; }

    public string? Name { set; get; }
}

/// <summary>
/// 试题分类对象
/// </summary>
public class Category
{
    public enum CategoryTreeTypeEnum
    {
        /// <summary>
        /// 章节
        /// </summary>
        Chapter,

        /// <summary>
        /// 知识点
        /// </summary>
        Knowledge,

        /// <summary>
        /// 解题方法
        /// </summary>
        Tricks
    }
    public int ID { get; set; }
    public string? Name { get; set; }
    public int Type { get; set; }
    public int qbmId { get; set; }
    public int parentId { get; set; }
    public int knowledgeType { get; set; }
}

public class NewCategory
{
    public int ID { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// 试题分类对象(同步)
/// </summary>
public class CategorySynchronize
{
    public int ID { get; set; }

    public string? Name { get; set; }
}

/// <summary>
/// 年级对象
/// </summary>
public class LearnGrade
{
    public int ID { set; get; }

    public string? Name { set; get; }

    public List<PaperType>? PaperTypeList { set; get; }
}

/// <summary>
/// 年级对象
/// </summary>
public class Term
{
    public int id { set; get; }
    public string name { set; get; } = "";
}

/// <summary>
/// 试卷类型对象
/// </summary>
public class PaperType
{
    public int ID { set; get; }

    public string? Name { set; get; }
    public int ParentId { set; get; }
}

/// <summary>
/// 试题难度对象
/// </summary>
public class QuesDiff
{
    public int ID { set; get; }

    public float DifValue { get; set; }

    public string? Name { set; get; }
}

/// <summary>
/// 试卷等级对象
/// </summary>
public class PaperLevel
{
    public int ID { set; get; }
    public string? Name { set; get; }
}

/// <summary>
/// 省份对象
/// </summary>
public class Province
{
    public int ID { set; get; }
    public string? Name { set; get; }
    public string? ShortName { set; get; }
}

/// <summary>
/// 试卷年份对象
/// </summary>
public class PaperYear
{
    public int ID { set; get; }
    public string? Name { set; get; }
}

/// <summary>
/// 试卷对象
/// </summary>
public class Paper
{
    public int ID { set; get; }

    public string? Title { set; get; }

    public string? Note { set; get; }

    public LearnGrade? LearnGrade { set; get; }

    public PaperType? PaperType { set; get; }

    public Province? Province { set; get; }

    public PaperYear? PaperYear { set; get; }

    public PaperLevel? PaperLevel { set; get; }

    public DateTime Time { set; get; }

    public List<Ques>? QuesList { set; get; }
}

/// <summary>
/// 试题对象
/// </summary>
public class Ques
{
    public int ID { set; get; }
    [JsonProperty("quesGuid")]
    public Guid Guid { set; get; }
    public string? Title { set; get; }
    public string? From { set; get; }
    public string? Knowledge { set; get; }
    public QuesType? QuesType { set; get; }

    public QuesAbility? QuesAbility { set; get; }

    public QuesDiff? QuesDiff { set; get; }

    public double quesDiffValue { get; set; }

    public List<List<Category>>? categoriesList;

    [JsonProperty("quesChildNum")]
    public int ChildNum { set; get; }
    public DateTime Time { set; get; }
    public string? QuesBody { set; get; }
    public string? QuesAnswer { set; get; }
    public string? QuesParse { set; get; }
    public int UseSum { set; get; }

    public double AvgScore { get; set; }
    /// <summary>
    /// 冗余属性-答案和解析的秘钥
    /// </summary>
    public string? ParseAnswerKey { get; set; }
    /// <summary>
    /// 试题分类
    /// </summary>
    public QuesAttribute? QuesAttribute { get; set; }

    /// <summary>
    /// 试题标题简称
    /// </summary>
    public string? TitleAbbreviation { get; set; }
    /// <summary>
    /// 试卷来源
    /// </summary>
    public List<PaperAttribute>? PaperSource { get; set; }

    /// <summary>
    /// 试题上传来源 1：自主导题 2：委托导题
    /// </summary>
    [JsonProperty("schoolQuesSource")]
    public int UploadSource { get; set; }

    public string? zxxkUserName { get; set; }
}

public class QuesAttribute
{
    public int ID { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// 试卷属性
/// </summary>
public class PaperAttribute
{
    public int Id { get; set; }

    public string? PaperTitle { get; set; }

    public int LearnGrade { get; set; }

    public int PaperType { get; set; }

    public int Province { get; set; }

    public int PaperYear { get; set; }

    public int PaperLevel { get; set; }

    public DateTime AddTimeOn { get; set; }

    public int SchoolId { get; set; }

    /// <summary>
    /// 匹配度(试题来源字段与筛选条件的匹配度)
    /// </summary>
    public int MatchingDegree { get; set; }
}
