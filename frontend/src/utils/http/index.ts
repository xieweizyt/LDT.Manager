import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios'
import { useUserStore } from '@/store/modules/user'
import { requestTokens } from '@/api/oauth'
import { HttpError } from './error'
import { ElMessage } from 'element-plus'

interface RequestConfig extends AxiosRequestConfig {
  showErrorMessage?: boolean
  showSuccessMessage?: boolean
}
type RetryConfig = InternalAxiosRequestConfig & { _retried?: boolean }

const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  timeout: 15000
})
let refreshPromise: Promise<void> | undefined

client.interceptors.request.use((config) => {
  const token = useUserStore().accessToken
  if (token) config.headers.set('Authorization', `Bearer ${token}`)
  config.headers.set('Accept-Language', 'zh-Hans')
  return config
})

client.interceptors.response.use(undefined, async (error) => {
  const config = error.config as RetryConfig | undefined
  const user = useUserStore()
  if (error.response?.status === 401 && config && !config._retried && user.refreshToken) {
    config._retried = true
    try {
      refreshPromise ??= requestTokens({
        grant_type: 'refresh_token',
        refresh_token: user.refreshToken
      })
        .then((tokens) => user.setToken(tokens.access_token, tokens.refresh_token))
        .finally(() => {
          refreshPromise = undefined
        })
      await refreshPromise
      return client.request(config)
    } catch {
      user.logOut()
    }
  } else if (error.response?.status === 401) {
    user.logOut()
  }
  return Promise.reject(error)
})

async function send<T>(config: RequestConfig): Promise<T> {
  if (
    ['POST', 'PUT', 'PATCH'].includes(config.method?.toUpperCase() ?? '') &&
    config.params &&
    config.data === undefined
  ) {
    config = { ...config, data: config.params, params: undefined }
  }
  try {
    const response = await client.request<T>(config)
    if (config.showSuccessMessage) ElMessage.success('操作成功')
    // ABP returns raw DTOs and 204 for successful writes.
    return response.data
  } catch (error) {
    const failure = axios.isAxiosError(error) ? error : undefined
    const status = failure?.response?.status ?? 0
    const data = failure?.response?.data
    const message =
      data?.error?.message ||
      data?.error_description ||
      (
        {
          401: '登录已过期，请重新登录',
          403: '没有执行此操作的权限',
          429: '请求过于频繁，请稍后重试'
        } as Record<number, string>
      )[status] ||
      (status ? '操作失败，请稍后重试' : '无法连接后端，请检查服务是否启动')
    if (config.showErrorMessage !== false) ElMessage.error(message)
    throw new HttpError(message, status, {
      url: config.url,
      method: config.method
    })
  }
}

export default {
  get: <T>(config: RequestConfig) => send<T>({ ...config, method: 'GET' }),
  post: <T>(config: RequestConfig) => send<T>({ ...config, method: 'POST' }),
  put: <T>(config: RequestConfig) => send<T>({ ...config, method: 'PUT' }),
  del: <T>(config: RequestConfig) => send<T>({ ...config, method: 'DELETE' }),
  request: send
}
