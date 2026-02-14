# Template Examples

This folder contains two starter templates that can be used by a CLI scaffold flow.

- `dotnet/abp-module-simple`: A `dotnet new` compatible ABP module template.
- `npm/abp-package-simple`: A Node package template compatible with ABP npm package conventions.

## Dotnet Template (ABP module)

Install from local folder:

```bash
dotnet new install ./templates/dotnet/abp-module-simple
```

Create a new module:

```bash
dotnet new abp-module-simple -n Acme.Inventory.Mvc -o ./src/Acme.Inventory.Mvc --ModuleClassName InventoryMvc
```

Uninstall:

```bash
dotnet new uninstall abp-module-simple
```

## Npm Template (ABP package)

This template is intentionally simple and includes both:

- `abp.resourcemapping.js` (actual ABP mapping file)
- `abp.resourcemappings.js` (reminder stub; harmless if unused)

Copy the folder and replace placeholders:

- `__PACKAGE_NAME__`
- `__ABP_VERSION__`
- `__DESCRIPTION__`

Then run:

```bash
npm install
```
