# RazorVue.Authoring

This is the smallest standalone Blazor-first RazorVue sample in the repository. It uses
ordinary Razor authoring with typed TDesign controls for a task-board CRUD flow:

- `TForm<TaskDraft>` and typed `TInput<string>` with `@bind-Value`
- `TForm<TaskDraft>` field rules with typed validation and reset callbacks
- `TPrimaryTable<TaskRow>` with typed columns, custom cell render fragments, and `CellEmptyContent` context
- C#-side `TPrimaryTableColCell<TaskRow>` render fragment with typed row projection
- `OnRowClick` event payload projected into reactive component state
- async form/dialog callbacks, writable `[Inject] NavigationManager`, and a named
  `[CascadingParameter]`
- `@page "/"` and `@page "/tasks"` through the existing RazorVue route catalog
- internal push/replace navigation with `NavigationOptions.ReplaceHistoryEntry`,
  `HistoryEntryState`, and `LocationChanged` subscription/disposal

## Build and verify

From the repository root, prepare the candidate packages and build the sample with an isolated NuGet cache.

~~~text
dotnet run --file scripts/csharp/pack-release.cs
dotnet run --file samples/RazorVue.Authoring/verify-smoke.cs -- --configuration Release --package-output artifacts/packages
~~~

The verification checks the Debug modules, source maps, Release bundle, and packaged runtime resources, then drives the SPA in Chrome.
To include the browser mount in the same gate, omit `--skip-browser` (the default). Google Chrome
is discovered automatically; set `RAZORVUE_BROWSER_EXE` or
`RAZORVUE_BROWSER_PATH` to choose a specific executable.

The browser journey runs below `/authoring/` to keep the PathBase contract visible. The sample
uses `Navigation.BaseUri` for the replace target, verifies that replacing the current route does
not grow `history.length`, and renders the received URI, `HistoryEntryState`, and
`LocationChanged` count. The contract is limited to same-base internal navigation; external
URIs, `forceLoad`, server circuits, SSR/prerender route identity, and cancellable
`popstate`/`hashchange` handling remain outside this sample.

## Authoring boundary

The sample intentionally contains no `BuildRenderTree`, `RenderTreeBuilder`, historical bridge
components, application-side casts, `object` escape hatches, handwritten JavaScript, or
`IJSRuntime`. It also does not use Microsoft/Blazor built-in UI components or recreate
`Router`, `RouteView`, `LayoutView`, or `NavLink`. `Bootstrap` owns only the Vue mount framing;
RazorVue and `Jazor.Emit` own expression lowering, route-catalog generation, module/source-map
materialization, and Release bundling.
