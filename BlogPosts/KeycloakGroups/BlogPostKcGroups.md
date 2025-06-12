KeyCloak CLI adding user to group without sed

The KeyCloak CLI, kcadm.sh, is great when you script the creation of ad hoc environments, for instance local dev environments you can play with on the plane or train.

However, at times it can be frustrating.

For instance it is easy to create a realm:

```sh
echo "==> Configuring admin connection"
/opt/keycloak/bin/kcadm.sh config credentials \
    --server http://localhost:8088 \
    --realm master \
    --user $KC_BOOTSTRAP_ADMIN_USERNAME \
    --password $KC_BOOTSTRAP_ADMIN_PASSWORD
echo "==> Disabling SSL Required on master realm, because source of noise for local dev use"
/opt/keycloak/bin/kcadm.sh update realms/master \
    --set sslRequired=NONE
echo "==> Creating demo realm"
/opt/keycloak/bin/kcadm.sh create realms \
    --set realm=demo_realm_name \
    --set enabled=true \
    --set sslRequired=NONE \
    --output
```

And creating a client is also straight forward:

```sh
echo "==> Creating demo oauth client registration in demo realm"
/opt/keycloak/bin/kcadm.sh create clients \
    --target-realm demo_realm_name \
    --set id="demo_client_id" \
    --set clientId=demo_client_name \
    --set publicClient="true" \
    --set "redirectUris=[\"*\"]" \
    --set "webOrigins=[\"*\"]" \
    --set directAccessGrantsEnabled=true \
    --set enabled=true \
    --output
echo "===> Creating client scope openid needed by legacy client"
/opt/keycloak/bin/kcadm.sh create client-scopes \
    --target-realm demo_realm_name \
    --set id="openid_scope_id" \
    --set name="openid" \
    --set protocol=openid-connect \
    --set 'attributes."include.in.token.scope"=true' \
    --output
echo "==> Adding scope openid to client demo_client_name"
/opt/keycloak/bin/kcadm.sh update \
    clients/demo_client_id/default-client-scopes/openid_scope_id \
    --target-realm demo_realm_name
```

As is creating a user:

```sh
echo "==> Creating demo user demo-user in demo realm demo_realm_name"
/opt/keycloak/bin/kcadm.sh create users \
    --target-realm demo_realm_name \
    --set username=demo-user \
    --set enabled=true \
    --set emailVerified=true \
    --set "email=demo-user@example.com" \
    --set "firstName=demo-user Given" \
    --set "lastName=demo-user Family" \
    --output
echo "==> Setting demo password for demo-user in realm demo_realm_name"
/opt/keycloak/bin/kcadm.sh set-password \
    --target-realm demo_realm_name \
    --username demo-user \
    --new-password password
```

Creation of a group similarly poses no challenges:

```sh
echo "==> Creating demo group in demo realm"
/opt/keycloak/bin/kcadm.sh create groups \
    --target-realm demo_realm_name \
    --set name=demo-group \
    --output
```

Actually it's quite similar to how we'd create a realm role:

```sh
echo "==> Creating demo role in demo realm"
/opt/keycloak/bin/kcadm.sh create roles \
    --target-realm demo_realm_name \
    --set name=demo-realm-role \
    --output
```

Or a client role:

```sh
echo "==> Creating demo role in demo client in demo realm"
/opt/keycloak/bin/kcadm.sh create \
    clients/demo_client_id/roles \
    --target-realm demo_realm_name \
    --set name=demo-client-role \
    --set "description=Custom client role" \
    --output
```

However, this is where the similarities end.
While adding the roles to a user is quite simple:

```sh
echo "==> Add realm role to user"
/opt/keycloak/bin/kcadm.sh add-roles \
    --target-realm demo_realm_name \
    --uusername "demo-user" \
    --rolename demo-realm-role
echo "==> Add client role to user"
/opt/keycloak/bin/kcadm.sh add-roles \
    --target-realm demo_realm_name \
    --uusername "demo-user" \
    --cclientid demo_client_name \
    --rolename demo-client-role
```

We are not as fortunate when wanting to add a group.
At the time of writing, I found no comparable option for adding a group, which would have allowed us to at pass in the user name and group name.
Instead, the solution seems to be invoking `/opt/keycloak/bin/kcadm.sh update "users/<userid>/groups/<groupid>" --target-realm ...`.
And, unlike the clients, KC is not happy when we try to manually pass `--set id=<predefined>` when creating users or groups.

