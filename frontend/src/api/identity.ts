import request from '@/utils/http'

export interface Paged<T> {
  items: T[]
  totalCount: number
}
export interface User {
  id: string
  userName: string
  name?: string
  surname?: string
  email: string
  phoneNumber?: string
  isActive: boolean
  lockoutEnabled: boolean
  concurrencyStamp?: string
}
export interface Role {
  id: string
  name: string
  isDefault: boolean
  isPublic: boolean
  isStatic: boolean
  concurrencyStamp?: string
}
export interface UserInput {
  userName: string
  name?: string
  email: string
  phoneNumber?: string
  password?: string
  isActive: boolean
  lockoutEnabled: boolean
  concurrencyStamp?: string
}
export interface RoleInput {
  name: string
  isDefault: boolean
  isPublic: boolean
  concurrencyStamp?: string
}
export interface Permission {
  name: string
  displayName: string
  parentName?: string
  isGranted: boolean
  allowedProviders: string[]
  grantedProviders: { providerName: string; providerKey: string }[]
}
export interface PermissionGroup {
  name: string
  displayName: string
  permissions: Permission[]
}
export interface PermissionResult {
  entityDisplayName: string
  groups: PermissionGroup[]
}
export const identity = {
  users: (filter = '', page = 1, size = 10) =>
    request.get<Paged<User>>({
      url: '/api/identity/users',
      params: {
        filter,
        skipCount: (page - 1) * size,
        maxResultCount: size,
        sorting: 'UserName'
      }
    }),
  user: (id: string) => request.get<User>({ url: `/api/identity/users/${id}` }),
  createUser: (data: UserInput) => request.post<User>({ url: '/api/identity/users', data }),
  updateUser: (id: string, data: UserInput) =>
    request.put<User>({ url: `/api/identity/users/${id}`, data }),
  deleteUser: (id: string) => request.del<void>({ url: `/api/identity/users/${id}` }),
  roles: (filter = '', page = 1, size = 10) =>
    request.get<Paged<Role>>({
      url: '/api/identity/roles',
      params: {
        filter,
        skipCount: (page - 1) * size,
        maxResultCount: size,
        sorting: 'Name'
      }
    }),
  assignableRoles: () =>
    request.get<{ items: Role[] }>({
      url: '/api/identity/users/assignable-roles'
    }),
  createRole: (data: RoleInput) => request.post<Role>({ url: '/api/identity/roles', data }),
  updateRole: (id: string, data: RoleInput) =>
    request.put<Role>({ url: `/api/identity/roles/${id}`, data }),
  deleteRole: (id: string) => request.del<void>({ url: `/api/identity/roles/${id}` }),
  userRoles: (id: string) =>
    request.get<{ items: Role[] }>({ url: `/api/identity/users/${id}/roles` }),
  inheritedPermissions: (id: string) =>
    request.get<Record<string, string[]>>({
      url: `/api/ldt/users/${id}/inherited-permissions`
    }),
  setUserRoles: (id: string, roleNames: string[]) =>
    request.put<void>({
      url: `/api/ldt/users/${id}/roles`,
      data: { roleNames }
    }),
  permissions: (providerName: 'U' | 'R', providerKey: string) =>
    request.get<PermissionResult>({
      url: '/api/permission-management/permissions',
      params: { providerName, providerKey }
    }),
  setPermissions: (
    providerName: 'U' | 'R',
    providerKey: string,
    permissions: { name: string; isGranted: boolean }[]
  ) =>
    request.put<void>({
      url: '/api/permission-management/permissions',
      params: { providerName, providerKey },
      data: { permissions }
    })
}
