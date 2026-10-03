import { useHealth } from '@/api/health'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function HealthStatus() {
  const { data, isPending, isError } = useHealth()

  // Any health response means the API itself answered; the database has its own status.
  const api = isPending ? 'Checking…' : isError ? 'Unreachable' : 'Reachable'
  const database = isPending ? 'Checking…' : isError ? 'Unknown' : data.database

  return (
    <Card className="max-w-sm">
      <CardHeader>
        <CardTitle>System status</CardTitle>
        <CardDescription>Connection from this app to the API and the database.</CardDescription>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-[auto_1fr] items-center gap-x-4 gap-y-2 text-sm">
          <dt>API</dt>
          <dd>
            <StatusBadge value={api} />
          </dd>
          <dt>Database</dt>
          <dd>
            <StatusBadge value={database} />
          </dd>
        </dl>
      </CardContent>
    </Card>
  )
}

const okValues = new Set(['Healthy', 'Reachable'])

function StatusBadge({ value }: { value: string }) {
  const variant = okValues.has(value)
    ? 'default'
    : value === 'Checking…'
      ? 'outline'
      : 'destructive'
  return <Badge variant={variant}>{value}</Badge>
}
