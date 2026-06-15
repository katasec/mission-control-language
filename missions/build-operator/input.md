# Task: Design a Kubernetes Build Operator

Design a Kubernetes operator that watches for `BuildRequest` custom resources and orchestrates
container image builds using Tekton pipelines.

## Requirements

- Watch for `BuildRequest` CRDs in any namespace
- Trigger a Tekton `PipelineRun` when a `BuildRequest` is created
- Update `BuildRequest` status as the build progresses (Pending → Building → Succeeded/Failed)
- Support configurable build timeouts (default: 30 minutes)
- Clean up completed `PipelineRun` resources after 24 hours
- Expose Prometheus metrics: build count, build duration, failure rate

## Constraints

- Must run with least-privilege RBAC — only the permissions it needs
- No persistent storage — all state lives in Kubernetes objects
- Must handle leader election for HA deployments
- Go 1.22, controller-runtime v0.17, Kubernetes 1.29+
