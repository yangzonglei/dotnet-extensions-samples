namespace Yzl.Extensions.Samples.Cache.Models;

/// <summary>
/// 用户数据传输对象 —— 用于演示缓存的基础数据模型
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 查询参数对象 —— 用于演示 SpEL 嵌套属性访问
/// </summary>
public class QueryQo
{
    public int UserId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 产品数据传输对象 —— 用于演示多缓存区域的场景
/// </summary>
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// 订单数据传输对象 —— 用于演示复杂 SpEL 表达式
/// </summary>
public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
