// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

// The app's testable surface (data client, stores, route table, view models) is internal — an app
// exposes no public API — and the sibling test project drives it through the Testing renderer and
// memory-history router. Deliberately NO assembly-level [SupportedOSPlatform("browser")]: only the
// bootstrap (Program.Main) touches browser-only interop and is marked there, so every logic-bearing
// type stays platform-neutral and referenceable from the (host-run) test project without CA1416.
[assembly: InternalsVisibleTo("Assimalign.Viu.HackerNews.Tests")]
