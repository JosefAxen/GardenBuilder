// Infrastructure for Drömträdgård. See docs/design.md, "Infrastruktur och drift".
// Skeleton: resources are added in week 1 (staging) and must stay identical between staging and production,
// differing only in parameters.
targetScope = 'resourceGroup'

@description('Environment name, e.g. staging or prod.')
@allowed(['staging', 'prod'])
param environmentName string

@description('Azure region. All data stays in Sweden Central.')
param location string = 'swedencentral'

var prefix = 'gb-${environmentName}'

// TODO week 1, in this order:
// 1. Log Analytics workspace + Application Insights
// 2. Container Registry
// 3. Key Vault (RBAC, access via managed identity)
// 4. Storage account (private 'photos' and 'dream-images' containers, public 'models' container, soft delete 7 days)
// 5. PostgreSQL Flexible Server (point-in-time restore)
// 6. Container Apps environment + 'api' and 'worker' container apps (min 1 replica in prod)

output resourcePrefix string = prefix
output deployedLocation string = location
