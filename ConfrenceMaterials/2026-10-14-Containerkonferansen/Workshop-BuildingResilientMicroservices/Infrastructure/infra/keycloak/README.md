Keycloak setup in kind cluster

# Future work

~~- Prefix manifest filenames with `manifest-`~~
- Consider makig better names for client variables, to it's clearer that clients `ID` is not same as `clientid`, and perhaps reduce confusion surface arising from `clientid` being reused as clients `name`.
~~- Make setup script run as separate "provisioner job", or at least init container, so that it can sanely be waited for~~
- Drag out the portion with "kafka tenant provisioning" as separate job, or at least function within the script.
- Consider making curl variant of provisioning work
  - Has to run from somewhere though, and inside same kubernetes cluster as keycloak itself seems like the best choice for most conceivable scenarios
  - relies on extra cli tooling like yq that might need to be installed, which is not needed when running kcadm.sh like now inside the keycloak image
