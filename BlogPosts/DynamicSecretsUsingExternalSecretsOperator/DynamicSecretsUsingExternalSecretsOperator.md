Secrets good.
Kubernetes good.
Dynamic secrets good.
Vaults good. Make extracting and editing easy/hard to do very wrong.
Putting secrets more places than needed not good. What about dynamic secrets only needed within the cluster?
Dynamic secrets natively in kubernetes using helm eh when using argo (link issue?).

If ESO usable as external dependency, can we use ESO without the unneeded external component?
Actually yes, even though documentation mostly guides you through other variants. I.e. manage a collection within a cluster not focused on and not very straightforward.

Need ESO in kubernetes. Easy and well documented, look up the install yourself. Maybe put argo app example here though? Nah.
ESO needs permissions to create, view, and edit secrets for our approach to work.
Our context doesn't need restricted access to secrets between namespaces for security, namespacing purely for manageability.
Therefore make kubernetes cluster role for ESO with full access to all secrets in cluster for ESO to use:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: eso-store-role
  namespace: external-secrets-operator
rules:
  - apiGroups: [""]
    resources:
      - secrets
    # "Get", "list", and "watch" are needed for read access
    verbs:
      - get
      - list
      - watch
      - create
      - update
      - patch
      - delete
  # This will allow the role `eso-store-role` to perform **permission reviews** for itself within the defined namespace:
  - apiGroups:
      - authorization.k8s.io
    resources:
      - selfsubjectrulesreviews # used to review or fetch the list of permissions a user or service account currently has.
    verbs:
      - create # `create` allows creating a `selfsubjectrulesreviews` request.
```

For ESO to use role, need account to assign role to:

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: eso-service-account
  namespace: external-secrets-operator
```

Once have role and account, need to glue them together:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: bind-eso-store-role-to-eso-service-account
  namespace: external-secrets-operator
subjects:
  - kind: ServiceAccount
    name: eso-service-account
    namespace: external-secrets-operator
roleRef:
  kind: ClusterRole
  name: eso-store-role
  apiGroup: rbac.authorization.k8s.io
```

After permissions established, can create SecretStore storage place that can be used to put generated secrets in to, and fetched from when creating secrets based on those stored:

```yaml
# secretstore-cluster-k8s.yaml
apiVersion: external-secrets.io/v1
kind: ClusterSecretStore
metadata:
  name: eso-cluster-secret-store
  namespace: external-secrets-operator # any ns; the store can target multi‑svcAccount
spec:
  provider:
    kubernetes:
      remoteNamespace: eso-secret-storage
      # Using a serviceAccount that can read the source secret
      auth:
        serviceAccount:
          name: eso-service-account
          namespace: external-secrets-operator
      server:
        url: kubernetes.default
        caProvider:
          # By magiv exists by default
          type: ConfigMap
          name: kube-root-ca.crt
          namespace: external-secrets-operator
          key: ca.crt
```

Note in this in cluster setup, eso store is only a namespace with secrets. No actual need to push things at store, every secret in namespace will be possible to extract with reference to store. But works better to push should you ever wish to expand to external resting place.

Need ESO generator to dynamically generate things, for instance password-like things:

```yaml
apiVersion: generators.external-secrets.io/v1alpha1
kind: Password
metadata:
  name: eso-app-restriction-specific-password-generator
  namespace: eso-secret-storage
spec:
  length: 42
  digits: 5
  symbols: 5
  symbolCharacters: "-_$@"
  noUpper: false
  allowRepeat: true
```

Note generator needs to be in namespace where externalsecret/pushsecret that does the generation is.

Beware structure of push secret. `SecertStoreRef: {}` in our case refers to the ClusterSecretStore, which is the one determining which namespace generated data is stored in. `selector: {}` used to obtain secret data to stuff in store. `data: {}` determines where in the secret store the new secret is stored (in our case in a secret specified in `remoteKey: ""`, under key specified in `property`).

Example

```yaml
apiVersion: external-secrets.io/v1alpha1
kind: PushSecret
metadata:
  name: pushsecret-app-password
  namespace: eso-secret-storage # Same NS as generator resource
spec:
  # refreshInterval: 6h0m0s # Doesn't work
  # updatePolicy: IfNotExists # Doesn't work
  refreshInterval: "0"
  secretStoreRefs:
    - name: eso-cluster-secret-store
      kind: ClusterSecretStore
  selector:
    generatorRef:
      apiVersion: generators.external-secrets.io/v1alpha1
      kind: Password
      name: eso-app-restriction-specific-password-generator # Matches generator name defined above
  data:
    - match:
        secretKey: password # property in the generator output
        remoteRef:
          remoteKey: app-password-secret # Remote reference (where the secret is going to be pushed)
          property: app-password-secret-key # the property to use to push into
```

Note `refreshInterval: "0"`. Not all things happy with changing secrets. Figuring out if using reloader operator (todo link) or similar viable for your app out of scope here.

Alternative is just dumping new secret in store namespace like this:

```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: eso-app-password
  namespace: eso-secret-storage # where you want the new secret, has to match bot store and generator ns using this apporach
spec:
  # refreshInterval: 1h
  refreshPolicy: CreatedOnce
  target:
    name: app-password-secret
    creationPolicy: Owner
    template:
      data:
        app-password-secret-key: '{{"{{ .password }}"}}'
  dataFrom:
    - sourceRef:
        generatorRef:
          apiVersion: generators.external-secrets.io/v1alpha1
          kind: Password
          name: eso-app-restriction-specific-password-generator
```

Again note `refreshPolicy` ensuring no autorotation. Also be amazed at escaping syntax for accessing property named `password` outputted from generator.

Once secret is stored, can be made available for use. Is done like shown below:

```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: eso-create-secret-for-use-by-app
  namespace: where-app-lives # where you want the new secret
spec:
  # refreshInterval: 1h                     # how often to re‑read
  refreshPolicy: CreatedOnce
  secretStoreRef:
    name: eso-cluster-secret-store
    kind: ClusterSecretStore
  target:
    name: app-credentials
    creationPolicy: Owner
    template:
      # ToDo: consider expanding on metadata if using e.g. pg together with other app?
      type: kubernetes.io/basic-auth
      metadata:
        labels:
          cnpg.io/reload: "true"
      data:
        username: 'apps-username-for-instance'
        password: '{{"{{ .retrievedpassword }}"}}'
        connectionString: 'you would put our secret {{"{{ .retrievedpassword }}"}} in a thing like this?'
  data:
    - secretKey: retrievedpassword # key in *target* secret
      remoteRef:
        key: app-password-secret
        property: app-password-secret-key
```

Share and enjoy!
