# nursing-backend-services

养老护理平台 backend 工程，面向 admin、family、nani 三端提供统一的 gateway、BFF、领域服务和 SaaS 多租户基础能力。

## 定位

- architecture style: edge + domain microservices + shared building blocks
- runtime: .NET 10
- primary goals: SaaS ready, workflow ready, tenant aware, audit friendly

## 当前工程结构

```text
src/
  BuildingBlocks/
    NursingBackend.BuildingBlocks/
  Gateway/
    NursingBackend.ApiGateway/
  Bff/
    NursingBackend.Bff.Admin/
    NursingBackend.Bff.Family/
    NursingBackend.Bff.Nani/
  Services/
    NursingBackend.Services.Identity/
    NursingBackend.Services.Tenant/
    NursingBackend.Services.Elder/
    NursingBackend.Services.Care/
    NursingBackend.Services.Health/
    NursingBackend.Services.Visit/
    NursingBackend.Services.Staffing/
    NursingBackend.Services.Operations/
    NursingBackend.Services.Billing/
    NursingBackend.Services.Notification/
    NursingBackend.Services.AiOrchestration/
tests/
  NursingBackend.ArchitectureTests/
deploy/
  compose.infrastructure.yml
  k8s/
```

## 服务设计摘要

| 服务 | 作用 |
| --- | --- |
| ApiGateway | 统一入口、租户解析、认证透传、流量治理 |
| Bff.Admin | Admin 聚合查询、审批视图和运营 DTO |
| Bff.Family | 家属视图裁剪、探视/账单/消息聚合 |
| Bff.Nani | 班次任务、报警响应、移动执行上下文 |
| Identity | 用户、角色、会话、设备登录 |
| Tenant | 租户、套餐、特性开关、租户配置 |
| Elder | 老人档案、入住退住、家属绑定 |
| Care | 护理计划、任务、执行、交接班 |
| Health | 生命体征、健康档案、异常输入 |
| Visit | 探视预约、审批、签到、视频协同元数据 |
| Staffing | 员工档案、入职、班次、排班 |
| Operations | 机构、房间、设备、物资、报警与运营事件 |
| Billing | 套餐、账单、支付、欠费 |
| Notification | 站内信、短信、Push、模板 |
| AiOrchestration | AI 评估、摘要、解释、审计关联 |

## 本地命令

```bash
docker compose -f docker-compose-infras.yml up -d postgres redis rabbitmq keycloak seq
dotnet run --project src/Tools/NursingBackend.DatabaseMigrator/NursingBackend.DatabaseMigrator.csproj
dotnet run --project src/Tools/NursingBackend.DatabaseSeeder/NursingBackend.DatabaseSeeder.csproj
dotnet run --project src/Tools/NursingBackend.DeadLetterReplay/NursingBackend.DeadLetterReplay.csproj -- --dry-run --limit 20
dotnet build nursing-backend-services.slnx
dotnet test nursing-backend-services.slnx
```

本地基础设施入口:

- backend 根目录提供 `docker-compose-infras.yml`，作为本地 PostgreSQL、Redis、RabbitMQ、Keycloak、Seq 的统一启动入口。
- Postgres 会自动挂载 `deploy/postgres/init/01-create-service-databases.sh`，首次初始化时创建 `nursing_elder`、`nursing_health`、`nursing_care`、`nursing_visit`、`nursing_billing`、`nursing_notification`、`nursing_operations`、`nursing_organizations`、`nursing_rooms`、`nursing_staffing`、`nursing_config`、`nursing_ai`。
- 若本地复用了旧的 Postgres volume，`NursingBackend.DatabaseMigrator` 也会在迁移前补建缺失的 service database，不要求先删卷重建。
- 旧的 `deploy/compose.infrastructure.yml` 仍保留给 deploy 目录内部资产使用，但本地开发默认优先使用根目录入口，避免相对路径混淆。
- Debug 构建默认使用 Workstation GC，避免 macOS 上一次启动多个 ASP.NET Core 服务时 Server GC 按进程放大本地内存预留；Release 构建仍保留生产默认 GC 行为。
- EventWorker 的 RabbitMQ 消费窗口由 `RabbitMq:ConsumerPrefetchCount` 控制，默认 20，避免本地队列积压时启动 worker 被 broker 一次性推送过多消息。
- 本地 `launchSettings.json` 关闭自动打开浏览器，并设置 `DOTNET_PROCESSOR_COUNT=2` 与 `MSBUILDDISABLENODEREUSE=1`，避免 IDE/CLI 一次启动多个 backend profile 时每个服务都按整机 CPU 数调度，同时避免 CLI 退出后残留 MSBuild node reuse 子进程。
- 本地 `docker-compose-infras.yml` 对 Postgres、Redis、RabbitMQ、Keycloak、Seq 设置 CPU 和内存上限，避免基础设施容器启动期与 backend 服务抢占全部 macOS CPU。

