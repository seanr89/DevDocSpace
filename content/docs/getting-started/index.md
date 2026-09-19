---
title: Getting Started
---

# Getting Started

Welcome to DevDocSpace. This portal hosts documentation and interactive API references for internal services.

## Browsing docs

Use the sidebar to navigate namespaces. Each namespace maps to a service or team and is published from that service's repository.

## Calling an API

Open **APIs** in the navigation, pick a service and version, choose an environment (Sandbox, Staging, Production) and use **Try it out**. Requests are routed through the portal proxy, which injects the correct upstream credentials for you.

```bash
curl -H "X-Api-Key: dds_..." https://portal.example.com/api/v1/proxy/petstore/v1/sandbox/pets
```
