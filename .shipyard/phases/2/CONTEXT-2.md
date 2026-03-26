# Phase 2 Context: Update Transport Projects

## Decisions

- **NuGet scope:** Bump ALL DotNetWorkQueue.* packages to 0.9.11 (core, transport-specific, and Dashboard.Client)
- **Redis EnableHistory pattern:** Set `RedisBaseTransportOptions.EnableHistory` in the options lambda of QueueContainer, alongside `Injectors.SetOptions()`. Use the concrete type, not the interface. Pattern:
  ```csharp
  options => {
      Injectors.SetOptions(options, SharedConfiguration.EnableChaos);
      options.GetInstance<RedisBaseTransportOptions>().EnableHistory = SharedConfiguration.EnableHistory;
  }
  ```
- **Consumer History.Enabled removal:** Simply delete the `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;` lines. No replacement needed.
- **Dashboard.Api:** Excluded per project non-goals.
- **Parallelism:** The 5 transport solutions are independent and their plans can execute in parallel (same wave).
