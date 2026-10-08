<template>
  <ElCard class="ldt-card" shadow="never">
    <template #header
      ><div class="ldt-heading">
        <div>
          <h2 style="font-size: 17px">
            {{ provider === 'U' ? '个人直接授权' : '角色功能权限' }}
          </h2>
          <p>
            {{
              provider === 'U'
                ? '直接授权是额外授予，取消勾选不会取消从角色继承的权限。'
                : '勾选需要授予角色的功能权限，角色成员将继承这些权限。'
            }}
          </p>
        </div>
        <ElButton :disabled="loading || saving" @click="load">重新加载</ElButton>
      </div></template
    >
    <div v-loading="loading" class="permission-body">
      <ElAlert v-if="failed" title="权限加载失败，请重新加载" type="error" :closable="false" />
      <ElEmpty v-else-if="!loading && groups.length === 0" description="暂无可配置权限" />
      <div v-for="group in groups" :key="group.name" class="permission-group">
        <div class="group-heading">
          <h3>{{ group.displayName }}</h3>
          <ElButton link type="primary" :disabled="saving" @click="toggleGroup(group)">{{
            allSelected(group) ? '取消全选' : '全选'
          }}</ElButton>
        </div>
        <div class="permission-grid">
          <div
            v-for="permission in group.permissions"
            :key="permission.name"
            class="permission-item"
            :class="{ child: !!permission.parentName }"
          >
            <ElCheckbox
              :model-value="checked(permission)"
              :disabled="saving"
              @change="(value) => toggle(permission, !!value)"
              >{{ label(permission) }}</ElCheckbox
            >
            <div v-if="inheritedRoles[permission.name]?.length" class="permission-code">
              继承自角色：{{ inheritedRoles[permission.name].join('、') }}
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class="permission-footer">
      <span class="ldt-muted"
        >已选择 {{ selected.size }} 项{{ provider === 'U' ? '直接授权' : '角色授权' }}</span
      ><ElButton
        type="primary"
        :loading="saving"
        :disabled="loading || failed || !dirty"
        @click="save"
        >保存权限</ElButton
      >
    </div>
  </ElCard>
</template>

<script setup lang="ts">
  import { ElMessage } from 'element-plus'
  import { identity, type Permission, type PermissionGroup } from '@/api/identity'
  const props = defineProps<{ provider: 'U' | 'R'; target: string }>()
  const emit = defineEmits<{ saved: [] }>()
  const groups = ref<PermissionGroup[]>([]),
    selected = ref(new Set<string>()),
    original = ref(new Set<string>())
  const inheritedRoles = ref<Record<string, string[]>>({})
  const loading = ref(false),
    saving = ref(false),
    failed = ref(false)
  let generation = 0
  const dirty = computed(
    () =>
      selected.value.size !== original.value.size ||
      [...selected.value].some((name) => !original.value.has(name))
  )
  function label(permission: Permission) {
    const all = groups.value.flatMap((group) => group.permissions)
    const names = [permission.displayName]
    let parent = all.find((item) => item.name === permission.parentName)
    while (parent) {
      names.unshift(parent.displayName)
      parent = all.find((item) => item.name === parent?.parentName)
    }
    return names.join(' / ')
  }
  function checked(permission: Permission) {
    return selected.value.has(permission.name)
  }
  async function load() {
    const ownGeneration = ++generation
    groups.value = []
    selected.value = new Set()
    original.value = new Set()
    inheritedRoles.value = {}
    failed.value = false
    if (!props.target) return
    loading.value = true
    try {
      const [result, inherited] = await Promise.all([
        identity.permissions(props.provider, props.target),
        props.provider === 'U' ? identity.inheritedPermissions(props.target) : Promise.resolve({})
      ])
      if (ownGeneration !== generation) return
      groups.value = result.groups
      inheritedRoles.value = inherited
      selected.value = new Set(
        result.groups
          .flatMap((group) => group.permissions)
          .filter((permission) =>
            permission.grantedProviders.some(
              (provider) =>
                provider.providerName === props.provider && provider.providerKey === props.target
            )
          )
          .map((permission) => permission.name)
      )
      original.value = new Set(selected.value)
    } catch {
      if (ownGeneration === generation) failed.value = true
    } finally {
      if (ownGeneration === generation) loading.value = false
    }
  }
  function toggle(permission: Permission, value: boolean) {
    const all = groups.value.flatMap((group) => group.permissions)
    if (value) {
      selected.value.add(permission.name)
      let parent = all.find((item) => item.name === permission.parentName)
      while (parent) {
        selected.value.add(parent.name)
        parent = all.find((item) => item.name === parent?.parentName)
      }
    } else {
      selected.value.delete(permission.name)
      for (const child of all.filter((item) => item.parentName === permission.name))
        toggle(child, false)
    }
  }
  function allSelected(group: PermissionGroup) {
    return group.permissions.every(checked)
  }
  function toggleGroup(group: PermissionGroup) {
    const value = !allSelected(group)
    for (const permission of group.permissions) toggle(permission, value)
  }
  async function save() {
    if (saving.value || !dirty.value) return
    saving.value = true
    const provider = props.provider,
      target = props.target
    try {
      await identity.setPermissions(
        provider,
        target,
        groups.value
          .flatMap((group) => group.permissions)
          .map((permission) => ({
            name: permission.name,
            isGranted: selected.value.has(permission.name)
          }))
      )
      ElMessage.success('权限已保存')
      await load()
      emit('saved')
    } catch {
      /* Keep changes available for retry on failure. */
    } finally {
      saving.value = false
    }
  }
  watch(() => [props.provider, props.target], load, { immediate: true })
  onBeforeUnmount(() => {
    generation++
  })
</script>

<style scoped lang="scss">
  @use './admin.scss';
  .permission-body {
    min-height: 140px;
  }
  .permission-group {
    margin-bottom: 24px;
  }
  .group-heading {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: 12px;
    border-bottom: 1px solid var(--art-gray-200);
    margin-bottom: 12px;
  }
  .group-heading h3 {
    font-size: 15px;
    font-weight: 650;
  }
  .permission-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    gap: 12px;
  }
  .permission-item {
    padding: 10px 14px;
    border-radius: 10px;
    background: var(--art-gray-100);
    overflow: hidden;
  }
  .permission-item.child {
    padding-left: 24px;
  }
  :deep(.el-checkbox) {
    height: auto;
    align-items: flex-start;
  }
  :deep(.el-checkbox__label) {
    white-space: normal;
    line-height: 1.5;
  }
  .permission-code {
    font-size: 11px;
    color: var(--art-gray-500);
    overflow-wrap: anywhere;
    margin-top: 3px;
  }
  .permission-footer {
    display: flex;
    justify-content: space-between;
    gap: 15px;
    align-items: center;
    border-top: 1px solid var(--art-gray-200);
    padding-top: 18px;
  }
</style>
