$sql = 'SELECT id, title, description, severity, status ' +
  'FROM incidents WHERE id = ' +
  "'20000000-0000-0000-0000-000000000003';"
docker compose --env-file infra/.env.example `
  -f infra/compose.yaml exec -T postgres `
  psql -U securelab -d securelab -c $sql
Remove-Variable sql
