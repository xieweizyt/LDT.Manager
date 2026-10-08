<template>
  <div class="ldt-page">
    <div class="ldt-heading">
      <div>
        <h2>角色列表</h2>
        <p>维护角色，统一配置成员的访问权限</p>
      </div>
      <ElButton v-if="can('AbpIdentity.Roles.Create')" type="primary" @click="edit()"
        ><ArtSvgIcon icon="ri:add-line" class="mr-1" />新增角色</ElButton
      >
    </div>
    <ElCard class="ldt-card" shadow="never">
      <div class="ldt-toolbar">
        <div class="ldt-search">
          <ElInput
            v-model="filter"
            placeholder="搜索角色名称"
            clearable
            @keyup.enter="search"
            @clear="search"
          /><ElButton type="primary" plain @click="search">查询</ElButton>
        </div>
        <ElButton :loading="loading" @click="load">刷新</ElButton>
      </div>
      <ElAlert
        v-if="failed"
        title="角色列表加载失败，请重试"
        type="error"
        :closable="false"
        class="mb-4"
      />
      <ElTable v-loading="loading" :data="rows" row-key="id" empty-text="暂无角色">
        <ElTableColumn label="角色名称" min-width="220"
          ><template #default="{ row }"
            ><div class="ldt-person">
              <ElAvatar :size="38"><ArtSvgIcon icon="ri:shield-user-line" /></ElAvatar
              ><span>{{ row.name }}</span
              ><ElTag v-if="row.isStatic" size="small" type="info">系统角色</ElTag>
            </div></template
          ></ElTableColumn
        >
        <ElTableColumn label="默认角色" min-width="120"
          ><template #default="{ row }"
            ><ElTag :type="row.isDefault ? 'success' : 'info'">{{
              row.isDefault ? '是' : '否'
            }}</ElTag></template
          ></ElTableColumn
        >
        <ElTableColumn label="公开角色" min-width="120"
          ><template #default="{ row }">{{ row.isPublic ? '是' : '否' }}</template></ElTableColumn
        >
        <ElTableColumn label="操作" min-width="240" fixed="right"
          ><template #default="{ row }"
            ><ElButton v-if="can('AbpIdentity.Roles.Update')" link type="primary" @click="edit(row)"
              >编辑</ElButton
            ><ElButton
              v-if="can('AbpIdentity.Roles.ManagePermissions')"
              link
              type="primary"
              @click="
                router.push({
                  path: '/system/role-authorization',
                  query: { name: row.name }
                })
              "
              >授权</ElButton
            ><ElButton
              v-if="can('AbpIdentity.Roles.Delete')"
              link
              type="danger"
              :disabled="row.isStatic"
              @click="remove(row)"
              >删除</ElButton
            ></template
          ></ElTableColumn
        >
      </ElTable>
      <div class="ldt-pagination">
        <ElPagination
          v-model:current-page="page"
          v-model:page-size="size"
          :page-sizes="[10, 20, 50]"
          :total="total"
          layout="total, sizes, prev, pager, next"
          @current-change="load"
          @size-change="search"
        />
      </div>
    </ElCard>
    <ElDialog
      v-model="visible"
      :title="editingId ? '编辑角色' : '新增角色'"
      width="min(500px, 94vw)"
      :close-on-click-modal="!saving"
      destroy-on-close
    >
      <ElForm ref="formRef" :model="form" label-position="top" @submit.prevent="save">
        <ElFormItem
          label="角色名称"
          prop="name"
          :rules="[{ required: true, message: '请输入角色名称', trigger: 'blur' }]"
          ><ElInput v-model.trim="form.name" :disabled="staticRole" maxlength="256"
        /></ElFormItem>
        <ElFormItem label="默认角色"
          ><ElSwitch v-model="form.isDefault" /><span class="ml-3 ldt-muted"
            >新用户自动加入此角色</span
          ></ElFormItem
        >
        <ElFormItem label="公开角色"><ElSwitch v-model="form.isPublic" /></ElFormItem>
      </ElForm>
      <template #footer
        ><ElButton :disabled="saving" @click="visible = false">取消</ElButton
        ><ElButton type="primary" :loading="saving" @click="save">保存</ElButton></template
      >
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox, type FormInstance } from 'element-plus'
  import { identity, type Role, type RoleInput } from '@/api/identity'
  import { useAccess } from '../shared/useAccess'
  defineOptions({ name: 'Role' })
  const { can } = useAccess()
  const router = useRouter()
  const rows = ref<Role[]>([]),
    filter = ref(''),
    page = ref(1),
    size = ref(10),
    total = ref(0)
  const loading = ref(false),
    failed = ref(false),
    visible = ref(false),
    saving = ref(false),
    staticRole = ref(false)
  const editingId = ref(''),
    formRef = ref<FormInstance>()
  const form = reactive<RoleInput>({
    name: '',
    isDefault: false,
    isPublic: true
  })
  async function load() {
    loading.value = true
    failed.value = false
    try {
      const result = await identity.roles(filter.value, page.value, size.value)
      rows.value = result.items
      total.value = result.totalCount
    } catch {
      failed.value = true
      rows.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }
  function search() {
    page.value = 1
    void load()
  }
  function edit(row?: Role) {
    editingId.value = row?.id ?? ''
    staticRole.value = row?.isStatic ?? false
    Object.assign(form, {
      name: row?.name ?? '',
      isDefault: row?.isDefault ?? false,
      isPublic: row?.isPublic ?? true,
      concurrencyStamp: row?.concurrencyStamp
    })
    visible.value = true
  }
  async function save() {
    if (saving.value || !(await formRef.value?.validate().catch(() => false))) return
    saving.value = true
    try {
      if (editingId.value) await identity.updateRole(editingId.value, { ...form })
      else await identity.createRole({ ...form })
      ElMessage.success('角色已保存')
      visible.value = false
      await load()
    } catch {
      /* API displays the failure. */
    } finally {
      saving.value = false
    }
  }
  async function remove(row: Role) {
    try {
      await ElMessageBox.confirm(`确定删除角色“${row.name}”？`, '删除角色', {
        type: 'warning',
        confirmButtonText: '删除',
        cancelButtonText: '取消'
      })
      await identity.deleteRole(row.id)
      ElMessage.success('角色已删除')
      if (rows.value.length === 1 && page.value > 1) page.value--
      await load()
    } catch {
      /* Cancellation or an API failure leaves the role unchanged. */
    }
  }
  onMounted(load)
</script>

<style scoped lang="scss">
  @use '../shared/admin.scss';
</style>
