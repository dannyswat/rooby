import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { ProjectResponse } from '../api/types'
import { ProblemAlert } from '../components/ProblemAlert'
import { ProfilesTab } from './project/ProfilesTab'
import { SchemasTab } from './project/SchemasTab'

type Tab = 'profiles' | 'schemas'

export function ProjectPage() {
  const { project } = useParams<{ project: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const [entity, setEntity] = useState<ProjectResponse | null>(null)
  const [error, setError] = useState<unknown>(null)

  const tab: Tab = searchParams.get('tab') === 'schemas' ? 'schemas' : 'profiles'

  useEffect(() => {
    void (async () => {
      try {
        setEntity(await api.get<ProjectResponse>(`/projects/${project}`))
      } catch (err) {
        setError(err)
      }
    })()
  }, [project])

  return (
    <div>
      <div className="breadcrumbs">
        <Link to="/projects">Projects</Link> / {project}
      </div>
      <div className="page-header">
        <h1>{entity?.name ?? project}</h1>
      </div>
      <ProblemAlert error={error} />
      <div className="tabs">
        <button
          type="button"
          className={tab === 'profiles' ? 'tab active' : 'tab'}
          onClick={() => setSearchParams({ tab: 'profiles' })}
        >
          Profiles
        </button>
        <button
          type="button"
          className={tab === 'schemas' ? 'tab active' : 'tab'}
          onClick={() => setSearchParams({ tab: 'schemas' })}
        >
          Schemas
        </button>
      </div>
      {tab === 'profiles' ? <ProfilesTab /> : <SchemasTab />}
    </div>
  )
}
