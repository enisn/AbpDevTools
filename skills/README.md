# AbpDevTools Skills for AI Agents

Pre-built instruction sets that teach AI coding assistants how to use [AbpDevTools](https://github.com/enisn/AbpDevTools) commands. When installed, your AI agent will know how to run `abpdev` commands, configure local sources, switch references, and troubleshoot common issues -- without you having to explain the tool each time.

## Available Skills

| Skill | Description |
|-------|-------------|
| [abpdev-references](abpdev-references/SKILL.md) | Switch between NuGet packages and local project references (`abpdev references to-local`, `to-package`, config) |

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
New-Item -ItemType Directory -Force "$env:USERPROFILE\.cursor\skills\abpdev-references" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" -OutFile "$env:USERPROFILE\.cursor\skills\abpdev-references\SKILL.md"
```

```bash
# macOS / Linux
mkdir -p ~/.cursor/skills/abpdev-references
curl -fsSL -o ~/.cursor/skills/abpdev-references/SKILL.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md"
```

**Project-level (shared via your repository):**

```powershell
# Windows (PowerShell) -- run from your ABP project root
New-Item -ItemType Directory -Force ".cursor\skills\abpdev-references" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" -OutFile ".cursor\skills\abpdev-references\SKILL.md"
```

```bash
# macOS / Linux
mkdir -p .cursor/skills/abpdev-references
curl -fsSL -o .cursor/skills/abpdev-references/SKILL.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md"
```

---

### Claude Code

Claude Code reads `CLAUDE.md` files from the project root or `~/.claude/CLAUDE.md` globally.

**Project-level (run from your ABP project root):**

```bash
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" >> CLAUDE.md
```

**Personal (all projects):**

```bash
mkdir -p ~/.claude
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" >> ~/.claude/CLAUDE.md
```

> If you have multiple skills, separate them with `---` or a heading.

---

### GitHub Copilot

Copilot reads instructions from `.github/instructions/*.instructions.md` in your repository.

```powershell
# Windows (PowerShell) -- run from your ABP project root
New-Item -ItemType Directory -Force ".github\instructions" | Out-Null
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" -OutFile ".github\instructions\abpdev-references.instructions.md"
```

```bash
# macOS / Linux
mkdir -p .github/instructions
curl -fsSL -o .github/instructions/abpdev-references.instructions.md \
  "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md"
```

---

### OpenCode

OpenCode reads `AGENTS.md` files from the project root.

**Project-level (run from your ABP project root):**

```bash
curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md" >> AGENTS.md
```

---

### Other Agents

For any other AI agent (Windsurf, Cline, Aider, etc.) that supports custom instructions:

1. Download the skill content:
   ```bash
   curl -fsSL "https://raw.githubusercontent.com/enisn/AbpDevTools/master/skills/abpdev-references/SKILL.md"
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
| `abpdev-references` | User asks about switching between NuGet packages and local project references, configuring `local-sources.yml`, or using `abpdev references` commands. |

When the user asks about an `abpdev` command covered by a skill, fetch and read that skill
before responding. Use the raw GitHub URL pattern above.
````

---

## Install All Skills at Once

**Cursor (personal):**

```powershell
# Windows (PowerShell)
$skills = @("abpdev-references")
foreach ($skill in $skills) {
    New-Item -ItemType Directory -Force "$env:USERPROFILE\.cursor\skills\$skill" | Out-Null
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/enisn/AbpDevTools/main/skills/$skill/SKILL.md" -OutFile "$env:USERPROFILE\.cursor\skills\$skill\SKILL.md"
}
```

```bash
# macOS / Linux
for skill in abpdev-references; do
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
