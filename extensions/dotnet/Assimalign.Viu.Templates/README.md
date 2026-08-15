# Viu project templates

`Assimalign.Viu.Templates` installs two `dotnet new` templates for the current Viu SDK and
shared-framework package model ([V01.01.12.04]):

| Short name | Output |
| --- | --- |
| `viu-app` | A trimmed Browser WebAssembly application using `Assimalign.Viu.Sdk.Browser`, with a compiled reactive counter component. |
| `viu-lib` | A packable host-neutral component library using `Assimalign.Viu.Sdk`, with a generated component-catalog test project. |

Install and list the templates:

```sh
dotnet new install Assimalign.Viu.Templates
dotnet new list viu
```

Create projects:

```sh
dotnet new viu-app --name HelloViu
dotnet new viu-lib --name Contoso.Components
```

The application template pins its development origin to `http://127.0.0.1:51235` in
`Properties/launchSettings.json`. Keeping that URL stable lets a browser connected through
`dotnet watch` follow a rebuild/restart automatically. To use another port, change the active
profile's `applicationUrl` to an unused fixed port before starting the watch session.

Add a dual-target component assembly and an ASP.NET server host to the application with `--ssr`:

```sh
dotnet new viu-app --name HelloViu --ssr
dotnet run --project HelloViu/Server/HelloViu.Server.csproj
```

The Browser project references the generated component assembly, while the server host explicitly
registers its generated component and server-render catalogs. No runtime assembly discovery is used.

Both templates enable nullable reference types by default. Pass `--nullable false` to disable them:

```sh
dotnet new viu-lib --name Contoso.Components --nullable false
```

The application is ready for a normal trimmed publish. Ahead-of-time compilation remains an
explicit publish choice because it is substantially slower and requires the `wasm-tools` workload:

```sh
dotnet publish -c Release -p:RunAOTCompilation=true
```

The server variant declares `ViuServerRendering=true` on its base-SDK projects, as specified by
`[SSR-TARGET-1]` through `[SSR-TARGET-3]` and delivered by [V01.01.12.28].
