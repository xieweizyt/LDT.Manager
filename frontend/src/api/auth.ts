import axios from 'axios'
import { ElMessage } from 'element-plus'
import request from '@/utils/http'
import { requestTokens } from './oauth'

export async function fetchLogin(params: Api.Auth.LoginParams): Promise<Api.Auth.LoginResponse> {
  try {
    const tokens = await requestTokens({
      grant_type: 'password',
      username: params.userName,
      password: params.password,
      scope: import.meta.env.VITE_OAUTH_SCOPE
    })
    return { token: tokens.access_token, refreshToken: tokens.refresh_token }
  } catch (error) {
    ElMessage.error(
      axios.isAxiosError(error)
        ? error.response?.data?.error_description || '登录失败，请检查账号、密码及后端服务'
        : '登录失败'
    )
    throw error
  }
}

export function fetchGetUserInfo() {
  return request.get<Api.Auth.UserInfo & { permissions: string[] }>({
    url: '/api/ldt/session'
  })
}
