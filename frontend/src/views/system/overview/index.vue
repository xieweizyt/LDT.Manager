<template>
  <div class="ldt-page">
    <ElCard class="welcome-card" shadow="never"
      ><div class="welcome">
        <div>
          <ElTag effect="plain">系统管理</ElTag>
          <h1>你好，{{ user.info.userName }}</h1>
          <p>从这里管理用户、角色和访问权限。</p>
        </div>
        <div class="welcome-icon">
          <ArtSvgIcon icon="ri:shield-user-line" /> </div></div
    ></ElCard>
    <div class="overview-grid">
      <ElCard shadow="never" class="ldt-card"
        ><div class="ldt-muted">当前账号</div>
        <h3>{{ user.info.userName }}</h3>
        <p>{{ user.info.email }}</p></ElCard
      ><ElCard shadow="never" class="ldt-card"
        ><div class="ldt-muted">所属角色</div>
        <ElSpace wrap class="mt-4"
          ><ElTag v-for="role in user.info.roles" :key="role">{{ role }}</ElTag
          ><span v-if="!user.info.roles?.length">尚未分配角色</span></ElSpace
        ></ElCard
      >
    </div>
    <ElCard class="ldt-card" shadow="never"
      ><template #header><strong>常用功能</strong></template>
      <div class="action-grid">
        <button
          v-for="item in actions.filter(allowed)"
          :key="item.path"
          class="action-card"
          @click="router.push(item.path)"
        >
          <div class="action-icon"><ArtSvgIcon :icon="item.icon" /></div>
          <h3>{{ item.title }}</h3>
          <p>{{ item.description }}</p>
          <span>进入 <ArtSvgIcon icon="ri:arrow-right-line" /></span>
        </button>
      </div>
      <ElEmpty v-if="!actions.some(allowed)" description="当前账号暂无管理权限，请联系管理员授权"
    /></ElCard>
  </div>
</template>

<script setup lang="ts">
  import { useAccess } from '../shared/useAccess'
  defineOptions({ name: 'Overview' })
  const { user, can } = useAccess()
  const router = useRouter()
  const actions = [
    {
      title: '用户列表',
      description: '查看与维护系统账号',
      icon: 'ri:user-line',
      path: '/system/user',
      permission: 'AbpIdentity.Users'
    },
    {
      title: '角色列表',
      description: '维护角色与默认角色设置',
      icon: 'ri:group-line',
      path: '/system/role',
      permission: 'AbpIdentity.Roles'
    },
    {
      title: '用户授权',
      description: '分配角色和个人功能权限',
      icon: 'ri:user-settings-line',
      path: '/system/user-authorization',
      permission: 'AbpIdentity.Users',
      anyPermissions: [
        'AbpIdentity.Users.Update.ManageRoles',
        'AbpIdentity.Users.ManagePermissions'
      ]
    },
    {
      title: '角色授权',
      description: '统一配置角色功能权限',
      icon: 'ri:shield-keyhole-line',
      path: '/system/role-authorization',
      permission: 'AbpIdentity.Roles',
      anyPermissions: ['AbpIdentity.Roles.ManagePermissions']
    }
  ]
  function allowed(item: (typeof actions)[number]) {
    return can(item.permission) && (!item.anyPermissions || item.anyPermissions.some(can))
  }
</script>

<style scoped lang="scss">
  @use '../shared/admin.scss';
  .welcome-card {
    border-radius: 18px;
    background: linear-gradient(
      120deg,
      color-mix(in srgb, var(--main-color) 13%, var(--default-box-color)),
      var(--default-box-color)
    );
  }
  .welcome {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px;
  }
  .welcome h1 {
    font-size: 28px;
    font-weight: 650;
    margin: 18px 0 8px;
  }
  .welcome p {
    color: var(--art-gray-600);
  }
  .welcome-icon {
    color: var(--main-color);
    font-size: 80px;
    padding: 12px 25px;
  }
  .overview-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 18px;
  }
  .overview-grid h3 {
    font-size: 22px;
    margin: 14px 0 6px;
  }
  .overview-grid p {
    color: var(--art-gray-500);
    overflow-wrap: anywhere;
  }
  .action-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(210px, 1fr));
    gap: 18px;
  }
  .action-card {
    padding: 22px;
    text-align: left;
    border: 1px solid var(--art-gray-200);
    border-radius: 14px;
    background: var(--default-box-color);
    cursor: pointer;
    transition:
      transform 0.2s,
      border-color 0.2s;
    color: var(--art-gray-800);
  }
  .action-card:hover {
    transform: translateY(-3px);
    border-color: var(--main-color);
  }
  .action-icon {
    width: 44px;
    height: 44px;
    display: grid;
    place-items: center;
    color: var(--main-color);
    background: color-mix(in srgb, var(--main-color) 12%, transparent);
    border-radius: 12px;
    font-size: 23px;
  }
  .action-card h3 {
    font-size: 16px;
    font-weight: 650;
    margin: 18px 0 8px;
  }
  .action-card p {
    color: var(--art-gray-500);
    font-size: 13px;
  }
  .action-card span {
    display: flex;
    align-items: center;
    gap: 5px;
    margin-top: 20px;
    font-size: 13px;
    color: var(--main-color);
  }
  @media (max-width: 600px) {
    .overview-grid {
      grid-template-columns: 1fr;
    }
    .welcome-icon {
      display: none;
    }
  }
</style>
