import { ApiError } from '../api/client'

export function ProblemAlert({ error }: { error: unknown }) {
  if (!error) {
    return null
  }

  if (error instanceof ApiError) {
    const problem = error.problem
    return (
      <div className="alert alert-error">
        <div>{problem?.title ?? error.message}</div>
        {problem?.detail && <div>{problem.detail}</div>}
        {problem?.errors && problem.errors.length > 0 && (
          <ul>
            {problem.errors.map((e, i) => (
              <li key={i}>
                <strong>{e.path}</strong>: {e.detail} ({e.code})
              </li>
            ))}
          </ul>
        )}
      </div>
    )
  }

  return <div className="alert alert-error">{error instanceof Error ? error.message : String(error)}</div>
}
