import request from '@/utils/http'
import type { AppRouteRecord } from '@/types/router'
import { identity } from './identity'

// Adapters retained for upstream demo components; runtime pages use identity.ts directly.
export async function fetchGetUserList(params: Api.SystemManage.UserSearchParams) {
  const current = params.current ?? 1
  const size = params.size ?? 10
  const result = await identity.users(params.userName ?? '', current, size)
  return {
    records: result.items,
    current,
    size,
    total: result.totalCount
  } as unknown as Api.SystemManage.UserList
}

export async function fetchGetRoleList(params: Api.SystemManage.RoleSearchParams) {
  const current = params.current ?? 1
  const size = params.size ?? 10
  const result = await identity.roles(params.roleName ?? '', current, size)
  return {
    records: result.items,
    current,
    size,
    total: result.totalCount
  } as unknown as Api.SystemManage.RoleList
}

export function fetchGetMenuList() {
  return request.get<AppRouteRecord[]>({ url: '/api/ldt/menus' })
}
