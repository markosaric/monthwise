import { Link } from 'react-router'
import { PageHeader } from './PageHeader'

export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found" />
      <Link to="/" className="text-sm underline underline-offset-4">
        Back to the dashboard
      </Link>
    </>
  )
}
