import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { ApiError, api } from '../api/client'
import type { MeResponse } from '../api/types'

interface AuthContextValue {
  me: MeResponse | null
  loading: boolean
  login: (loginName: string, password: string) => Promise<void>
  logout: () => Promise<void>
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<MeResponse | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    try {
      setMe(await api.get<MeResponse>('/me'))
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setMe(null)
      } else {
        throw err
      }
    }
  }, [])

  useEffect(() => {
    void (async () => {
      setLoading(true)
      await refresh()
      setLoading(false)
    })()
  }, [refresh])

  const login = useCallback(async (loginName: string, password: string) => {
    await api.post('/auth/local-login', { loginName, password })
    await refresh()
  }, [refresh])

  const logout = useCallback(async () => {
    await api.post('/auth/logout')
    setMe(null)
  }, [])

  const value = useMemo(() => ({ me, loading, login, logout, refresh }), [me, loading, login, logout, refresh])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}

export function hasSystemAccess(me: MeResponse | null, flag: string): boolean {
  if (!me) {
    return false
  }
  const flags = me.systemAccess.split(',').map((s) => s.trim())
  return flags.includes('SystemAdmin') || flags.includes(flag)
}
