# Infrastructure

Bicep for Azure, region Sweden Central. Deployed by GitHub Actions with federated credentials (no stored secrets).

```bash
az deployment group create -g <resource-group> -f infra/main.bicep -p environmentName=staging
```
