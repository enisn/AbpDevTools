# `abpdev serve` - Web UI Feature Plan

## Overview

Add a web server feature to AbpDevTools that provides a browser-based UI for CLI functionality via `abpdev serve` command.

---

## Motivation

### Why a Web UI?

- **Crossplatform by default** - Runs in any browser on any OS
- **Zero additional distribution** - Uses existing CLI as package
- **Rich interaction potential** - Modern SPA with real-time updates
- **Remote access** - Can view/manage from other devices
- **Easy to test** - Try before committing to native desktop UI

### Use Cases

- Visual project/build management
- Logs viewer with real-time streaming
- Virtual environment configuration editor
- Local sources management UI
- Environment apps dashboard (start/stop databases)
- Notification history

---

## Architecture

### High-Level Design

```
┌─────────────┐
│   Browser   │ (Blazor Server or Static SPA)
└──────┬──────┘
       │ HTTP/WebSocket
       ▼
┌─────────────────────┐
│  ASP.NET Core Web   │ (Minimal API or Blazor Server)
│      Server         │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│  Existing CLI       │ (Reused Commands)
│    Commands         │
└─────────────────────┘
```

### Components

1. **Web Server** - Kestrel-based HTTP server
2. **API Layer** - REST endpoints or SignalR hub
3. **UI Layer** - Blazor Server or static files
4. **Command Bridge** - Adapters to call existing CLI commands

---

## Implementation Options

### Option A: Minimal API + Static UI (Recommended MVP)

```
Pros:
• Simplest to implement
• Lightweight overhead
• Flexibility in UI tech (React, Vue, vanilla JS)
• Easy to test

Cons:
• Need to build separate UI bundle
• No built-in reactivity
```

**Tech Stack:**
- ASP.NET Core Minimal APIs
- Static file serving for HTML/CSS/JS
- REST endpoints for commands
- Optional SignalR for real-time logs

**API Examples:**
```
GET  /api/projects          - List solutions/projects
POST /api/build              - Trigger build
GET  /api/logs/{project}    - Stream logs
GET  /api/env/config        - Get env config
PUT  /api/env/config        - Update env config
```

---

### Option B: Blazor Server (Recommended for Full Experience)

```
Pros:
• Full C# UI (no JS needed)
• Built-in SignalR for real-time
• Component-based architecture
• Rich ecosystem

Cons:
• Larger runtime footprint
• Requires Blazor knowledge
• Higher memory usage
```

**Tech Stack:**
- Blazor Server
- SignalR (built-in)
- Same C# backend as CLI
- Component library (MudBlazor, Telerik)

**Pages:**
- `/` - Dashboard
- `/build` - Build manager
- `/run` - Run projects
- `/logs` - Logs viewer
- `/env` - Environment configuration
- `/envapps` - Environment apps manager

---

### Option C: Blazor Hybrid + WebView

```
Pros:
• Can run as desktop app later
• Same code works in browser
• Native desktop features possible

Cons:
• More complex setup
• Larger distribution (if desktop)
```

---

## Feature Phasing

### Phase 1: MVP (Week 1-2)

**Goal:** Basic web server with project listing and build capability

- [ ] Add `abpdev serve` command
- [ ] Start Kestrel server on localhost:5000
- [ ] Serve static HTML page
- [ ] API endpoint: `GET /api/projects`
- [ ] API endpoint: `POST /api/build`
- [ ] Show build status/results in UI
- [ ] Auto-open browser on start
- [ ] Graceful shutdown on Ctrl+C

**Tech:** Minimal API + simple HTML/JS

---

### Phase 2: Enhanced Features (Week 3-4)

**Goal:** Add more commands and real-time features

- [ ] Add `GET /api/run` endpoint
- [ ] Add `GET /api/logs/{project}` endpoint
- [ ] Add `GET /api/env/config` endpoint
- [ ] Add `PUT /api/env/config` endpoint
- [ ] Implement logs streaming (SSE or SignalR)
- [ ] Better UI design (CSS framework)
- [ ] Configuration option: port number
- [ ] Configuration option: auto-open browser

**Tech:** Enhanced UI + real-time logs

---

### Phase 3: Full Blazor Experience (Week 5-6)

**Goal:** Rich SPA with full command coverage

- [ ] Migrate to Blazor Server
- [ ] Implement all CLI commands as UI features
- [ ] Real-time build/run status updates
- [ ] Virtual environments manager UI
- [ ] Local sources manager UI
- [ ] Environment apps dashboard
- [ ] Notification history
- [ ] Dark mode support

**Tech:** Blazor Server + MudBlazor

---

### Phase 4: Advanced Features (Week 7+)

**Goal:** Polish and power features

- [ ] Authentication/authorization
- [ ] Remote access (configurable bind address)
- [ ] WebSocket-based terminal output
- [ ] Project graphs/visualizations
- [ ] Settings persistence
- [ ] Theme customization
- [ ] Keyboard shortcuts
- [ ] Export/import configurations

