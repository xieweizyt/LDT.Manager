import axios from 'axios'

export interface OAuthTokens {
  access_token: string
  refresh_token: string
  token_type: string
  expires_in: number
}

const apiBase = import.meta.env.VITE_API_URL.replace(/\/$/, '')

export async function requestTokens(values: Record<string, string>) {
  const body = new URLSearchParams({
    client_id: import.meta.env.VITE_OAUTH_CLIENT_ID,
    ...values
  })
  const { data } = await axios.post<OAuthTokens>(`${apiBase}/connect/token`, body, {
    timeout: 15000,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
  })
  return data
}

export async function revokeRefreshToken(token: string) {
  const body = new URLSearchParams({
    client_id: import.meta.env.VITE_OAUTH_CLIENT_ID,
    token,
    token_type_hint: 'refresh_token'
  })
  await axios.post(`${apiBase}/connect/revocation`, body, { timeout: 10000 })
}
