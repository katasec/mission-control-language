-- Runs once, on first container init (empty data volume), as the postgres superuser.
-- Owns: both local databases (forge_rooms, authbilling_db), the least-privilege app
-- role, and extensions (none needed — postgres:16 has gen_random_uuid() built in).
-- Tables onward are owned elsewhere: EF Core migrations for forge_rooms, and
-- AuthBillingSchema's idempotent CREATE TABLE IF NOT EXISTS for authbilling_db.

CREATE ROLE forge_app WITH LOGIN PASSWORD 'forge_app_dev' NOSUPERUSER NOCREATEDB NOCREATEROLE;

CREATE DATABASE forge_rooms OWNER postgres;

GRANT CONNECT ON DATABASE forge_rooms TO forge_app;

\connect forge_rooms

-- The app role applies EF migrations in dev, so it needs CREATE on the schema;
-- it still cannot create databases or roles.
GRANT USAGE, CREATE ON SCHEMA public TO forge_app;

CREATE DATABASE authbilling_db OWNER postgres;

GRANT CONNECT ON DATABASE authbilling_db TO forge_app;

\connect authbilling_db

GRANT USAGE, CREATE ON SCHEMA public TO forge_app;
