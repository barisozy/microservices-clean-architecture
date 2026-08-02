# Deployment and Docker Compose

`ECommerce.AppHost` is the single source of truth for local orchestration and Docker Compose topology. The root `docker-compose.yml` is generated output; do not hand-author or edit it directly.

## Local Aspire

Install the Aspire CLI that matches the repository's Aspire hosting packages, then run:

```powershell
aspire run --project .\src\Orchestration\ECommerce.AppHost\ECommerce.AppHost.csproj
```

## Generate Docker Compose

From the repository root, publish the AppHost to a temporary output directory and copy the generated file to the root:

```powershell
$output = Join-Path (Get-Location) 'artifacts\compose'
aspire publish `
  --apphost .\src\Orchestration\ECommerce.AppHost\ECommerce.AppHost.csproj `
  --output-path $output `
  --non-interactive `
  --nologo
Copy-Item "$output\docker-compose.yaml" .\docker-compose.yml -Force
docker compose --env-file .env.example -f .\docker-compose.yml config --quiet
```

The generated topology contains the Gateway, 11 backend services, PostgreSQL 18.4, RabbitMQ 4.3.1-management, Valkey 9.1, Keycloak 26.6.4 and the Aspire Dashboard. Host-specific bind mounts are supplied through the generated Compose environment placeholders; use `.env.example` as the template and keep real credentials in an untracked `.env` file.

`artifacts/` and the Aspire CLI cache are local generated state and are intentionally ignored by Git. They can be deleted and recreated at any time.
