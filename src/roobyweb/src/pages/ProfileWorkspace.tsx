import { Link, useParams, useSearchParams } from 'react-router-dom'
import { DiffTab } from './profile/DiffTab'
import { ItemsTab } from './profile/ItemsTab'
import { SettingsTab } from './profile/SettingsTab'
import { TestsTab } from './profile/TestsTab'
import { VersionsTab } from './profile/VersionsTab'

type Tab = 'items' | 'tests' | 'diff' | 'versions' | 'settings'

const TABS: { key: Tab; label: string }[] = [
  { key: 'items', label: 'Items' },
  { key: 'tests', label: 'Test cases' },
  { key: 'diff', label: 'Pending changes' },
  { key: 'versions', label: 'Versions' },
  { key: 'settings', label: 'Settings' },
]

export function ProfileWorkspace() {
  const { project, profile } = useParams<{ project: string; profile: string }>()
  const [searchParams, setSearchParams] = useSearchParams()

  const tab = (searchParams.get('tab') as Tab | null) ?? 'items'

  return (
    <div>
      <div className="breadcrumbs">
        <Link to="/projects">Projects</Link> / <Link to={`/projects/${project}`}>{project}</Link> / {profile}
      </div>
      <div className="tabs">
        {TABS.map((t) => (
          <button
            type="button"
            key={t.key}
            className={tab === t.key ? 'tab active' : 'tab'}
            onClick={() => setSearchParams({ tab: t.key })}
          >
            {t.label}
          </button>
        ))}
      </div>
      {tab === 'items' && <ItemsTab />}
      {tab === 'tests' && <TestsTab />}
      {tab === 'diff' && <DiffTab />}
      {tab === 'versions' && <VersionsTab />}
      {tab === 'settings' && <SettingsTab />}
    </div>
  )
}
