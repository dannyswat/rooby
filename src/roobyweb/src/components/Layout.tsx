import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { hasSystemAccess, useAuth } from '../auth/AuthContext'

export function Layout() {
  const { me, logout } = useAuth()
  const navigate = useNavigate()

  async function handleLogout() {
    await logout()
    navigate('/login')
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">Rooby</div>
        <nav>
          <NavLink to="/projects">Projects</NavLink>
          {hasSystemAccess(me, 'SystemAdmin') && <NavLink to="/admin/access">Access</NavLink>}
        </nav>
        <div className="topbar-user">
          {me && (
            <>
              <span>{me.displayName}</span>
              <button type="button" onClick={handleLogout}>
                Log out
              </button>
            </>
          )}
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}
