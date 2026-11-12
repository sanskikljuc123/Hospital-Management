-- Runs once, when the PostgreSQL data volume is first initialised.
-- The application database (hospitaldb) is created by POSTGRES_DB; Keycloak gets its own.
CREATE DATABASE keycloak;
