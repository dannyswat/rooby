import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { RequireAuth } from './components/RequireAuth'
import { AdminAccessPage } from './pages/AdminAccessPage'
import { LoginPage } from './pages/LoginPage'
import { ProfileWorkspace } from './pages/ProfileWorkspace'
import { ProjectPage } from './pages/ProjectPage'
import { ProjectsPage } from './pages/ProjectsPage'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route path="/" element={<Navigate to="/projects" replace />} />
        <Route path="/projects" element={<ProjectsPage />} />
        <Route path="/projects/:project" element={<ProjectPage />} />
        <Route path="/projects/:project/profiles/:profile" element={<ProfileWorkspace />} />
        <Route path="/admin/access" element={<AdminAccessPage />} />
        <Route path="*" element={<Navigate to="/projects" replace />} />
      </Route>
    </Routes>
  )
}

export default App
