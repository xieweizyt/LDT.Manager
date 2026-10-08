<template>
  <div class="ldt-page">
    <div class="ldt-heading">
      <div>
        <h2>用户授权</h2>
        <p>为用户分配角色，也可以单独授予功能权限</p>
      </div>
    </div>
    <ElCard class="ldt-card" shadow="never"
      ><div class="ldt-selector">
        <span>选择用户</span
        ><ElSelect
          v-model="target"
          filterable
          remote
          :remote-method="searchUsers"
          :loading="searching"
          placeholder="输入用户名或邮箱查找"
          :disabled="savingRoles"
          ><ElOption
            v-for="item in users"
            :key="item.id"
            :label="`${item.userName} · ${item.email}`"
            :value="item.id" /></ElSelect></div
    ></ElCard>
    <template v-if="target">
      <ElCard class="ldt-card" shadow="never" v-if="can('AbpIdentity.Users.Update.ManageRoles')">
        <template #header
          ><div>
            <strong>所属角色</strong>
            <p class="ldt-muted mt-2"> 用户拥有所选角色的权限，取消角色后会移除该角色带来的授权 </p>
          </div></template
        >
        <div v-loading="loadingRoles">
          <ElAlert
            v-if="rolesFailed"
            title="角色加载失败，请重新加载"
            type="error"
            :closable="false"
          />
          <ElSelect
            v-else
            v-model="selectedRoles"
            multiple
            filterable
            placeholder="请选择角色"
            style="width: 100%"
            :disabled="savingRoles"
            ><ElOption v-for="role in roles" :key="role.id" :label="role.name" :value="role.name"
          /></ElSelect>
          <div class="flex justify-end mt-5">
            <ElButton v-if="rolesFailed" @click="loadRoles">重新加载</ElButton
            ><ElButton
              v-else
              type="primary"
              :loading="savingRoles"
              :disabled="loadingRoles || !rolesDirty"
              @click="saveRoles"
              >保存角色分配</ElButton
            >
          </div>
        </div>
      </ElCard>
      <PermissionEditor
        v-if="can('AbpIdentity.Users.ManagePermissions')"
        :key="`${target}:${permissionVersion}`"
        provider="U"
        :target="target"
        @saved="authorizationSaved"
      />
    </template>
    <ElCard v-else class="ldt-card" shadow="never"
      ><ElEmpty description="先选择一个用户，再配置授权"
    /></ElCard>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage } from 'element-plus'
  import { identity, type User, type Role } from '@/api/identity'
  import PermissionEditor from '../shared/PermissionEditor.vue'
  import { useAccess } from '../shared/useAccess'
  defineOptions({ name: 'UserAuthorization' })
  const { can, refresh, user } = useAccess()
  const route = useRoute()
  const users = ref<User[]>([]),
    roles = ref<Role[]>([]),
    target = ref('')
  const selectedRoles = ref<string[]>([]),
    originalRoles = ref<string[]>([])
  const searching = ref(false),
    loadingRoles = ref(false),
    savingRoles = ref(false),
    rolesFailed = ref(false)
  const permissionVersion = ref(0)
  let searchGeneration = 0,
    roleGeneration = 0
  const rolesDirty = computed(
    () =>
      selectedRoles.value.length !== originalRoles.value.length ||
      selectedRoles.value.some((role) => !originalRoles.value.includes(role))
  )
  async function searchUsers(filter = '') {
    const generation = ++searchGeneration
    searching.value = true
    try {
      const result = await identity.users(filter, 1, 50)
      if (generation === searchGeneration) users.value = result.items
    } catch {
      /* Retain the selected identity while showing the API error. */
    } finally {
      if (generation === searchGeneration) searching.value = false
    }
  }
  async function loadRoles() {
    const generation = ++roleGeneration
    selectedRoles.value = []
    originalRoles.value = []
    rolesFailed.value = false
    if (!target.value || !can('AbpIdentity.Users.Update.ManageRoles')) return
    loadingRoles.value = true
    try {
      const [available, assigned] = await Promise.all([
        identity.assignableRoles(),
        identity.userRoles(target.value)
      ])
      if (generation !== roleGeneration) return
      roles.value = available.items
      selectedRoles.value = assigned.items.map((role) => role.name)
      originalRoles.value = [...selectedRoles.value]
    } catch {
      if (generation === roleGeneration) rolesFailed.value = true
    } finally {
      if (generation === roleGeneration) loadingRoles.value = false
    }
  }
  async function saveRoles() {
    if (savingRoles.value) return
    savingRoles.value = true
    try {
      await identity.setUserRoles(target.value, selectedRoles.value)
      originalRoles.value = [...selectedRoles.value]
      permissionVersion.value++
      ElMessage.success('角色分配已保存')
      await authorizationSaved()
    } catch {
      /* Keep the selection available for retry. */
    } finally {
      savingRoles.value = false
    }
  }
  async function authorizationSaved() {
    await refresh()
    if (target.value === user.info.userId) window.location.reload()
  }
  watch(target, loadRoles)
  onMounted(async () => {
    await searchUsers()
    if (typeof route.query.id === 'string') {
      try {
        const selected = await identity.user(route.query.id)
        if (!users.value.some((item) => item.id === selected.id)) users.value.unshift(selected)
        target.value = selected.id
      } catch {
        /* Do not select an unknown user. */
      }
    }
  })
</script>

<style scoped lang="scss">
  @use '../shared/admin.scss';
</style>
