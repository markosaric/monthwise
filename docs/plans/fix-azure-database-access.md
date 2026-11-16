# Plan: The Azure database is private, and Monthwise uses its own database login

- **Branch:** `fix/azure-database-access`
- **Status:** In progress
- **Date:** 2026-11-16
- **Requirements:** [production-packaging.md, Appendix 5](../feature-requirements/production-packaging.md#appendix-5-the-azure-database-is-private-and-monthwise-uses-its-own-database-login)

## Goal

On Azure, the database can only be reached from Monthwise's own private network, and Monthwise
signs in to it with its own login that can use only the Monthwise data. The server administrator's
password stays in Key Vault and is never given to Monthwise (finding M6 of the code review).

## Requirement changes

Implements [Appendix 5](../feature-requirements/production-packaging.md#appendix-5-the-azure-database-is-private-and-monthwise-uses-its-own-database-login)
of the production packaging requirements (2026-11-16, finding M6):

- **FR-10 Azure** (extended): the database is reachable only from Monthwise's private network in
  Azure; Monthwise uses its own login that can read, change and migrate only the Monthwise data and
  cannot create logins or databases or change server settings; the administrator password is kept
  only in Key Vault; a deploy over an installation from before the appendix either moves it with
  all data or stops before any change with clear instructions; the documentation lists the added
  resources and their cost, and the total stays within the default budget of 25; the public HTTPS
  address is unchanged (H1 is its own work item).
- **FR-6 Secrets** (extended): on Azure, Monthwise is given only its own database login, the
  encryption keys and the image download key, never the administrator password.
- **New acceptance criteria 16 to 18:** the database refuses connections from outside the private
  network while Monthwise works; Monthwise's login can use and migrate its data but cannot create a
  login or a database, and the administrator password is not among Monthwise's secrets; a deploy
  over an older installation ends with all data on the private database or stops before any change
  with instructions; the user manual describes all of this.

## Scope

- In scope: `deploy/azure/main.bicep`, `deploy/azure/deploy.sh`, a new database setup script for
  the Azure setup job, the Azure chapter of the user manual, and a short note in `docs/INIT.md`.
- Out of scope: everything in the appendix's "Out of scope" (H1 and login, database logins on the
  Docker one-click and the native service, Entra ID sign-in). No application code, no migration
  and no front-end change: the application already reads its connection string from
  `/config/secrets`, and only its value changes.

## Design

- **Domain / data model changes:** none.
- **API / front-end changes:** none.
- **Database migrations:** none. The application keeps bringing the database up to date at start
  (FR-3), now as the owner of the `monthwise` database instead of as the administrator.

### Private network (`main.bicep`)

- **Virtual network** `vnet-monthwise-<suffix>`, `10.40.0.0/24`, with two subnets:
  - `apps` `10.40.0.0/27`, delegated to `Microsoft.App/environments` (the smallest subnet a
    workload profiles environment accepts).
  - `database` `10.40.0.32/28`, delegated to `Microsoft.DBforPostgreSQL/flexibleServers`.
- **Private DNS zone** `monthwise.private.postgres.database.azure.com`, linked to the virtual
  network, so the server's name resolves to its private address inside the network.
- **PostgreSQL Flexible Server:** `network: { delegatedSubnetResourceId, privateDnsZoneArmResourceId,
  publicNetworkAccess: 'Disabled' }` (VNet integration). The `AllowAzureServices` firewall rule is
  removed; a VNet-integrated server has no public address and no firewall. VNet integration is
  chosen over a private endpoint because it has no hourly cost (a private endpoint costs about
  €7 a month, which would push the total past the default budget of 25).
- **Container Apps environment:** a workload profiles environment with only the `Consumption`
  profile (same pricing and scale to zero as today, no fixed cost) and
  `vnetConfiguration: { infrastructureSubnetId: apps, internal: false }`. `internal: false` keeps
  the public HTTPS address exactly as today (the appendix leaves that to H1). Outbound traffic to
  ghcr.io, Docker Hub and Key Vault keeps working through the environment's default outbound
  address.
- Azure cannot add a virtual network to an existing Container Apps environment, nor move an
  existing PostgreSQL server from public access to VNet integration. Both are only possible on
  new resources, which drives the handling of older installations below.

### Monthwise's own login

- **Login:** `monthwise_app`, `LOGIN`, `NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION`, with
  its own random password.
- **What it owns:** the `monthwise` database and its `public` schema. Owning the database lets it
  create and drop the temporary import schema (backup and restore), create the name collation and
  run every migration; it owns every table it creates. It is not a member of `azure_pg_admin`.
- **What it cannot do:** create logins or databases, change server settings, or connect to the
  other databases: `CONNECT` on `monthwise` is revoked from `PUBLIC` and given only to
  `monthwise_app` and the administrator, and on the `postgres` database `CONNECT` is revoked from
  `PUBLIC` where Azure allows the administrator to do so (see open questions).
- **Secrets in Key Vault:** a new secret `app-database-password`. The secret `connection-string`
  now holds `Username=monthwise_app;Password=<app password>`. `postgres-password` (the
  administrator's) stays in Key Vault for the user and the setup job only.
- **Container app:** unchanged list of secrets (`connection-string`, encryption key, hash key,
  ghcr token); none of them contains the administrator password (FR-6).

### Database setup job

- A Container Apps job `caj-monthwise-db-setup` in the same environment (so it reaches the private
  database), trigger type `Manual`, image `docker.io/library/postgres:17-alpine` (a base image
  tag, as accepted in Appendix 2), 0.25 CPU / 0.5 Gi, one replica, retry limit 1, timeout 5 minutes.
- Its secrets come from Key Vault through the same managed identity: `postgres-password` and
  `app-database-password`, mounted as files. The administrator password exists only in this job,
  which runs for a few seconds per deploy.
- Its command runs `psql` with the setup SQL, passed in the template from `db-setup.sql` with
  `loadTextContent`, so the SQL is a readable file in the repository. The SQL is idempotent:
  1. create `monthwise_app` if it does not exist, otherwise set its password (so a changed Key Vault
     value is applied), always with the `NO...` attributes above;
  2. `GRANT monthwise_app TO <administrator> WITH INHERIT FALSE, SET TRUE` (needed on PostgreSQL 16+
     to hand ownership over);
  3. `ALTER DATABASE monthwise OWNER TO monthwise_app`, `ALTER SCHEMA public OWNER TO monthwise_app`,
     and `REASSIGN OWNED BY <administrator> TO monthwise_app` inside `monthwise`, for tables an
     earlier start may have created as the administrator;
  4. the `CONNECT` revokes and grants above.
  Passwords reach `psql` as variables read from the mounted files, never on the command line or in
  the log.

### Deploy step (`deploy.sh`)

- **First deployment:** also creates `app_database_password` (`openssl rand -hex 24`) and passes it
  to the template as a new secure parameter.
- **Later deployments:** reads it back from Key Vault like the other secrets.
- **Older installation check**, before anything is deployed: if the resource group already has a
  PostgreSQL server without a delegated subnet (`network.delegatedSubnetResourceId` empty), the
  script stops, changes nothing and prints the steps: export the data in Monthwise (Settings),
  deploy into a new resource group with `MONTHWISE_RESOURCE_GROUP=rg-monthwise-2 deploy.sh
  <version>`, import the export file there, check it, then delete the old resource group. This is
  the "stops before changing anything" branch of the appendix: Azure cannot move the existing
  server or environment in place, and the export file already works across installations with
  their own encryption keys. A copy inside the script is not possible either, because after the
  move the new database cannot be reached from Cloud Shell.
- **Order:** deploy the template (which creates or updates the job and the app), then start the
  setup job with `az containerapp job start` and wait for it to succeed (failure: print the job's
  log command and stop with exit code 1). On the first deployment the app's first start fails to
  sign in because `monthwise_app` does not exist yet; after the job has run, the script restarts
  the active revision, then waits for health as today. On later deployments the job only re-applies
  the same state, and the new revision signs in normally.
- The restore hint printed on failure (Appendix 3) stays; the restored server must be created in
  the same delegated subnet, so the printed command gets `--subnet` and `--private-dns-zone`.

### Documentation

- **User manual, Azure chapter:** the resource table gets the virtual network (free), the private
  DNS zone (about €0.50 a month) and the setup job (within the monthly free grant). Expected total
  stays "about €15 to 20 per month after the free first year". New paragraphs: the database is
  reachable only from Monthwise's private network, so database tools on the user's computer
  cannot connect to it; Monthwise uses its own login; where the administrator password is and
  when it is needed; how to move an installation made before this release (the steps printed by
  `deploy.sh`). "Azure's own backups" says that a restored server must be put in the same private
  network.
- **`docs/INIT.md`:** one row in the architecture table: on Azure the database is private and the
  app uses `monthwise_app`, set up by the setup job.

## Steps

1. Add `deploy/azure/db-setup.sql` with the idempotent setup above.
2. Change `main.bicep`: virtual network, subnets, private DNS zone and link, private server, new
   environment settings, setup job, `appDatabasePassword` parameter and Key Vault secret, the app's
   connection string; remove the firewall rule.
3. Change `deploy.sh`: new secret, older installation check, run the setup job, restart on the
   first deployment, restore hint.
4. Update the user manual's Azure chapter and `docs/INIT.md`.
5. Run the CI checks locally where possible: `az bicep build` and `shellcheck`.
6. Stage everything for review; the real deployment checks below need the user's Azure account.

## Tests

- **Unit / API tests:** none; no application code changes. The existing suites must stay green.
- **CI:** the existing "Check Azure template" (`az bicep build`) and "Check deployment scripts"
  (`shellcheck`) steps cover the changed files.
- **Manual checks on Azure** (need the user's subscription; cannot be run from here):
  - New deployment into an empty resource group: `deploy.sh` ends with "Monthwise is running" and
    the health response reports the database as working (acceptance 16).
  - From Cloud Shell, `psql` to the server's name fails to connect (no public address)
    (acceptance 16).
  - `az containerapp show` lists no secret holding the administrator password; in the setup job's
    log, `monthwise_app` has no `CREATEROLE`/`CREATEDB`; a `CREATE ROLE` attempt as `monthwise_app`
    (one-off `psql` from the setup job image) is refused (acceptance 17).
  - Deploying the same version again succeeds and keeps the data (FR-3).
  - In a resource group made with the previous `main.bicep`, `deploy.sh` stops before any change
    and prints the move steps; following them moves all data (acceptance 18).

## Open questions

- Whether Azure lets the administrator revoke `CONNECT` on the `postgres` database from `PUBLIC`.
  If not, `monthwise_app` can still connect there but owns and can change nothing in it; I will
  note the result of the manual check in this plan.
