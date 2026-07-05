-- Runs once, on first container init (empty data volume), as the postgres superuser.
-- Owns: the database, the least-privilege app role, and extensions (none needed —
-- postgres:16 has gen_random_uuid() built in).
-- EF Core migrations own everything from tables onward.

CREATE ROLE forge_app WITH LOGIN PASSWORD 'forge_app_dev' NOSUPERUSER NOCREATEDB NOCREATEROLE;

CREATE DATABASE forge_rooms OWNER postgres;

GRANT CONNECT ON DATABASE forge_rooms TO forge_app;

\connect forge_rooms

-- The app role applies EF migrations in dev, so it needs CREATE on the schema;
-- it still cannot create databases or roles.
GRANT USAGE, CREATE ON SCHEMA public TO forge_app;
