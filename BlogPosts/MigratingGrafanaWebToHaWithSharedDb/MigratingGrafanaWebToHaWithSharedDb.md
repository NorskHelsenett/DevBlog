Migrating Grafana Web from Single Instance to HA with DB

Here is a write-up of what we learned migrating from single instance grafana using local sqlite to HA grafana backed by PG.

# Background

Our Grafana usage reached a point where getting kubernetes to vertically autoscale the instance with more CPU and RAM was starting to become painful considering our node sizes and capacity. Worse, our users were getting cranky when we did upgrades or kubernetes shuffled things around in our observability cluster, because "The observability stack is down!"

Figuring out that our time was better spent herding kittens in kubernetes than regulating feelings ("how many people don't receive emergency healthcare and die because you can't marvel at your dashboard while grafana rids it'self of yesterdays CVEs?"), we reached for the obvious, simple, and easy solution; Migrate Grafana to a multi instance HA setup!

After reading several interpretations on how this could be done, we settled on mostly following this guide:

https://community.ibm.com/community/user/blogs/hiren-dave/2025/06/19/a-battle-tested-guide-migrating-grafana-from-sqlit

The essence is this:

- Extract the SQLite database from the single instance running pod's sotrage.
- Obtain a shared PG database that all your Grafana instances can reach.
- Migrate the PG database
- Use the [pgloader](https://github.com/dimitri/pgloader) utility to fill the database with your old data (dashboards, organizations, users, etc)
- Spin up new pods using the new database for storage and kill the old grafana instance

With the expected amount of smoothness of swapping out the innards of a solution in flight, here follows a summary of the things we learned about what didn't work the way we hoped.

# Learnings

Should you use cloud native pg to obtain a pg db form a pg cluster, and you anticipate need for more than 1 db ever, using the `Database` resource like shown below can be more manageable than relying on specifying it in the `initdb` config section of the cnpg chart:

```yaml
{{- /* https://cloudnative-pg.io/docs/1.28/declarative_database_management/ */}}
apiVersion: postgresql.cnpg.io/v1
kind: Database
metadata:
  name: grafana-db
  namespace: the-pg-cluster-ns
spec:
  name: grafanadb
  owner: grafanauser
  cluster:
    name: the-pg-cluster-ns
```

The collection of roles is still be nicely manageable in the cnpg chart directly, it dynamically reloads and makes the roles available:

```yaml
    managed:
      roles:
        - comment: The grafanaest of users
          connectionLimit: -1
          ensure: present
          inRoles: []
          inherit: true
          login: true
          name: grafanauser
          passwordSecret:
            name: secret-grafana-db
          superuser: false
```

Using the external secrets operator works well to ensure the secret exists. Order of creation doesn't matter, as postgres respects the `cnpg.io/reload: "true"` attribute on the resulting kubernetes secret-resource.

```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: create-db-pw-and-storage-secret
  namespace: the-pg-cluster-ns
spec:
  # refreshInterval: 1h
  refreshPolicy: CreatedOnce
  secretStoreRef:
    name: eso-cluster-secret-store
    kind: ClusterSecretStore
  target:
    name: secret-grafana-db # Name of created/materialized secret
    creationPolicy: Owner
    template:
      type: kubernetes.io/basic-auth
      metadata:
        labels:
          cnpg.io/reload: "true" # Makes PG update the users password when the created secret changes.
      data:
        username: 'grafanauser'
        password: '{{"{{ .retrievedpw }}"}}'
  data:
    - secretKey: retrievedpw # lookup name used in *target* secret
      remoteRef:
        key: generated-grafana-db-secret # existing secret in secret store
        property: generatedpw # key inside that secret
```

Grafana sets up it's own schema if you spin up new instance and point it at empty PG DB.

PGloader of export of old db complains about sqlite schema type vs pg schema type differences - can be (apparently safely) ignored.

PGloader can't handle default entries like admin user and main org existing in tables and giving conflicts. Solution is to truncate content of all ≈84 tables.

To get names of tables run

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema='public'
AND table_type='BASE TABLE'
```

To truncate all run

```sql
TRUNCATE
"alert_configuration_history",
"alert_configuration",
...
"user_role",
"user"
CASCADE;
```

To extract sqlite db from grafana (ToDo: Add note about command format, or at least point out that the names/uids are uninteresting, the structure is what to care about):

```sh
kubectl cp grafana/grafana-68c77684fc-7mc75:/var/lib/grafana /temp/grafana-local-storage
```

If stuck running pgloader in pod, copy extracted db to pvc mounted to pod:

```sh
tar cf - ./grafana.db | kubectl exec -i -n cn-pg-cluster-name pgloader-5dff9c7cd8-56ckq -- tar xf - -C /grafanamigration/grafana.db
```

Create pgloader command and put in file:

```sh
LOAD DATABASE
  FROM sqlite://./grafana.db
  INTO postgresql://grafanauser@cn-pg-cluster-name-rw:5432/grafanadb
WITH data only, reset sequences,
    workers = 8, concurrency = 1,
    batch concurrency = 1

SET PostgreSQL PARAMETERS
    maintenance_work_mem to '10248MB',
    work_mem to '512MB'
;
```

Note that grafana DB can be big, when small the `WITH data only, ...` part might not be needed, but for some environments couldn't get it to run without.

If running pgloader in pod, copy created pgloader command file to pvc mounted to pod:

```sh
tar cf - ./pgloader-command-file | kubectl exec -i -n cn-pg-cluster-name pgloader-5dff9c7cd8-56ckq -- tar xf - -C /grafanamigration/
```

Put PG password in env var to avoid weird breakage due to symbols:

```sh
export PGPASSWORD='the pw with weird symobls and things'
```

Disconnect everything from the DB, like pgadmin.

Execute the pgloading:

```sh
pgloader ./pg-loader-command-file
```

Watch warnings scroll by. In table printed at end, enjoy a print of sum of zero total errors.
(Actually was an error or two in one env in the cache_data table or something, but turned out to work fine, and 0 broken sessions wasn't a goal for our migration, so you have maybe verify if it works for you yourself.)

# Links and resources

- https://community.ibm.com/community/user/blogs/hiren-dave/2025/06/19/a-battle-tested-guide-migrating-grafana-from-sqlit
- https://medium.com/@yashraj45dighe/migrate-grafana-sqlite-db-to-postgresql-for-high-availability-c7303c3750ff
- https://community.grafana.com/t/migrating-from-sqlite-to-postgres/110408/4
- https://grafana.com/docs/grafana/latest/setup-grafana/set-up-for-high-availability/
- https://grafana.com/docs/grafana/latest/setup-grafana/configure-grafana/#database

## ESO docs
- https://external-secrets.io/main/guides/ownership-deletion-policy/

## PgLoader
- Project: https://github.com/dimitri/pgloader
- Docs: https://pgloader.readthedocs.io/en/latest/
- OciImage: https://hub.docker.com/r/dimitri/pgloader
