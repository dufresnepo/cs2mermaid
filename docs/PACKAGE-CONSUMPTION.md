# Package Consumption Guide

The `cs2mermaid` packages are published to **both** GitHub Packages and NuGet.org.

## Option 1: NuGet.org (Recommended for Public Use)

No configuration needed - this is the default source:

```bash
dotnet add package Cs2Mermaid.Build
```

That's it! Works immediately.

## Option 2: GitHub Packages

### Setup (One-time per machine)

1. Create a GitHub Personal Access Token (PAT):
   - Go to https://github.com/settings/tokens
   - Click "Generate new token (classic)"
   - Select scope: `read:packages`
   - Copy the token

2. Add GitHub Packages as a NuGet source:

```bash
dotnet nuget add source https://nuget.pkg.github.com/uniflow-technologies/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

Replace:
- `YOUR_GITHUB_USERNAME` - Your GitHub username
- `YOUR_GITHUB_PAT` - The token you created

### Using the Package

```bash
dotnet add package Cs2Mermaid.Build --source github
```

Or configure in `nuget.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/uniflow-technologies/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </github>
  </packageSourceCredentials>
</configuration>
```

## Which Should You Use?

### Use NuGet.org if:
- ✅ You want the simplest setup
- ✅ You're distributing to external users
- ✅ You want maximum discoverability

### Use GitHub Packages if:
- ✅ You want tight integration with GitHub
- ✅ You're using packages internally in an organization
- ✅ You want to test pre-release versions
- ✅ You prefer not to manage a separate NuGet.org account

## Verification

Check which sources are configured:

```bash
dotnet nuget list source
```

Search for packages:

```bash
# From NuGet.org
dotnet package search Cs2Mermaid

# From GitHub Packages
dotnet package search Cs2Mermaid --source github
```

## CI/CD Usage

### GitHub Actions (automatic auth)

```yaml
- name: Restore packages
  run: dotnet restore
  env:
    NUGET_AUTH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

No additional configuration needed!

### Azure Pipelines

```yaml
- task: NuGetAuthenticate@1
  inputs:
    nuGetServiceConnections: 'github-packages'
```

