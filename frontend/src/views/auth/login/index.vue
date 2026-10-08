<template>
  <div class="flex w-full h-screen">
    <LoginLeftView />
    <div class="relative flex-1">
      <AuthTopBar />
      <div class="auth-right-wrap">
        <div class="form">
          <h3 class="title">欢迎登录</h3>
          <p class="sub-title">统一管理用户、角色与访问权限</p>
          <ElForm
            ref="formRef"
            :model="form"
            :rules="rules"
            @submit.prevent="submit"
            style="margin-top: 30px"
          >
            <ElFormItem prop="userName">
              <ElInput
                v-model.trim="form.userName"
                placeholder="请输入用户名"
                autocomplete="username"
                class="custom-height"
              />
            </ElFormItem>
            <ElFormItem prop="password">
              <ElInput
                v-model="form.password"
                type="password"
                show-password
                placeholder="请输入密码"
                autocomplete="current-password"
                class="custom-height"
              />
            </ElFormItem>
            <ElButton
              native-type="submit"
              type="primary"
              :loading="loading"
              class="w-full custom-height"
              style="margin-top: 15px"
              >登录</ElButton
            >
          </ElForm>
          <p class="mt-6 text-sm text-g-500">
            账号由系统管理员创建。需要访问权限时，请联系管理员。
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import type { FormInstance, FormRules } from 'element-plus'
  import { fetchLogin } from '@/api/auth'
  import { useUserStore } from '@/store/modules/user'
  defineOptions({ name: 'Login' })
  const formRef = ref<FormInstance>()
  const form = reactive({ userName: '', password: '' })
  const loading = ref(false)
  const router = useRouter()
  const route = useRoute()
  const user = useUserStore()
  const rules: FormRules = {
    userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
    password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
  }
  async function submit() {
    if (loading.value || !(await formRef.value?.validate().catch(() => false))) return
    loading.value = true
    try {
      const tokens = await fetchLogin(form)
      user.setToken(tokens.token, tokens.refreshToken)
      user.setLoginStatus(true)
      form.password = ''
      const redirect = route.query.redirect
      await router.push(
        typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')
          ? redirect
          : '/'
      )
    } catch {
      /* The API displays the actual login failure. */
    } finally {
      loading.value = false
    }
  }
</script>

<style scoped src="./style.css"></style>
