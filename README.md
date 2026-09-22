# 基于 QoS 的 Web Service 动态调用平台

这是一个使用 .NET 10 复现毕业设计思路的完整示例：系统维护多个服务类别及候选提供者，根据 QoS 指标动态评分、选择和绑定服务，并支持一次并行组合调用 1～4 类能力。

项目同时支持 REST/JSON 和 SOAP 1.1/WSDL。SOAP 客户端不依赖生成的代理类，而是在运行时读取并校验 WSDL，再根据注册信息组装 SOAP 请求。

## 文档

- [实施状态与设计说明](docs/PLAN.md)
- [UML 设计](docs/UML.md)
- [Docker 部署说明](docs/DEPLOYMENT.md)

## 已实现功能

- 天气、物流、汇率、消息四类服务，共 10 个候选提供者。
- 天气类别包含 2 个 REST 候选和 1 个 SOAP/WSDL 候选。
- 可用性、成功率、响应时间、成本、负载五维 QoS 归一化评分。
- 页面自定义 QoS 权重，候选排名和评分过程可解释。
- 1～4 类任务通过 `Task.WhenAll` 并行编排。
- 2 秒调用超时，失败后按排名自动尝试下一候选。
- 连续失败 2 次熔断 20 秒，之后自动提供恢复尝试机会。
- 后台每 10 秒检查 Provider 健康状态，离线端点退出候选池。
- 最近 100 次组合调用持久化到 JSON。
- Razor Pages 控制台和 `POST /api/composite` 组合调用 API。
- Docker Compose 两容器部署和命名卷历史持久化。
- 单元测试、集成测试和真实 REST/SOAP 端到端验证。

## 推荐启动方式

当前机器已经安装 WSL 2 和 Docker Desktop，镜像也已成功构建。执行：

```powershell
.\scripts\start-demo.cmd
```

访问：

- Web 控制台：<http://localhost:5200>
- Web 健康检查：<http://localhost:5200/health>
- Provider 健康检查：<http://localhost:5201/health>
- SOAP WSDL：<http://localhost:5201/soap/legacy-weather?wsdl>

停止环境：

```powershell
.\scripts\stop-demo.cmd
```

使用 `.cmd` 是为了避免 Windows PowerShell 执行策略阻止 `.ps1` 文件。

## 本地开发启动

不使用 Docker 时，打开两个终端：

```powershell
dotnet run --project src/QosServicePlatform.MockProviders
```

```powershell
dotnet run --project src/QosServicePlatform.Web --urls http://localhost:5200
```

编译与测试：

```powershell
dotnet restore QosServicePlatform.slnx --disable-parallel -m:1
dotnet build QosServicePlatform.slnx --no-restore -m:1
dotnet test QosServicePlatform.slnx --no-restore --no-build -m:1
```

## 组合调用 API

```powershell
curl.exe -X POST http://localhost:5200/api/composite `
  -H "Content-Type: application/json" `
  -d '{"categories":["weather","shipping","currency","notification"],"payload":{"city":"苏州","amount":"200"}}'
```

若要让 QoS 明确选择 SOAP 天气服务，可提高成本和低负载权重：

```json
{
  "categories": ["weather"],
  "payload": { "city": "南京" },
  "weights": {
    "availability": 0,
    "successRate": 0,
    "responseTime": 0,
    "cost": 0.5,
    "load": 0.5
  }
}
```

## 故障注入

强制快速天气服务失败：

```powershell
curl.exe -X POST http://localhost:5201/api/admin/failures/weather/fast `
  -H "Content-Type: application/json" `
  -d '{"enabled":true}'
```

恢复服务时将 `enabled` 改为 `false`。

## 技术栈

- .NET 10 / ASP.NET Core / Razor Pages
- `HttpClientFactory`
- REST/JSON、SOAP 1.1、WSDL、XML
- JSON 调用历史与内存 QoS 状态
- xUnit
- Docker Desktop、WSL 2、Docker Compose

