<template>
  <div class="ldt-page">
    <div class="ldt-heading">
      <div>
        <h2>用户列表</h2>
        <p>管理账号信息、启用状态和用户访问权限</p>
      </div>
      <ElButton v-if="can('AbpIdentity.Users.Create')" type="primary" @click="edit()"
        ><ArtSvgIcon icon="ri:add-line" class="mr-1" />新增用户</ElButton
      >
    </div>
    <ElCard class="ldt-card" shadow="never">
      <div class="ldt-toolbar">
        <div class="ldt-search">
          <ElInput
            v-model="filter"
            placeholder="搜索用户名、姓名或邮箱"
            clearable
            @keyup.enter="search"
            @clear="search"
          />
          <ElButton type="primary" plain @click="search">查询</ElButton>
        </div>
        <ElButton :loading="loading" @click="load">刷新</ElButton>
      </div>
      <ElAlert
        v-if="failed"
        title="用户列表加载失败，请重试"
        type="error"
        :closable="false"
        class="mb-4"
      />
      <ElTable
        v-loading="loading"
        :data="rows"
        row-key="id"
        empty-text="暂无用户"
        style="width: 100%"
      >
        <ElTableColumn label="用户" min-width="220">
          <template #default="{ row }">
            <div class="ldt-person">
              <ElAvatar :size="38">{{ row.userName.slice(0, 1).toUpperCase() }}</ElAvatar>
              <div>
                <div>{{ row.userName }}</div>
                <div class="ldt-muted">{{ row.name || '未设置姓名' }}</div>
              </div>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="email" label="邮箱" min-width="220" show-overflow-tooltip />
        <ElTableColumn label="手机号码" min-width="150"
          ><template #default="{ row }">{{ row.phoneNumber || '--' }}</template></ElTableColumn
        >
        <ElTableColumn label="账号状态" width="110"
          ><template #default="{ row }"
            ><ElTag :type="row.isActive ? 'success' : 'info'" effect="light">{{
              row.isActive ? '已启用' : '已停用'
            }}</ElTag></template
          ></ElTableColumn
        >
        <ElTableColumn label="操作" min-width="230" fixed="right">
          <template #default="{ row }">
            <ElButton v-if="can('AbpIdentity.Users.Update')" link type="primary" @click="edit(row)"
              >编辑</ElButton
            >
            <ElButton
              v-if="
                can('AbpIdentity.Users.ManagePermissions') ||
                can('AbpIdentity.Users.Update.ManageRoles')
              "
              link
              type="primary"
              @click="
                router.push({
                  path: '/system/user-authorization',
                  query: { id: row.id }
                })
              "
              >授权</ElButton
            >
            <ElButton
              v-if="can('AbpIdentity.Users.Delete')"
              link
              type="danger"
              :disabled="row.id === user.info.userId"
              @click="remove(row)"
              >删除</ElButton
            >
          </template>
        </ElTableColumn>
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
      :title="editingId ? '编辑用户' : '新增用户'"
      width="min(560px, 94vw)"
      :close-on-click-modal="!saving"
      destroy-on-close
    >
      <ElForm
        ref="formRef"
        :model="form"
        :rules="rules"
        label-position="top"
        @submit.prevent="save"
      >
        <ElFormItem label="用户名" prop="userName"
          ><ElInput v-model.trim="form.userName" maxlength="256"
        /></ElFormItem>
        <ElFormItem label="姓名"><ElInput v-model="form.name" maxlength="64" /></ElFormItem>
        <ElFormItem label="邮箱" prop="email"
          ><ElInput v-model.trim="form.email" maxlength="256"
        /></ElFormItem>
        <ElFormItem label="手机号码"
          ><ElInput v-model.trim="form.phoneNumber" maxlength="16"
        /></ElFormItem>
        <ElFormItem v-if="!editingId" label="初始密码" prop="password"
          ><ElInput
            v-model="form.password"
            type="password"
            show-password
            autocomplete="new-password"
        /></ElFormItem>
        <ElFormItem label="账号状态"
          ><ElSwitch v-model="form.isActive" active-text="启用" inactive-text="停用"
        /></ElFormItem>
      </ElForm>
      <template #footer
        ><ElButton :disabled="saving" @click="visible = false">取消</ElButton
        ><ElButton type="primary" :loading="saving" @click="save">保存</ElButton></template
      >
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
  import { identity, type User, type UserInput } from '@/api/identity'
  import { useAccess } from '../shared/useAccess'
  defineOptions({ name: 'User' })
  const { can, user } = useAccess()
  const router = useRouter()
  const rows = ref<User[]>([])
  const filter = ref(''),
    page = ref(1),
    size = ref(10),
    total = ref(0)
  const loading = ref(false),
    failed = ref(false),
    visible = ref(false),
    saving = ref(false)
  const editingId = ref('')
  const formRef = ref<FormInstance>()
  const emptyForm = (): UserInput => ({
    userName: '',
    name: '',
    email: '',
    phoneNumber: '',
    password: '',
    isActive: true,
    lockoutEnabled: true
  })
  const form = reactive<UserInput>(emptyForm())
  const rules = computed<FormRules>(() => ({
    userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
    email: [
      { required: true, message: '请输入邮箱', trigger: 'blur' },
      { type: 'email', message: '请输入有效的邮箱', trigger: 'blur' }
    ],
    password: editingId.value
      ? []
      : [
          { required: true, message: '请输入初始密码', trigger: 'blur' },
          {
            min: 6,
            message: '密码至少 6 位，需包含大小写字母、数字及符号',
            trigger: 'blur'
          }
        ]
  }))
  async function load() {
    loading.value = true
    failed.value = false
    try {
      const result = await identity.users(filter.value, page.value, size.value)
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
  async function edit(row?: User) {
    if (row) {
      try {
        const fresh = await identity.user(row.id)
        Object.assign(form, emptyForm(), fresh)
        editingId.value = fresh.id
      } catch {
        return
      }
    } else {
      Object.assign(form, emptyForm())
      delete form.concurrencyStamp
      editingId.value = ''
    }
    visible.value = true
  }
  async function save() {
    if (saving.value || !(await formRef.value?.validate().catch(() => false))) return
    saving.value = true
    try {
      if (editingId.value) {
        const { password: _password, ...data } = form
        await identity.updateUser(editingId.value, data)
      } else await identity.createUser({ ...form })
      ElMessage.success('用户已保存')
      visible.value = false
      await load()
    } catch {
      /* API shows validation and permission errors. */
    } finally {
      saving.value = false
    }
  }
  async function remove(row: User) {
    try {
      await ElMessageBox.confirm(`确定删除用户“${row.userName}”？`, '删除用户', {
        type: 'warning',
        confirmButtonText: '删除',
        cancelButtonText: '取消'
      })
      await identity.deleteUser(row.id)
      ElMessage.success('用户已删除')
      if (rows.value.length === 1 && page.value > 1) page.value--
      await load()
    } catch {
      /* Cancellation or an API error leaves the row unchanged. */
    }
  }
  onMounted(load)
</script>

<style scoped lang="scss">
  @use '../shared/admin.scss';
</style>
