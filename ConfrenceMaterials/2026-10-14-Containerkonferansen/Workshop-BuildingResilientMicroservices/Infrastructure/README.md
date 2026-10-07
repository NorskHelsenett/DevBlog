Local dev kubernetes cluster
===

# What

A kubernetes cluster you can run locally on your dev machine using KIND (Kubernetes IN Docker).
Contains various utilities that are generally usefull as well as ones we are known to use.
Components come pre-configured.

# Why

Over the last years we've gone from containerization being something we're looking into for most applications, to Kubernetes becoming the defacto standard for how to run software across clouds and our own hardware.

> "Just find the helm chart for the thing you need, and throw it in your Kubernetes cluster."

Where compose shines for checking that throwing your code into a container works in the sense that the container builds and starts, and some rudimentary dev variants of dependencies are usable by it, it fails to be a viable path to testing the next layer of product packaging for those that are going to use/operate the software.
Here a local kubernetes cluster is great for verifying the charts and manifests code needed to bootstrap the application code works at all.
Further, it's very nice to be able to check that the code starts, reports it's health as kubernetes needs, and is scalable in the various ways kubernetes provides for, without having to wait for the CI/CD system and time/space in a shared test cluster.

# How

Lots of scripts that set up various components.

Why not all through ArgoCd; And not even consistent between helm and raw manifest applications?
Use for workshops, relevance for situations where ArgoCd not present, exposure/experience for the devs beyond pure ArgoCd.
Workshops also reason for the approach with rendering manifests and refreshing them if older than given limit instead of just pointing to chart repository.
Gives stronger consistency for a controllable period.
Which is neat for dev teams too.

Why HaProxy Ingress as gateway?
Ease of setup for relevant use-case of testing out httproutes as world seems to be moving on from ingresses.
Projectcontour with Envoy was neat, but started to become brittle when MetalLB thrown into the mix to facilitate named address traffic from workspace on machine running kind.

Why Oci registry; I don't need no proxy, and you'd need one per upstream like gcr and quay anyways?
To be able to locally build images and test them in the cluster, without having to push to an upstream shared with others.

Why shell specific commands sprinkled in, couldn't this be proper cross platform by more or less only using `kubectl apply` and `kubectl wait`?
At the time of writing the people suffering windows can use wsl, or spin up an llm locally and have it translate everything to powershell or something.