Fair enough you say.
Divining the users ID is as simpe as making a call to `/opt/keycloak/bin/kcadm.sh get users ...`, and similarly `/opt/keycloak/bin/kcadm.sh get groups ...` for the groups ID.

But here the fun begins.
Because, by default, for both of these calls you get a json-object when retrieving the resource.
And somewhere deep inside, there will be a line like `"id": "<uuid>"`, if you're lucky.
So, at this point you'll say, "Great, no big deal, the KC oci/docker image ships with `sed`, we'll just spend some time figuring out we can pass the output to `sed -n 's|.*"id"\s*:\s*"\([^"]*\)".*$|\1|p'` and be on our way!"

After staring at the horror you've created for a short while after confirming that it works, you'll swiftly conclude that there has to be a better way than parsing json with regex to get where we want to go.
And fortunately there is!

For both the resources we care about, you can add the parameters `--format "csv"` and `--noquotes` to `kcadm.sh get`.
Combined with the `--fields "id"` parameter, you will actually get just a single string out if you pass in the right query!

So, in practice, you'll end up with something looking more or less like this:

```sh
echo "==> Fetching ID of demo user in demo realm"
ID_USER_TO_ADD=$(/opt/keycloak/bin/kcadm.sh get users \
    --target-realm demo_realm_name \
    --query "exact=true" \
    --query "username=demo-user" \
    --fields "id" \
    --format "csv" \
    --noquotes)

# Beware querying `search` not `name`, because interesting API design decisions: https://github.com/keycloak/keycloak/issues/19138#issuecomment-1886819021
echo "==> Fetching ID of demo group in demo realm"
ID_GROUP_ADDING_TO=$(/opt/keycloak/bin/kcadm.sh get groups \
    --target-realm demo_realm_name \
    --query "exact=true" \
    --query "search=demo-group" \
    --fields "id" \
    --format "csv" \
    --noquotes)

echo "Adding user \"demo-user\" (ID: \"${ID_USER_TO_ADD}\") to group \"demo-group\" (ID: \"${ID_GROUP_ADDING_TO}\") in realm \"demo_realm_name\""
/opt/keycloak/bin/kcadm.sh update \
    "users/${ID_USER_TO_ADD}/groups/${ID_GROUP_ADDING_TO}" \
    --target-realm demo_realm_name \
    --set "userId=${ID_USER_TO_ADD}" \
    --set "groupId=${ID_GROUP_ADDING_TO}" \
    --no-merge
```

The only thing missing then is adding a custom mapper, so that your test tokens can have the fields `roles: ["a","b"]` and `groups: ["x","y"]` populated with the info like your application expects:

```sh
echo "==> Creating group mapper for demo client so that groups gets included in tokens"
/opt/keycloak/bin/kcadm.sh create \
    clients/demo_client_id/protocol-mappers/models \
    --target-realm demo_realm_name \
    --set "name=groups" \
    --set "protocol=openid-connect" \
    --set "protocolMapper=oidc-group-membership-mapper" \
    --set "config.\"full.path\"=false" \
    --set "config.\"introspection.token.claim\"=true" \
    --set "config.\"multivalued\"=true" \
    --set "config.\"id.token.claim\"=true" \
    --set "config.\"access.token.claim\"=true" \
    --set "config.\"userinfo.token.claim\"=true" \
    --set "config.\"claim.name\"=groups" \
    --set "config.\"jsonType.label\"=string" \
    --output
echo "==> Creating role mapper for demo client so that roles gets included in tokens"
/opt/keycloak/bin/kcadm.sh create \
    clients/demo_client_id/protocol-mappers/models \
    --target-realm demo_realm_name \
    --set "name=roles" \
    --set "protocol=openid-connect" \
    --set "protocolMapper=oidc-usermodel-realm-role-mapper" \
    --set "config.\"introspection.token.claim\"=true" \
    --set "config.\"multivalued\"=true" \
    --set "config.\"id.token.claim\"=true" \
    --set "config.\"access.token.claim\"=true" \
    --set "config.\"claim.name\"=roles" \
    --set "config.\"jsonType.label\"=string" \
    --output
```

Which you now can veryfy by running:

```sh
curl --request POST \
    --url "http://localhost:$KC_HTTP_PORT/realms/demo_realm_name/protocol/openid-connect/token" \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data client_id=demo_client_name \
    --data username=demo-user \
    --data password=password \
    --data realm=demo_realm_name \
    --data grant_type=password
```

Share and enjoy!
