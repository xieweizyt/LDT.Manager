# 本次交付验证

验证日期：2026-10-06。源码位于 `D:\临时文件\ldt`。

| 项目 | 结果与证据 |
| --- | --- |
| 前端来源 | Art Design Pro，提交 `f3aaf58eec1a0e988f162352c33862327a484f95` |
| 后端来源 | ABP-MicroService，提交 `2b261c9185ddb4536ee406d5b43ada4f3774c71b` |
| 后端裁剪 | 工作区未复制上游 WebApp；后端目录内没有 Vue 页面文件 |
| 技术与运行 | Vue 3、Element Plus、.NET 8、ABP 8.1.1、EF Core、IdentityServer4、SQL Server LocalDB |
| 独立启动 | 用 `Start-Backend.ps1` 和 `Start-Frontend.ps1` 分别启动；API 在 5000，页面在 8080 |
| 数据库 | EF Code First 的 `InitialLdt` 迁移已应用到真实 `LdtAdmin` 数据库，初始化管理员与 admin 角色 |
| 类型与构建 | `vue-tsc --noEmit` 和最终 `build:prod` 通过；后端 Release 编译通过，0 警告、0 错误 |
| 独立发布 | `frontend/dist/index.html` 和 `artifacts/backend/Ldt.Host.dll` 已生成 |
| 前端转发 | 通过 8080 请求 `/connect/token` 登录并调用 `/api/ldt/session`，返回 HTTP 200 |
| 真实接口测试 | `Test-Api.ps1` 最后运行的 40 项检查全部通过，见 [JSON 报告](api-verification.json) |
| 正式配置校验 | Compose 配置校验通过；正式模式在缺少签名证书时拒绝启动 |

接口测试覆盖用户、角色新增/编辑/删除，个人直接授权，角色授权，角色成员分配，继承权限来源，授权即时生效与撤销，账号停用，未登录和越权拒绝，以及刷新令牌轮换和退出撤销。每轮生成独立的临时测试账号与角色，完成后删除；最终报告不包含密码与令牌。

“编辑用户”和“分配角色”分别检查实际 ABP 权限。角色分配使用 `AbpIdentity.Users.Update.ManageRoles`，原上游只检查用户编辑权限的角色写入接口已移除。测试确认：只有编辑权限不能分配角色，角色分配权限可以单独授予，也不会带来权限编辑器访问权限。

浏览器在 1280×720 下检查了工作台、用户列表、角色列表、用户授权和角色授权，页面读取真实数据库数据。用户授权显示 admin 角色及继承权限来源；角色授权显示 11 项已授权功能。新增用户的空表单提交显示必填错误。浏览器检查以读取和表单校验为主，数据写入与授权保存通过上述真实 API 测试验证。

截图：[工作台](screenshots/overview.jpg)、[用户列表](screenshots/users.jpg)、[角色列表](screenshots/roles.jpg)、[用户授权](screenshots/user-authorization.jpg)、[角色授权](screenshots/role-authorization.jpg)。构建日志为 `frontend-build.log` 与 `production-build.log`。

正式环境尚未部署：真实 SQL Server 连接、前后端域名和签名证书需要在配置模板中填写。本机 Docker 守护进程未运行，因此只验证了 Compose 配置解析，没有验证镜像构建及容器启动。正式环境的公网 TLS、数据库连通性和证书链需要在实际部署环境验证。
