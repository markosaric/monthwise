-- Gives Monthwise its own database login on Azure (production packaging, Appendix 5). Run by the
-- setup job (caj-monthwise-db-setup) as the server administrator on every deployment, before the
-- app starts; safe to run again. The passwords come from Key Vault through environment variables
-- and are never printed.
\set ON_ERROR_STOP on
\set app_password `printf '%s' "$APP_DATABASE_PASSWORD"`
SELECT current_user AS admin \gset

-- The login: no superuser, no new logins, no new databases (Azure does not let the administrator
-- grant superuser, replication or bypassing row security at all).
SELECT NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'monthwise_app') AS create_login \gset
\if :create_login
CREATE ROLE monthwise_app LOGIN;
\endif
ALTER ROLE monthwise_app WITH LOGIN NOCREATEDB NOCREATEROLE INHERIT PASSWORD :'app_password';

-- PostgreSQL 16 and later: the administrator needs SET on the login to hand the database over and
-- to act as its owner below, without inheriting its rights.
GRANT monthwise_app TO :"admin" WITH INHERIT FALSE, SET TRUE;

-- Monthwise owns its database, so it can bring it up to date (FR-3), create the import schema and
-- the name collation. Its public schema follows the database owner (pg_database_owner).
SELECT pg_get_userbyid(datdba) <> 'monthwise_app' AS hand_over
  FROM pg_database WHERE datname = 'monthwise' \gset
\if :hand_over
ALTER DATABASE monthwise OWNER TO monthwise_app;
\endif

-- Only Monthwise and the administrator may connect to the Monthwise database.
SET ROLE monthwise_app;
REVOKE ALL ON DATABASE monthwise FROM PUBLIC;
GRANT CONNECT ON DATABASE monthwise TO :"admin";
RESET ROLE;

-- Keep Monthwise out of the server's maintenance database too, where Azure lets the administrator
-- change that; the administrators keep their access through azure_pg_admin.
\set ON_ERROR_STOP off
GRANT CONNECT ON DATABASE postgres TO azure_pg_admin;
REVOKE CONNECT ON DATABASE postgres FROM PUBLIC;
\set ON_ERROR_STOP on

SELECT format('Database login monthwise_app is ready (create database: %s, create role: %s, superuser: %s).',
              rolcreatedb, rolcreaterole, rolsuper)
  FROM pg_roles WHERE rolname = 'monthwise_app';