---

## Technical Implementation

### 1. Command Line Integration

```csharp
// Existing command can be called programmatically
var buildCommand = new BuildCommand();
var result = await buildCommand.ExecuteAsync(context);
```

### 2. Project Structure

```
src/AbpDevTools/
├── Commands/
│   ├── BuildCommand.cs
│   ├── RunCommand.cs
│   └── ...
├── Web/
│   ├── Server.cs              (Web server startup)
│   ├── Api/
│   │   ├── ProjectsEndpoint.cs
│   │   ├── BuildEndpoint.cs
│   │   └── ...
│   ├── Pages/
│   │   └── (Blazor pages or static HTML)
│   └── Static/
│       └── (CSS, JS, images)
└── ServeCommand.cs            (New CLI command)
```

### 3. Configuration

```yaml
# abpdev.yml additions
web:
  enabled: true
  port: 5000
  autoOpen: true
  bindAddress: "localhost"  # or "0.0.0.0" for remote access
```

---

## Comparison with Desktop UI Approaches

| Aspect | Web Server | Avalonia | MAUI | WPF/WinUI |
|--------|------------|----------|------|-----------|
| **Implementation Time** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| **Crossplatform** | ✅ Native browser | ✅ All platforms | ✅ All platforms | ❌ Windows only |
| **Distribution** | ✅ Zero extra | ❌ Separate installer | ❌ Separate installer | ❌ Separate installer |
| **Updates** | ✅ Via CLI | ❌ Separate | ❌ Separate | ❌ Separate |
| **Native Feel** | ⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Performance** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Memory** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Startup Time** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Offline Access** | ❌ Must run serve | ✅ Always | ✅ Always | ✅ Always |
| **Remote Access** | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Learning Curve** | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ |

---

## Risks & Mitigations

### Risks

1. **Port conflicts** - Port 5000 may be in use
   - **Mitigation:** Auto-detect free port, configurable

2. **Browser compatibility** - Different browsers behave differently
   - **Mitigation:** Modern browser requirement, testing

3. **Security** - Exposing CLI commands via HTTP
   - **Mitigation:** Default to localhost only, optional auth

4. **Performance** - Running server + CLI may be heavy
   - **Mitigation:** Lazy loading, optimize API calls

5. **User adoption** - Users may prefer CLI
   - **Mitigation:** Keep CLI primary, UI optional

---

## Future Paths

### Path 1: Stay Web-Only

- Continue improving web UI
- Add PWA capabilities for offline use
- Possibly add Electron wrapper later

### Path 2: Add Desktop Wrapper

- Keep web UI as core
- Add WebView2 wrapper for Windows
- Or Avalonia wrapper for crossplatform desktop

### Path 3: Separate Desktop App

- Maintain web UI for quick access
- Build full desktop app (Avalonia/MAUI) separately
- Share backend logic via shared library

---

## Decision Framework

### Choose Web Server If:

- ✅ Want to test UI demand first
- ✅ Prioritize crossplatform access
- ✅ Need remote management capability
- ✅ Want minimal distribution overhead
- ✅ OK with browser-based experience
- ✅ Features are view/monitor heavy

### Choose Desktop Framework If:

- ✅ Need native desktop integration
- ✅ Want system tray/status bar presence
- ✅ Target is Windows-only development
- ✅ Native performance is critical
- ✅ Rich drag-drop or file operations needed
- ✅ Want offline-first experience

---

## Estimated Effort

| Phase | Duration | Complexity |
|-------|----------|------------|
| MVP (Basic serve + build UI) | 1-2 weeks | Medium |
| Enhanced (Logs, env config) | 2 weeks | Medium |
| Full Blazor (All commands) | 2-3 weeks | High |
| Polish & Advanced | 2-4 weeks | Medium-High |

**Total:** 7-11 weeks for full-featured web UI

---

## Success Criteria

- [ ] User can run `abpdev serve` and access UI in browser
- [ ] Build/run/prepare commands work from UI
- [ ] Logs viewer shows real-time output
- [ ] Configuration can be managed via UI
- [ ] Performance is acceptable (sub-2s load, responsive UI)
- [ ] Works on Windows, macOS, Linux
- [ ] Minimal impact on existing CLI functionality

---

## Next Steps

1. **Decide on approach** (Minimal API vs Blazor Server)
2. **Choose MVP scope** (Which features first?)
3. **Define UI/UX** (Wireframes, design system)
4. **Create technical spike** (Proof of concept)
5. **Get user feedback** (Test with small group)
6. **Iterate** (Improve based on feedback)

---

## Questions to Decide

1. Which implementation approach? (Minimal API or Blazor Server)
2. Should we start with MVP or go full-featured?
3. Do we need authentication? (If enabling remote access)
4. Should it auto-open browser by default?
5. Default port preference? (5000, 8080, etc.)
6. Should we persist any settings?
