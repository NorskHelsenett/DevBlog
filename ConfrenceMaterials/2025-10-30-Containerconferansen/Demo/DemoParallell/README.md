# What

A simple distributed dict/crud api.
Make feature toggles for:
    inbox storage backing: in-mem, slqite in-mem, slite on disk
    inbox shipping: async, sync
    outbox storage backing: in-mem, slqite in-mem, slite on disk
    outbox hydration: async, sync

run at least 6 instances, so that each configuration of each component is present at least once
    maybe can do with as few as 3?

# Why

Gives multi component system to observe, with observable (measurable) differences, while being fairly simple (yet not completely trivial)
