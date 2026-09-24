import type { ReactElement } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function RequireAuth({ children }: { children: ReactElement }) {
  const { me, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return <div className="page-loading">Loading…</div>
  }

  if (!me) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}
