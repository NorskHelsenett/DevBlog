Local Dev Environment in Kubernetes
===

# Background

Looking at the [What job interviews taught me about Kubernetes](https://notnotp.com/notes/what-job-interviews-taught-me-about-kubernetes/), I realized that the time had finally come for our platform setup for local dev to transition to Kubernetes. Our organization has been on Kubernetes for years, and almost all the dev teams have a solid amount of experience with Kubernetes as their primary target for running their applications. So given an external validation of the feeling that everything is moving to Kubernetes, both for the things we make ourselves, but also when reaching for someplace to spin up dependencies for our apps, it was time.

An important consideration beyond it feeling right, is that as the teams write their applications in Kubernetes, the manifests that come out at the end have to work when everything is glued together in Kubernetes. And while there are several test and playground environments, the feedback loop is often too long for them be good for validating that the component glue is possible and works well.

# What we're gonna set up

The full setup can be found here:

[](./DemoCode/)

Basically, it's a [KIND](https://kind.sigs.k8s.io/) (Kubernetes IN Docker) cluster, with some core services. To not make this into an insanely long textbook, this article will be mainly focusing on the aspects of the choices I find interesting to discuss or highlight. The full code is linked if you want to study it in depth, and the technologies themselves are easy enough to track down if you want to read about them individually or look into their strengths and weaknesses.

Components overview:
- KIND cluster
- Container registry (OCI Container registry running as separate Docker container)
- ArgoCD
- HaProxy Ingress (Kubernetes Gateway API Provider)
- External Secrets Operator
- Cloud Native Postgre
- Keycloak
- Kafka (Strimzi)
  - Schema registry (Apicurio)
- Kube-Prometheus/Prometheus Operator (Observability)
  - Prometheus
  - Alertmanager
  - Grafana
    - Kubernetes dashboards


# Limitations

The Kubernetes environment is intended as a playground for testing out programs that are being created. Which means it is note made with consideration for being a good infrastructure setup sandbox from the operations perspective. If you want to play with how the different operators used interact at scale for instance, the chosen setup will likely feel bad and annoying. For instance the kafka cluster is being run with several brokers, because that is relevant when testing the configuration of applications that use Kafka. Did we set up acks in a way that works for us? How do things work when we get more throughput and different brokers can be leads for different partitions? While on the other hand, basically no custom software cares about how many instances of the schema registry run behind the load balancer to achieve better uptime, so the schema registry is left in a basic sing pod deployment to conserve battery resources, uptime be damned.

Properly working certificates/tls on localhost for the developer was also cut from the scope. It is to annoying to make it work well cross platform in a work setting with people having varying experience levels with their operating systems but also limited access to mess with things. Looking away from the big scary banners in the browser about the Keycloak login page form being served over http beats playing an [osu!](https://en.wikipedia.org/wiki/Osu%21)-minigame to defeat the untrusted ad hoc generated certificate warnings.

The startup time is slow as molasses. The observant reader will notice that this kinda goes against the stated purpose of shorter feedback loops. What saves us is that unless you do funky things, there should be no need to constantly wipe the dev cluster to un-tangle your app and its dependencies continuously. The intended usecase is to fire it up when you start your machine and plod though your new mail and dms, or do something productive like getting the first coffee of the day.

The Keycloak realm, user, and client setup is slow and likely intimidating for new teams and members to expand on. I've tried to keep it ergonomic and as gentle as possible, but it is what it is. Deal with it. Or use the Keycloak web UI I guess.

No organic beings were harmed in the production of this piece (although animals of the human kind were used, so can't really call it vegan), however, the keen student of the code will perceive the traces of LLMs in the scripts and manifests. In the end the baked in outdated information in the LLMs about the various projects config likely slowed down the progress more than it sped it up, despite the occasional flushing of a writers block arising from the dread of tackling a new subcomponent. Where they really shone were making the scripts more POSIX compliant (Windows users, here is where you give up on the 1990s and embrace wsl for your shell). So if you're allergic you've been warned. (Also there are a lot of "verbose" comments sprinkled around, which I find that I like, seeing as I will probably be the only one maintaining this setup, but probably won't have much time to do it beyond the couple of times a year something catastrophically breaks. And in that situation, a lengthy comment explaining what the rarely (by me) seen tower of shell-pipes is meant to achieve, and how it technically should be working, is kinda very nice.)

# The environment setup

## KIND

The possibly familiar rationale: Teams building containerized apps need Docker, or at least something Docker-esque to test that their code builds and manages to start. Hence mostly anyone in the target audience should have the permissions and ability to run it. And if it's good enough for testing and developing Kubernetes itself, then it should do fine for checking out apps.

Due to wanting to do some light distributed systems testing, it's set up with 3 Kubernetes nodes.

For ease of use, the setup enables and encourages use of node ports instead of or in addition to the gateway setup discussed later. For instance ArgoCD is set up with both a *node port* attaching it's service directly to a port on the KIND host Kubernetes node/Docker container, and with a HTTPRoute (set up in the gateway setup). This allows the developers to to `kubectl apply -f` or `helm install` their argo apps and interface with them directly, without having to care about the gateway setup, or even nuking the gateway completely should they figure it's an unnecessary drag on the resources. However, we figured mostly everyone would prefer a more readable and memorable address than a specific port on localhost, so we gave it the address http://argocd.localho.st:8080, in addition to another address on a dns zone we control ourselves.

Other than that nothing much interesting to say, except that mounting paths from the host for cross run persistence has been omitted until the first users that need it but can't set it up themselves comes along.

## Container registry

Because we not only want to check out chats of other peoples pre-made products and containers, but also our own charts, and images we only have and build locally while making new things, we need a place where Kubernetes can pull our own home-made images from. This is achieved by running a OCI Image Registry in Docker, so that we can tag images with it's local name on our dev machines and push to it, while we expose the container to the KIND Kubernetes cluster so that it can pull from it. We use the `registry:3` Docker image.

Note that while you can set up the `registry:3` containers to act as a local proxy for external images, to for instance save on bandwidth or be kind to the entities that enjoy rate limiting offices and conference venues, this would require one container per upstream registry you want to proxy, which makes no sense in our setup, where we already have viable internal proxies.

## ArgoCD

As we use a lot of [ArgoCD](https://argo-cd.readthedocs.io), and thus the teams write their app installers/charts for it, it was natural to include a deployment of it in the dev cluster. For the installation of ArgoCD itself, the choice fell on first downloading, and then applying the installation manifest. The alternatives would have been live fetching by pointing `kubectl` at a URL, or using helm. This approach however is neat for stability when working semi-offline, and is also nice for the partially intended workshop usage, where it gives greater chances of what worked on the dry run the day ahead still working the same. Not to mention that it gives some breathing room should the upstream decide to suddenly change up how they organize their charts and repositories completely.

## Gateway

[Kubernetes Gateways](https://kubernetes.io/docs/concepts/services-networking/gateway/) are nice, for several reasons. First of all, the new model of catering to infrastructure providers, cluster operations, and application developers separately, suits our purposes well. In many ways our KIND cluster can be seen as the cluster infrastructure and operation of it, allowing the developers to focus on how their app delivery fits into it.

It is also a very nice interface for exposing easily memorable addresses using our own DNS zones or publicly available zones and  solutions like localho.st and nip.io, that allow us to bind to arbitrary names like http://argocd.localho.st. The trick is mostly in pointing a subdomain named (having an A and AAAA record) `*` pointing to `127.0.0.1` (and/or `::1` for the IPv6 case).

Another good part is allowing for exposing non-http services easily. This is nice because it allows us to for instance admin Kafka directly from the dev machine using lightweight native tooling, as well as enabling creation of more diverse applications and capabilities.

As for the choice of technology, it ended up on [HAProxy Ingress](https://haproxy-ingress.github.io/) for the gateway. We first tried [NGINX Gateway Fabric](https://docs.nginx.com/nginx-gateway-fabric/get-started/#set-up-a-kind-cluster), but it proved to require too much manual intervention to get up and running in a KIND context for it to be worth the maintenance hassle. Next up on the chopping block was [Project Contour](https://projectcontour.io/). Having a strong legacy as an ingress provider as well, it seemed like a good choice for 2026, now that gateway has just recently arrived, and most manifests and charts are still written with ingress in mind. However, it turned out to be quite brittle in our KIND-world as well, due to the reliance on an external load balancer. Throwing [MetalLB](https://metallb.io/) into the mix kinda worked, but would have been painful for similar reasons as having the development teams setting entries manually in the hosts-files in addition to everything else they're tasked with.

HAProxy Ingress was nice and easy to get working in KIND. It was just a matter of installing the generic Kubernetes gateway API custom resource definitions, creating the namespace, and throwing in the HAProxy Ingress helm chart. The only snag was that the helm chart proved resilient against being rendered and then `kubectl apply`ed instead of being thrown in using `helm upgrade`. However, after setting it up and pushing in a HTTPRoute for ArgoCD to verify it was working, we could move on!

## External Secrets Operator

[External Secrets Operator](https://external-secrets.io) is an interesting component. In some senses, it is not needed. Many can make do with using Kubernetes secrets directly, and others again use vault solutions like [OpenBao](https://openbao.org/). So in some sense it it's a waste of constrained resources. On the other hand, some teams use it, and it's value proposal of managing secrets which are shared between different components that need to be the same, but exist packaged differently, and perhaps across different namespaces, is great for us.

The only real downside is that it takes a bit of time for the operator to pick up on new `ExternalSecret`-resources it should use to materialize new Kubernetes secrets. And there is no built in way in Kubernetes to wait for secrets (or resources generally) coming into existence, unlike for say deployments we can block using `kubectl -n $ESO_NS wait --for=condition=available deployments/$RELEASE_NAME-external-secrets --timeout=40s`. So we have to write our own small utility to block until the secrets we need are available.

## Keycloak

The [Keycloak](https://www.keycloak.org/) setup you'll find can possibly seem less insane, perhaps even reasonable, with some explanation. "Why Keycloak" itself is not worth spending text on though; Having an [OAuth](https://en.wikipedia.org/wiki/OAuth) and [OIDC](https://en.wikipedia.org/wiki/OpenID#OpenID_Connect_(OIDC)) setup you can test with offline is an amazing productivity boost. And using a battle tested, state of the art solution, which you can later self host for free, is also clearly good.

Keycloak in Kubernetes is neat, in many ways it is a much smoother setup experience than in Docker. One point it breaks a bit down, is when you want to run with an address supplied by the gateway that only resolves locally, and you cant be bothered to stir around in the innards of computers owned and managed by someone else. Keycloak really wants to upgrade your connection to https while redirecting you to the next page in the login process. However, like when running local dev environments directly in Docker, it's easily resolved by setting the `KC_HOSTNAME` to the base address and port you'll be assigning like `http://keycloak.localho.st:8080`.

The second nice part with running in Kubernetes, is that unlike Docker, you can block progress until Keycloak has been completely configured in a sane way. The trick is starting the database, running the configuration as a job you await the completion off, and then continuing on. However, here is the part that needs some justification. The two most common ways of achieving this is either starting the server and then doing the config in the UI, or after starting the server doing http calls to the API using the bootstrap credentials. Of these, the curl approach is the generally most sane way of automating, as it is to some extent reusable in production.

Here the choice however was to use the `kcadmin.sh` cli that's built in to the Keycloak image to configure it, by creating a script, putting it in a config map, mounting it with the correct access settings, and executing it in the entrypoint/as the startup command. While it is a bit cumbersome, and unlikely to be as easy to reach for in a production setting as the rest-api for automation or custom tooling, it is neat because it doesn't drag in yet another tool in our already sluggish setup, keeping the memory weight marginally lower and process itself slightly more self container.

Another curiosity you might stumble upon and question, is why there is a plain postgresql deployment for the Keycloak backend when there is a perfectly serviceable [CloudNativePG](https://cloudnative-pg.io/) deployment within the cluster. The main reason for this is that it was supplied with the Keycloak basic get started example chart, and because persistence and robustness is not a concern for this local setup, it's a nice reminder of how simple it can actually be to get started. As for why not just stick to storage within the container/pod, it's so that we can run the setup jobs before starting serving, and also to enable experimentation with multiple instances.

## Kafka

For the [Kafka](https://kafka.apache.org/) deployment we went with [Strimzi](https://strimzi.io/), because it has effectively become the standard way to spin up Kafka if you have Kubernetes available; You'll find almost all community resources focusing on it, and it works well, and has for a long time.

The Kafka cluster is set up using OAuth backed by the Keycloak instance within the cluster. For discussion on how to set up Kafka with OAuth and the associated tradeoffs, read this excellent blogpost series by Tim&Koko:
https://tim-koko.ch/en/blog/strimzi-kafka-oauth-keycloak-authentication/

For the Schema Registry the choice has fallen on [Apicurio](https://www.apicur.io/registry/), because it's what we use, and the licensing currently being pure Apache-2.0 is great. Apicurio is set up using Kafka as the storage backend, which is good because we are setting it up for use with the Kafka cluster, so it makes sense that the storage persists for as long as the related Kafka cluster (while we don't need it to linger when the cluster dies).

## Observability

While I have [previously written about](https://medium.com/norsk-helsenett/scaling-local-observability-ca9249cd69a1) observability using the [OpenTelemetry Collector](https://opentelemetry.io/docs/collector/) and the [Grafana](https://grafana.com) LGTM stack locally, here the [Kube-Prometheus/Prometheus-Operator](https://github.com/prometheus-operator/kube-prometheus) project was chosen. First and foremost because there is some auxiliary tooling like alerting and scaling (looking at you [KEDA](https://keda.sh/)) which is much easier to get startet with using in-cluster prometheus instances. In the wild you'll also come across prometheus deployments in the clusters precisely for these use-cases as well. Secondly you get a lot Kubernetes dashboards in the Grafana instance out of the box, which is neat for teams exploring the impact of how they package their products for shipment and consumption.

Currently the most interesting aspects of this setup is perhaps the config for Grafana passed in the values.yaml file to the chart disabling login and giving anyone resolving the instance admin. The reasoning is that this is not meant for production but local use, so having a login here provides no utility, only friction.

# Final notes

Again, the complete setup can be found here:
[](./DemoCode/)

Share and enjoy!