数据库初始化顺序:

- 先运行 `NursingBackend.DatabaseMigrator` 创建缺失 database 并升级 schema。
- 再运行 `NursingBackend.DatabaseSeeder` 写入 elder、organization、rooms、staffing、health、care、visit、billing、notification、operations 的联调样本。
- seeder 使用固定 `tenant-demo` 与稳定业务主键，支持重复执行，不依赖内存 seed。
- 本地通过 Identity Service `POST /api/identity/dev-login` 生成联调 token 时，`tenantId` 也必须使用 `tenant-demo`；像 `ORG-PD-01` 这样的值是机构主键，不是租户主键，用错后会因为租户隔离表现为空列表或详情 404。

本地端口基线:

- edge: gateway `5200`、family-bff `5274`、nani-bff `5213`、admin-bff `5146`
- identity and tenant: identity `5265`、tenant `5186`
- core domain: elder `5062`、care `5019`、health `5197`、visit `5050`、notification `5144`
- extended domain: staffing `5216`、rooms `5217`、organization `5218`、operations `5211`、billing `5253`、config `5290`、ai-orchestration `5267`
- broker and local credentials: RabbitMQ `5672` / management `15672`，默认开发账号 `nursing` / `nursing`
- 若本地验证出现服务健康但下游聚合 502，先检查 `ServiceEndpoints` 是否仍引用旧的 `53xx` 默认端口。

## Kubernetes 交付资产

- 基线路径: `deploy/k8s/base`
- 开发环境 overlay: `deploy/k8s/overlays/dev`
- staging overlay: `deploy/k8s/overlays/staging`
- prod overlay: `deploy/k8s/overlays/prod`
- 设计文档: `../nursing-documents/docs/architecture/backend-cloud-native-kubernetes-architecture.md`

GitOps 与发布顺序:

- namespace: sync-wave -3
- config 与 secret: sync-wave -2
- migration job: Argo CD PreSync hook, sync-wave -1
- 应用 workloads: 默认 wave 0
- event worker: sync-wave 1
- HPA 与 ingress: sync-wave 2

示例命令:

```bash
kubectl apply -k deploy/k8s/overlays/dev
kubectl apply -k deploy/k8s/overlays/staging
kubectl apply -k deploy/k8s/overlays/prod
kubectl get pods -n nursing-platform
```

## 当前已落地能力

- shared platform defaults 已统一接入 JWT bearer、authorization 和 tenant request context
- Elder、Health、Care、Visit、Billing、Notification 已接入 EF Core + PostgreSQL 基线持久化
- Admin 入住纵切会完成 elder/health/care 编排，并给 family 推送入住通知
- Care 与 Visit 已具备 outbox -> notification 的事件派发能力，支持 inline dispatch 和手动补派发
- Billing 已具备 invoice issue -> outbox -> notification 链路，Notification 失败时可回调 Billing 创建补偿记录并把账单切到 ActionRequired
- 已提供 Kustomize 版 Kubernetes 部署清单，覆盖 gateway、3 个 BFF 和核心/扩展领域服务
- 已提供数据库迁移 console 工程与 Kubernetes Job，可在服务启动前完成 schema 升级
- 已提供 RabbitMQ 驱动的事件 worker 工程与 Kubernetes Deployment，用于 outbox -> broker -> notification 异步链路
- event worker 已补充 retry queue、dead-letter queue 与 backlog metrics 基线
- Notification 已补充 delivery attempts 审计表与 observability summary，Billing 已补充 open compensation / failed notification / overdue invoice 汇总查询
- shared platform defaults 已接入 OTLP exporter、AspNetCore/HttpClient/Runtime instrumentation，Billing 与 Notification 已补充自定义 metrics 与 ActivitySource
- k8s base 已补充真实 OpenTelemetry Collector、ServiceMonitor 与 PrometheusRule，用于 notification delivery failure、compensation callback failure、provider signature failure 和 billing compensation spike 告警
- prod overlay 已切入 ExternalSecret 与 ExternalName 外部依赖接入模型，避免在 Git 中保留生产密钥
- staging/prod overlay 已切换到 `patches` 新语法，kustomize render 不再依赖已弃用的 `patchesStrategicMerge`

