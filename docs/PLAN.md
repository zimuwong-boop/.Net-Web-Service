# 项目实施状态与设计说明

## 1. 当前结论

项目核心范围已经完成，并通过本地进程与 Docker 容器两种方式验证。它不再只是实施计划，而是可直接演示的毕业设计复现版本。

| 阶段 | 状态 | 交付结果 |
| --- | --- | --- |
| 设计基线 | 已完成 | 范围、QoS 公式、UML、验收场景 |
| 解决方案与领域模型 | 已完成 | 5 个生产项目、2 个测试项目 |
| 模拟服务 | 已完成 | 4 类、10 个候选，支持延迟和故障模拟 |
| QoS 选择引擎 | 已完成 | 五维归一化、加权评分、稳定排名 |
| 动态调用 | 已完成 | REST 与 SOAP/WSDL 运行时调用 |
| 组合编排 | 已完成 | 1～4 类并行、部分失败和关联 ID |
| 弹性与监控 | 已完成 | 健康检查、超时、故障转移、熔断 |
| Web 与 API | 已完成 | Razor 控制台、组合调用 API、调用历史 |
| 容器化交付 | 已完成 | Dockerfile、Compose、命名卷、一键脚本 |
| 自动化验证 | 已完成 | 编译、单元/集成测试、REST/SOAP 端到端验证 |

## 2. 系统范围

| 服务类别 | 候选提供者 | 协议 | 示例输入与输出 |
| --- | ---: | --- | --- |
| 天气查询 | 3 | REST × 2、SOAP × 1 | 城市 → 温度和天气状况 |
| 物流报价 | 3 | REST | 起点、终点、重量 → 价格和时效 |
| 汇率换算 | 2 | REST | 币种、金额 → 换算金额 |
| 消息通知 | 2 | REST | 收件人、内容 → 发送结果 |

所有候选都是可控的模拟服务，不依赖第三方账号。Provider 可模拟不同延迟、错误率、成本和负载，使 QoS 决策可观察、可重复。

## 3. QoS 模型

### 3.1 指标与默认权重

| 指标 | 默认权重 | 趋势 |
| --- | ---: | --- |
| Availability | 35% | 越高越好 |
| SuccessRate | 25% | 越高越好 |
| ResponseTime | 20% | 越低越好 |
| Cost | 10% | 越低越好 |
| Load | 10% | 越低越好 |

```text
Score =
    0.35 × Availability
  + 0.25 × SuccessRate
  + 0.20 × ResponseSpeed
  + 0.10 × CostEfficiency
  + 0.10 × LoadCapacity
```

所有指标先归一化到 `[0, 1]`，低值优先项反向计分。页面和 API 都允许自定义权重，但总和必须为 1（页面显示为 100%）。

### 3.2 选择与保护流程

1. 从注册表取得服务类别的全部候选端点。
2. 过滤禁用、健康检查失败和正在熔断的候选。
3. 读取内存 QoS 快照并完成归一化评分。
4. 按综合分数降序、名称升序稳定排名。
5. 根据端点协议选择 REST 或 SOAP 调用路径。
6. 成功时记录延迟和成功状态；失败时记录指标并尝试下一名。
7. 同一端点连续失败 2 次后熔断 20 秒。
8. 单类全部失败时返回结构化失败，不阻止其他类别完成。
9. 聚合结果写入最近 100 次调用历史。

## 4. SOAP/WSDL 动态调用

天气类别中的“经典 SOAP 天气”提供：

- SOAP 1.1 服务：`/soap/legacy-weather`
- WSDL：`/soap/legacy-weather?wsdl`
- 操作：`QueryWeather`
- SOAP Action：`http://qos-demo.local/weather/QueryWeather`

动态客户端首次调用时下载并解析 WSDL，确认 `QueryWeather` 操作存在，然后根据注册表中的地址和 SOAP Action 生成 XML Envelope。它不使用 Visual Studio 生成的静态代理，因此端点协议和地址仍由运行时注册信息决定。

## 5. 解决方案结构

```text
QosServicePlatform.slnx
├─ src/
│  ├─ QosServicePlatform.Domain/          # 实体、QoS 与调用结果模型
│  ├─ QosServicePlatform.Application/     # 评分、编排和抽象接口
│  ├─ QosServicePlatform.Infrastructure/  # 注册表、REST/SOAP、健康、熔断、历史
│  ├─ QosServicePlatform.Web/             # Razor 页面、聚合 API
│  └─ QosServicePlatform.MockProviders/   # REST 与 SOAP 模拟提供者
├─ tests/
│  ├─ QosServicePlatform.UnitTests/
│  └─ QosServicePlatform.IntegrationTests/
├─ scripts/                               # CMD/PowerShell 启停脚本
├─ docs/
├─ docker-compose.yml
└─ NuGet.Config
```

## 6. 部署状态

Docker Compose 创建：

- `providers` 容器：对外端口 `5201`，承载 REST 和 SOAP/WSDL。
- `web` 容器：对外端口 `5200`，承载 UI、调度中心和组合 API。
- `qos-history` 命名卷：保存 `App_Data/call-history.json`。
- Compose 内部网络：Web 通过 `http://providers:8080` 动态调用 Provider。

本机已经完成 WSL 2、Docker Desktop、镜像构建和容器端到端验证。

## 7. 已验证场景

1. 默认权重下选择综合评分最高候选。
2. 提高速度权重后优先选择快速服务。
3. 提高成本与低负载权重后选中 SOAP 天气服务。
4. 最高分候选失败后自动调用下一名。
5. 连续失败触发熔断，熔断端点暂时退出候选池。
6. Provider 离线后健康检查将其过滤，恢复后重新可用。
7. 天气、物流、汇率、通知四类并行调用成功。
8. WSDL 下载、操作发现、SOAP 请求和响应解析成功。
9. 调用历史写入 JSON，并在容器命名卷中持久保存。
10. Web、Provider、WSDL 和组合 API 均通过容器端口访问。

## 8. 完成定义

- 四个服务类别和 10 个候选均可运行。
- 支持一次选择 1～4 类并行调用。
- QoS 选择结果可解释且可重复测试。
- REST 与 SOAP 候选共用同一套注册、评分和编排流程。
- 单候选失败可故障转移，单类失败不拖垮整个组合请求。
- 页面展示排名、尝试记录、调用结果、历史、健康和熔断状态。
- Docker Compose 可从新环境构建并启动系统。
- 编译无错误、无警告，现有自动化测试全部通过。

## 9. 未实现的可选增强

这些内容不影响当前毕业设计演示：

- SQLite/EF Core 替换 JSON 历史存储。
- 响应时间和成功率趋势图表。
- OpenTelemetry 分布式追踪。
- 登录、权限与真实第三方服务凭据管理。
- 将单个候选进一步拆分为独立容器。
- 保存多套可命名的 QoS 权重策略。

