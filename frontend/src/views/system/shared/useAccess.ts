import { useUserStore } from '@/store/modules/user'
import { fetchGetUserInfo } from '@/api/auth'

export function useAccess() {
  const user = useUserStore()
  const can = (permission: string) => user.info.buttons?.includes(permission) ?? false
  const refresh = async () => user.setUserInfo(await fetchGetUserInfo())
  return { can, refresh, user }
}