## 事件派发验证

```bash
# 触发护理计划待派发事件补派发
curl -X POST http://localhost:5311/api/care/outbox/dispatch

# 触发探视预约待派发事件补派发
curl -X POST http://localhost:5313/api/visits/outbox/dispatch
```

## Dead-Letter Replay

当 event worker 已将消息投递到 dead-letter queue 后，可使用独立工具做受控重放。

```bash
# 仅预览，不会重新发布，也不会删除死信消息
dotnet run --project src/Tools/NursingBackend.DeadLetterReplay/NursingBackend.DeadLetterReplay.csproj -- --dry-run --limit 20

# 重新发布到主 exchange，并从 dead-letter queue 删除原始消息
dotnet run --project src/Tools/NursingBackend.DeadLetterReplay/NursingBackend.DeadLetterReplay.csproj -- --execute --limit 20

# 重新发布但保留原始死信，适合先做影子验证
dotnet run --project src/Tools/NursingBackend.DeadLetterReplay/NursingBackend.DeadLetterReplay.csproj -- --execute --keep-source --limit 5
```

工具行为:

- 默认使用 `RabbitMq` 配置段连接 broker，并复用 worker 的 exchange/queue 命名
- replay 时会重置 `x-retry-count` 头，避免消息回放后因历史重试次数立即再次进入 dead-letter
- `--keep-source` 会把原始死信重新放回队列，仅建议在短时间人工观察窗口内使用

## Billing / Notification 补偿入口

```bash
# 创建账单并进入通知 outbox
curl -X POST http://localhost:5253/api/billing/invoices \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer <token>' \
  -H 'X-Tenant-Id: tenant-demo' \
  -d '{"elderId":"ELD-1","elderName":"王秀兰","packageName":"基础护理套餐","amount":3999.50,"dueAtUtc":"2026-04-05T09:00:00Z"}'

# 手动上报通知投递失败，Notification 会触发 Billing 补偿
curl -X POST http://localhost:5144/api/notifications/<notificationId>/delivery-result \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer <token>' \
  -H 'X-Tenant-Id: tenant-demo' \
  -d '{"status":"Failed","channel":"sms","failureCode":"provider-timeout","failureReason":"sms provider timeout"}'

# 观察 Billing 补偿与 Notification 审计摘要
curl -H 'Authorization: Bearer <token>' -H 'X-Tenant-Id: tenant-demo' http://localhost:5253/api/billing/observability
curl -H 'Authorization: Bearer <token>' -H 'X-Tenant-Id: tenant-demo' http://localhost:5144/api/notifications/observability

# 模拟供应商 webhook 回调，不走用户鉴权，改用共享 webhook key
curl -X POST http://localhost:5144/api/provider-callbacks/notifications \
  -H 'Content-Type: application/json' \
  -H 'X-Provider-Webhook-Key: development-provider-webhook-key' \
  -d '{"provider":"twilio","channel":"sms","status":"Failed","notificationId":"<notificationId>","providerMessageId":"twilio-msg-1","failureCode":"provider-timeout","failureReason":"sms provider timeout"}'

# 使用 HMAC-SHA256 签名的 provider callback，timestamp.rawBody 作为签名原文
body='{"provider":"twilio","channel":"sms","status":"Delivered","notificationId":"<notificationId>","providerMessageId":"twilio-msg-2"}'
timestamp=$(date +%s)
signature=$(printf '%s.%s' "$timestamp" "$body" | openssl dgst -sha256 -hmac 'replace-twilio-signature-secret' -binary | xxd -p -c 256)
curl -X POST http://localhost:5144/api/provider-callbacks/notifications \
  -H 'Content-Type: application/json' \
  -H "X-Twilio-Timestamp: $timestamp" \
  -H "X-Twilio-Signature: $signature" \
  -d "$body"
```

provider callback 当前已改成配置驱动，用户需要填写的关键配置位如下：

- `ProviderCallbacks:SharedKey`：共享 webhook key 回退值
- `ProviderCallbacks:SharedKeyHeaderName`：共享 key 请求头名
- `ProviderCallbacks:DefaultProfile:*`：默认 provider 的签名算法、编码、原文模式、头名、时间窗和 secret
- `ProviderCallbacks:Profiles:*`：供应商级 profile，例如 `twilio` 或 `email-hmac-base64` 的签名头、编码、prefix、状态映射、channel 覆盖

如果要替换为真实供应商签名模型，优先修改：

