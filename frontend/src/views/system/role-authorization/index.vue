<template>
  <div class="ldt-page">
    <div class="ldt-heading">
      <div>
        <h2>角色授权</h2>
        <p>为角色设置功能权限，角色成员会继承这些授权</p>
      </div>
    </div>
    <ElCard class="ldt-card" shadow="never"
      ><div class="ldt-selector">
        <span>选择角色</span
        ><ElSelect
          v-model="target"
          filterable
          remote
          :remote-method="searchRoles"
          :loading="loading"
          placeholder="输入角色名称查找"
          ><ElOption
            v-for="role in roles"
            :key="role.id"
            :label="role.name"
            :value="role.name" /></ElSelect></div
    ></ElCard>
    <PermissionEditor v-if="target" provider="R" :target="target" @saved="saved" />
    <ElCard v-else class="ldt-card" shadow="never"
      ><ElEmpty description="先选择一个角色，再配置授权"
    /></ElCard>
  </div>
</template>

<script setup lang="ts">
  import { identity, type Role } from '@/api/identity'
  import PermissionEditor from '../shared/PermissionEditor.vue'
  import { useAccess } from '../shared/useAccess'
  defineOptions({ name: 'RoleAuthorization' })
  const { refresh, user } = useAccess()
  const route = useRoute()
  const roles = ref<Role[]>([]),
    target = ref(''),
    loading = ref(false)
  let generation = 0
  async function searchRoles(filter = '') {
    const current = ++generation
    loading.value = true
    try {
      const result = await identity.roles(filter, 1, 50)
      if (current === generation) roles.value = result.items
    } catch {
      /* The API reports failures. */
    } finally {
      if (current === generation) loading.value = false
    }
  }
  async function saved() {
    await refresh()
    if (user.info.roles?.includes(target.value)) window.location.reload()
  }
  onMounted(async () => {
    const name = typeof route.query.name === 'string' ? route.query.name : ''
    await searchRoles(name)
    if (roles.value.some((role) => role.name === name)) target.value = name
  })
</script>

<style scoped lang="scss">
  @use '../shared/admin.scss';
</style>
