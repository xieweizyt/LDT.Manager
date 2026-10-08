# LDT 管理系统

源码目录：`D:\临时文件\ldt`。前端基于 Art Design Pro，后端复用 ABP-MicroService 的 BaseService、ABP Identity 和 IdentityServer4。两个应用独立构建、独立启动，通过 HTTP API 通信。

```text
ldt/
  frontend/             Vue 3、TypeScript、Element Plus、Art 布局与主题
  backend/Ldt.Host/     当前系统唯一后端启动项目，端口 5000
  backend/Ldt.sln       当前系统的 Visual Studio 解决方案
  backend/BaseService/ 上游管理模块源码
  backend/AuthServer/  上游认证模块源码，供对照和扩展
  scripts/             前后端独立启动、构建和接口验证
  deploy/              正式环境配置、容器与 TLS 代理示例
  docs/                本次验证结果和页面截图
  upstream.json        两个来源仓库及复制时的提交版本
```

上游后端的 `WebApp` 前端没有复制。当前后端将认证与管理模块装配到一个 ABP API 进程，不需要启动上游的多套网关和业务微服务。前端页面仍使用 Art 的菜单、标签页、主题和基础组件。

| 功能 | 已连接的真实能力 |
| --- | --- |
| 用户列表 | 查询、分页、新增、编辑、删除、启用与停用 |
| 角色列表 | 查询、分页、新增、编辑、删除、默认角色设置 |
| 用户授权 | 分配角色、保存个人直接权限、显示角色继承权限 |
| 角色授权 | 读取和保存角色功能权限 |

后端使用 SQL Server 和 EF Core Code First。登录沿用上游的 OAuth2 / IdentityServer4 公共客户端密码模式，SPA 没有客户端密钥。访问令牌有效期 15 分钟，刷新令牌有效期 8 小时并轮换；退出会撤销刷新令牌。令牌保存在当前标签页的 sessionStorage，不保存账号密码。每次 API 请求会检查账号状态并读取当前角色，撤销角色和停用账号会影响已签发令牌。权限由后端检查，前端按有效权限显示菜单和按钮。

个人直接权限与角色权限相加。取消个人直接授权不会撤销从角色继承的权限；要取消继承权限，请调整所属角色或角色授权。

用户角色分配单独检查 `AbpIdentity.Users.Update.ManageRoles`；编辑用户只检查 `AbpIdentity.Users.Update`。已替换上游原来用“编辑用户”权限分配角色的写入接口，避免两项权限混用。

1. 本地运行需要 PowerShell 7、.NET 8 SDK 或更新 SDK、Node 20.19+ 或 22.12+、pnpm 11、SQL Server LocalDB。脚本在本机 Node 版本较旧时会尝试使用 Codex 自带的 Node；也支持安装符合版本要求的 Node。

   ```powershell
   cd D:\临时文件\ldt
   .\scripts\Initialize-Local.ps1
   .\scripts\Start-Backend.ps1
   ```

   在另一个 PowerShell 7 终端启动前端：

   ```powershell
   cd D:\临时文件\ldt
   .\scripts\Start-Frontend.ps1
   ```

   此电脑上的依赖已安装，可以直接分别运行两个启动脚本。首次启动会创建 **LdtAdmin** 数据库、执行迁移并初始化管理员。用户与角色列表初始只含管理员，其他内容从数据库读取。

   - 前端：<http://localhost:8080>
   - 后端：<http://localhost:5000>
   - 开发接口文档：<http://localhost:5000/swagger>
   - 本地账号：`admin`
   - 本地密码：`Ldt.Local2026!`

   在各自终端按 `Ctrl+C` 停止对应服务。8080 被占用时前端会报错，不会自动改成其他端口；后端入口固定监听 5000。

2. 配置按环境分别保存：

   | 环境 | 前端 | 后端 |
   | --- | --- | --- |
   | 本地 | `.env`、`.env.development` | `appsettings.json`、`appsettings.Development.json` |
   | 正式 | `.env`、`.env.production`，可用 `.env.production.local` 覆盖 | `appsettings.json`、`appsettings.Production.json` 和环境变量 |

   本地前端将 `/api`、`/connect` 请求转发到 5000。需要使用已有开发 SQL Server 时，设置 `ConnectionStrings__Default` 后再启动。环境变量优先于配置文件。

3. 正式环境先准备实际 SQL Server、前后端 HTTPS 域名、IdentityServer 签名 PFX 证书及密码，并填写配置模板。正式模式不自动迁移，也不会使用本地管理员密码或开发签名密钥。

   ```powershell
   Copy-Item .\frontend\.env.production.local.example .\frontend\.env.production.local
   Copy-Item .\deploy\.env.production.example .\deploy\.env.production
   # 编辑两个文件，填写实际域名、数据库、管理员密码和签名证书路径。
   .\scripts\Build-Production.ps1
   .\scripts\Start-Backend.ps1 -Environment Production -EnvFile .\deploy\.env.production -MigrateOnly
   .\scripts\Start-Backend.ps1 -Environment Production -EnvFile .\deploy\.env.production
   ```

   前端产物在 `frontend/dist`，后端产物在 `artifacts/backend`。静态服务器使用 `frontend/deploy/nginx.conf`，监听 8080。TLS 网关示例为 `deploy/nginx-tls.example.conf`，分别将前端域名转到 8080、API 域名转到 5000。后端必须配置实际可信代理 IP，才能正确识别代理转发的 HTTPS。

   首次初始化密码只用于创建管理员；后续迁移不重置已保存的账号、角色或权限。正式启动要求提供真实数据库连接和签名证书，Swagger 仅在开发环境启用。

   可选的容器启动流程如下，使用外部 SQL Server，不在部署文件中写入数据库密码：

   ```powershell
   docker compose --env-file .\deploy\.env.production -f .\deploy\compose.production.yml build
   docker compose --env-file .\deploy\.env.production -f .\deploy\compose.production.yml run --rm backend --migrate
   docker compose --env-file .\deploy\.env.production -f .\deploy\compose.production.yml up -d
   ```

   将签名证书放到 `deploy/certificates/identity-signing.pfx`。本地密码、正式环境密钥文件、运行日志、进程记录和构建产物均已设置忽略规则。

4. 验证脚本仅允许连接 `localhost:5000` 的 Development 服务，会创建专用测试账号与角色，并在结束时删除它们。报告不包含密码和令牌。

   ```powershell
   .\scripts\Test-Api.ps1
   ```

   最后一次运行的 40 项检查全部通过，涵盖真实登录、列表读取、用户/角色操作、授权保存、撤销授权、越权拒绝、停用账号、刷新令牌与退出撤销。结果保存在 `docs/api-verification.json`。浏览器检查和正式部署的实际验证范围见 [验证记录](docs/verification.md)。

来源：[Art Design Pro](https://github.com/Daymychen/art-design-pro)、[ABP-MicroService](https://github.com/WilliamXu96/ABP-MicroService)。保留 Art 的 MIT LICENSE 和上游源码说明。IdentityServer4 是上游保留的旧认证组件，正式上线前应评估其维护与安全更新情况；本次未升级到另一套认证产品。
