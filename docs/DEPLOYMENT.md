# 容器化部署

本项目已在 Windows、WSL 2、Docker Desktop 29.8.0 和 Docker Compose 5.5.1 环境完成实际构建与端到端验证。

项目提供两个 Linux 容器：

- `providers`：REST 模拟服务和 SOAP/WSDL 服务，对外端口 `5201`。
- `web`：QoS 调度器、组合 API 和 Razor 页面，对外端口 `5200`。

调用历史保存在 Docker 命名卷 `qos-history` 中，重建容器不会丢失。

## 前置条件

Windows 上需要启用 WSL 2，并安装、启动 Docker Desktop。安装这些系统组件通常需要管理员权限和一次重启。当前开发机已经满足这些条件；新机器部署时才需要重新安装。

## 一键启动

```powershell
.\scripts\start-demo.cmd
```

推荐使用 `.cmd` 入口，因为它不受 Windows PowerShell 脚本执行策略影响，并会在需要时启动 Docker Desktop、等待引擎就绪。

启动完成后访问：

- Web 控制台：`http://localhost:5200`
- 健康检查：`http://localhost:5200/health`
- SOAP WSDL：`http://localhost:5201/soap/legacy-weather?wsdl`

查看日志：

```powershell
docker compose logs -f
```

查看容器状态：

```powershell
docker compose ps
```

停止环境：

```powershell
.\scripts\stop-demo.cmd
```

彻底删除容器和持久化调用历史：

```powershell
docker compose down --volumes
```

最后一条命令会删除 `qos-history` 卷中的历史数据，请仅在确认不再需要数据时使用。