- [nursing-backend-services/src/Services/NursingBackend.Services.Notification/appsettings.json](nursing-backend-services/src/Services/NursingBackend.Services.Notification/appsettings.json)
- [nursing-backend-services/src/Services/NursingBackend.Services.Notification/appsettings.Development.json](nursing-backend-services/src/Services/NursingBackend.Services.Notification/appsettings.Development.json)
- [nursing-backend-services/deploy/k8s/base/configmap.yaml](nursing-backend-services/deploy/k8s/base/configmap.yaml)
- [nursing-backend-services/deploy/k8s/base/secret.yaml](nursing-backend-services/deploy/k8s/base/secret.yaml)

当前 profile 已额外支持以下常见差异，不需要再改业务代码：

- `SignatureEncoding`：支持 `HexLower`、`HexUpper`、`Base64`
- `SignaturePayloadMode`：支持 `TimestampDotBody`、`TimestampBody`、`Body`
- `SignaturePrefix`：支持去掉类似 `sha256=` 的签名前缀再比对

Kubernetes 环境下，provider profile 的非敏感参数已经预留在 ConfigMap，敏感 secret 已拆到 Secret。需要注意：

- `ProviderCallbacks__Profiles__0__*`、`ProviderCallbacks__Profiles__1__*` 这样的数组下标要和 appsettings 中的 profile 顺序保持一致
- 如果新增第三个以上 provider，除了改 appsettings，也要同步补充对应的 ConfigMap/Secret 环境变量
- 旧的 `ProviderCallbacks__SignatureSecret` 占位已经失效，当前以 `DefaultProfile` 和 `Profiles__{index}` 级别的 secret 为准

## Telemetry 与告警

```bash
# 本地指定 OTLP collector
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
dotnet run --project src/Services/NursingBackend.Services.Notification/NursingBackend.Services.Notification.csproj

# 查看 k8s base 中的 PrometheusRule
kubectl get prometheusrule nursing-platform-alerts -n nursing-platform -o yaml

# 查看仓库内置的 OTel Collector 与 ServiceMonitor
kubectl get deploy nursing-otel-collector -n nursing-platform
kubectl get servicemonitor nursing-otel-collector -n nursing-platform -o yaml
```

监控链路当前也改成了配置驱动，用户需要填写的关键配置位如下：

- `OTEL_UPSTREAM_OTLP_ENDPOINT`：collector 上游导出目标
- `OTEL_UPSTREAM_OTLP_AUTHORIZATION`：collector 上游鉴权头
- `OTEL_COLLECTOR_DEBUG_VERBOSITY`：debug exporter 级别
- `Monitoring__DashboardUrl`：真实 dashboard 地址占位
- `Monitoring__AlertRouteName`：真实 alert route 名称占位

对应文件：

- [nursing-backend-services/deploy/k8s/base/configmap.yaml](nursing-backend-services/deploy/k8s/base/configmap.yaml)
- [nursing-backend-services/deploy/k8s/base/secret.yaml](nursing-backend-services/deploy/k8s/base/secret.yaml)
- [nursing-backend-services/deploy/k8s/base/otel-collector-config.yaml](nursing-backend-services/deploy/k8s/base/otel-collector-config.yaml)
- [nursing-documents/docs/operations/backend-provider-callback-observability-configuration.md](../nursing-documents/docs/operations/backend-provider-callback-observability-configuration.md)

当前已对外暴露的关键指标包括:

- `nursing_notification_delivery_failed_total`
- `nursing_notification_compensation_request_failed_total`
- `nursing_notification_provider_signature_failures_total`
- `nursing_notification_provider_callback_duplicates_total`
- `nursing_billing_compensations_created_total`
- `nursing_billing_invoices_issued_total`

## 文档来源

平台级 backend 设计文档统一维护在 nursing-documents：

- ../nursing-documents/docs/architecture/frontend-backend-input-analysis.md
- ../nursing-documents/docs/architecture/backend-saas-microservice-architecture.md
- ../nursing-documents/docs/architecture/backend-service-domain-design.md
- ../nursing-documents/docs/architecture/backend-dataflow-workflows.md
- ../nursing-documents/docs/architecture/backend-tech-selection-dotnet-vs-spring.md

## 下一步建议

1. 将 prod overlay 中的 ExternalSecret 与 ExternalName 占位值替换为真实云资源绑定。
2. 为 dead-letter replay 增加审计日志或批次记录，便于事后追踪补投递。
3. 在现有配置驱动 profile 基础上，为不同供应商补齐专用签名格式、时间窗和事件语义映射。
