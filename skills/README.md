# AbpDevTools Skills for AI Agents

Pre-built instruction sets that teach AI coding assistants how to use [AbpDevTools](https://github.com/enisn/AbpDevTools) commands. When installed, your AI agent will know how to run `abpdev` commands, configure local sources, switch references, and troubleshoot common issues -- without you having to explain the tool each time.

## Available Skills

| Skill | Description |
|-------|-------------|
| [abpdev-add-package](abpdev-add-package/SKILL.md) | Add NuGet packages from any source and automatically wire ABP module dependencies (`abpdev add-package`) |
| [abpdev-workflow](abpdev-workflow/SKILL.md) | Core developer workflow commands (`abpdev build`, `migrate`, `run`, `test`, `prepare`, `logs`, `bundle`) |
| [abpdev-environments](abpdev-environments/SKILL.md) | Virtual environments and infra apps (`abpdev env`, `envapp`, `switch-to-env`) |
| [abpdev-migrations](abpdev-migrations/SKILL.md) | EF Core migration and database workflows (`abpdev migrations`, `database-drop`) |
| [abpdev-maintenance](abpdev-maintenance/SKILL.md) | Maintenance/utilities (`abpdev clean`, `replace`, `logs clear`, `tools`, `update`, `find-port`, etc.) |
| [abpdev-references](abpdev-references/SKILL.md) | Switch between NuGet packages and local project references (`abpdev references to-local`, `to-package`, `references config`, `local-sources`) |

## Recommended Granularity

Prefer **one skill per reusable command family**, not one skill per individual leaf command and not one giant all-in-one `abpdev` skill.

Use this rule of thumb:

1. Create a separate skill when a command area has its own config, workflows, caveats, and troubleshooting.
2. Group closely related commands that are usually used together, such as `run/build/test/prepare` or `migrations/*`.
3. Avoid monolithic skills because agents have to fetch/read more than necessary and the instructions become harder to maintain.

> **Base URL for raw downloads:**
> ```
> https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills
> ```

## Installation

Each AI agent has its own way of loading custom instructions. Pick your agent below.

---

### Cursor

Cursor natively supports skills as `SKILL.md` files.

**Personal (available across all your projects):**

```powershell
# Windows (PowerShell)
$skill = "abpdev-references"
New-Item -ItemType Directory -Force "$env:USERPROFILE\.cursor\skills\$skill" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" -OutFile "$env:USERPROFILE\.cursor\skills\$skill\SKILL.md"
```

```bash
# macOS / Linux
skill="abpdev-references"
mkdir -p ~/.cursor/skills/$skill
curl -fsSL -o ~/.cursor/skills/$skill/SKILL.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md"
```

**Project-level (shared via your repository):**

```powershell
# Windows (PowerShell) -- run from your ABP project root
$skill = "abpdev-references"
New-Item -ItemType Directory -Force ".cursor\skills\$skill" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" -OutFile ".cursor\skills\$skill\SKILL.md"
```

```bash
# macOS / Linux
skill="abpdev-references"
mkdir -p .cursor/skills/$skill
curl -fsSL -o .cursor/skills/$skill/SKILL.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md"
```

---

### Claude Code

Claude Code reads `CLAUDE.md` files from the project root or `~/.claude/CLAUDE.md` globally.

**Project-level (run from your ABP project root):**

```bash
skill="abpdev-references"
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" >> CLAUDE.md
```

**Personal (all projects):**

```bash
mkdir -p ~/.claude
skill="abpdev-references"
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" >> ~/.claude/CLAUDE.md
```

> If you have multiple skills, separate them with `---` or a heading.

---

### GitHub Copilot

Copilot reads instructions from `.github/instructions/*.instructions.md` in your repository.

```powershell
# Windows (PowerShell) -- run from your ABP project root
$skill = "abpdev-references"
New-Item -ItemType Directory -Force ".github\instructions" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" -OutFile ".github\instructions\$skill.instructions.md"
```

```bash
# macOS / Linux
skill="abpdev-references"
mkdir -p .github/instructions
curl -fsSL -o .github/instructions/$skill.instructions.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md"
```

---

### OpenCode

OpenCode reads `AGENTS.md` files from the project root.

**Project-level (run from your ABP project root):**

```bash
skill="abpdev-references"
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/$skill/SKILL.md" >> AGENTS.md
```

---

### Other Agents

For any other AI agent (Windsurf, Cline, Aider, etc.) that supports custom instructions:

1. Download the skill content:
   ```bash
    curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/<skill-name>/SKILL.md"
   ```
2. Paste it into your agent's rules file, custom instructions, or system prompt configuration.

---

## For Agents

> **Copy the block below and paste it into any AI agent's custom instructions or rules.**
> The agent will download and install AbpDevTools skills itself.

````markdown
## AbpDevTools Skills

This project uses [AbpDevTools](https://github.com/enisn/AbpDevTools) (`abpdev` CLI) for building,
running, and managing ABP-based .NET projects.

Pre-built agent skills with detailed instructions for each `abpdev` command are maintained at:

    https://github.com/enisn/AbpDevTools/tree/main/skills

### How to install a skill

1. Fetch the skill index to see available skills:
   ```
   https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills/README.md
   ```

2. Download the skill you need. Each skill lives in its own folder as a `SKILL.md` file:
   ```
   https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills/<skill-name>/SKILL.md
   ```

3. Read the downloaded content and follow the instructions within when the user asks about
   the relevant `abpdev` command.

### Currently available skills

| Skill name | When to fetch |
|------------|---------------|
| `abpdev-add-package` | User asks about installing a NuGet package into an ABP project and automatically adding module dependencies with `abpdev add-package`. |
| `abpdev-workflow` | User asks about building, migrating, running, testing, preparing, bundling, or opening logs with AbpDevTools. |
| `abpdev-environments` | User asks about virtual environments, `abpdev env`, `envapp`, Docker-backed infra tools, or switching shells with environment variables. |
| `abpdev-migrations` | User asks about EF Core migrations, recreating migrations, or dropping databases across multiple projects. |
| `abpdev-maintenance` | User asks about cleanup, replacements, tool-path config, updates, notifications, occupied ports, file finding, or ABP Studio version switching. |
| `abpdev-references` | User asks about switching between NuGet packages and local project references, configuring `local-sources.yml`, using `abpdev references` commands, or the `abpdev local-sources` alias. |

When the user asks about an `abpdev` command covered by a skill, fetch and read that skill
before responding. Use the raw GitHub URL pattern above.
````

---

## Install All Skills at Once

**Cursor (personal):**

```powershell
# Windows (PowerShell)
$skills = @("abpdev-add-package", "abpdev-workflow", "abpdev-environments", "abpdev-migrations", "abpdev-maintenance", "abpdev-references")
foreach ($skill in $skills) {
    New-Item -ItemType Directory -Force "$env:USERPROFILE\.cursor\skills\$skill" | Out-Null
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills/$skill/SKILL.md" -OutFile "$env:USERPROFILE\.cursor\skills\$skill\SKILL.md"
}
```

```bash
# macOS / Linux
for skill in abpdev-add-package abpdev-workflow abpdev-environments abpdev-migrations abpdev-maintenance abpdev-references; do
    mkdir -p ~/.cursor/skills/$skill
    curl -fsSL -o ~/.cursor/skills/$skill/SKILL.md \
      "https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills/$skill/SKILL.md"
done
```

As new skills are added, append their names to the list above.

---

## Contributing

To add a new skill for another `abpdev` command:

1. Create `skills/<skill-name>/SKILL.md` with YAML frontmatter (`name`, `description`) and markdown instructions.
2. Keep it **under 500 lines**, focused on usage.
3. Add the skill to the "Available Skills" table and the "For Agents" skill list in this README.
4. Submit a pull request.
