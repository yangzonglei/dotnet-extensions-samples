# Yzl.Extensions.SpringBoot.Admin.Client.Net 更新日志

## v0.1.9 [2026/06/29]

### 🚀 新功能
1. **`SbaClientOptions` 改用 `[ConfigurationProperties]` 特性配置绑定**：添加 `[ConfigurationProperties("spring:boot:admin:client")]` 特性声明配置前缀，通过 `AddConfigurationPropertiesScan` 自动扫描注册，替代手动 `services.Configure<T>()` 绑定

### 🔧 修复
1. **更新命名空间引用**：`Yzl.Extensions.Common` → `Yzl.Extensions.Core`，适配 Core 包重命名
2. **新增 `PackageId` 属性**：csproj 中显式声明 `Yzl.Extensions.SpringBoot.Admin.Client.Net` 包 ID

## v0.1.8 [2026/06/28]

### 新功能
1. **实例 ID 跟踪**：首次注册 `POST /instances` 后解析响应 body 中的 `id` 字段，保存到 `ISbaRegistrationService.InstanceId`
2. **注册优化**：首次注册 `POST /instances` 后解析响应 body 中的 `id` 字段，保存到 `ISbaRegistrationService.InstanceId`，供反注册使用
3. **自动降级**：POST 失败时自动重试（指数退避），SBA Server 重启后重新注册获取新 ID
4. **自动反注册**：应用关闭时发送 `DELETE /instances/{id}` 通知 SBA Server 立即移除实例，对齐 `spring.boot.admin.client.auto-deregistration`
5. **AutoRegistration 开关**：新增 `SbaClientOptions.AutoRegistration` 属性（默认 true），设为 false 后跳过自动注册
6. **AutoDeregistration 开关**：新增 `SbaClientOptions.AutoDeregistration` 属性（默认 true），设为 false 后关闭时不移除实例
7. **ISbaRegistrationService 接口扩展**：新增 `DeregisterAsync()` 方法和 `InstanceId` 只读属性
8. 注册 **metadata** 新增 `build-version` 字段，从 `AssemblyInformationalVersionAttribute` 读取版本号，解决 SBA `/applications` 接口中 `buildVersion: null` 问题
9. **升级 Yzl.Extensions.Actuator 依赖版本至 0.1.8**

### 配置示例
```json
{
  "spring": {
    "boot": {
      "admin": {
        "client": {
          "Url": "http://localhost:9090",
          "AutoRegistration": true,
          "AutoDeregistration": true
        }
      }
    }
  }
}
```

## v0.1.7 
-

## v0.1.6 
-

## v0.1.5 [2026/05/14]
1. 升级 Yzl.Extensions.Actuator 依赖版本至 0.1.5

## v0.1.4 [2026/05/10]
1. 优化 Spring Boot Admin Client 注册与心跳流程，注册请求复用 HttpClient，并缓存应用名、实例地址、端点列表、metadata 与 payload，减少重复构建开销
2. 心跳服务改为启动后立即注册，注册失败时使用 ILogger 记录告警，并按 RefreshInterval 进行指数退避重试，最大退避到 8 倍刷新间隔
3. 优化 Actuator URL 自动解析逻辑，解析结果增加线程安全缓存，并移除对 IHostApplicationLifetime 的依赖
4. 注册 metadata 新增 startup 启动时间，并使用强类型 payload 与 endpoint descriptor 生成 Spring Boot Admin 实例信息
5. 优化本机端口解析工具，清理 GeneratedRegex 区域性参数并规范配置读取格式
6. 新增项目架构与执行流程文档，补充依赖注入、URL 解析、注册 payload、心跳重试等核心流程说明

## v0.1.1 [2026/04/10]
1. 升级版本号

## v0.1.0 [2026/04/09]
1. 首次发布