import { Link } from 'react-router'
import { PageHeader } from './PageHeader'
import { appTexts } from '@/components/common/texts'

export function NotFoundPage() {
  return (
    <>
      <PageHeader title={appTexts.notFound.title} />
      <Link to="/" className="text-sm underline underline-offset-4">
        {appTexts.common.backToDashboard}
      </Link>
    </>
  )
}
