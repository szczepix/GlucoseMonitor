# Testing PR Builds

This document explains how to test self-contained builds from pull requests before merging.

## Automatic Artifact Generation

The build workflow automatically generates and uploads build artifacts for:

1. **PR branches starting with `copilot/`** - Automatic on every push
2. **Manual workflow runs** - Using the "Run workflow" button

## Downloading Build Artifacts

### From PR Branches (e.g., `copilot/*`)

1. Go to the PR on GitHub
2. Click the "Checks" tab
3. Find the "Build" workflow run
4. Scroll to the bottom to find "Artifacts" section
5. Download one of the available artifacts:
   - **GlucoseMonitor-UI-win-x64** - Main application files
   - **GlucoseMonitor-MockServer-win-x64** - Mock server files
   - **GlucoseMonitor-win-x64-zip** - Complete application as ZIP

### From Manual Workflow Runs

1. Go to Actions → Build workflow
2. Click "Run workflow" button
3. Select the branch you want to build
4. Optionally set "Build and upload artifacts for testing" to `true`
5. Click "Run workflow"
6. Wait for the workflow to complete
7. Download artifacts from the workflow run page

## Testing the Downloaded Build

### Extract and Run

```powershell
# Extract the ZIP
Expand-Archive -Path GlucoseMonitor-win-x64.zip -DestinationPath ./GlucoseMonitor-Test

# Navigate to the folder
cd GlucoseMonitor-Test

# Run the application
.\GlucoseMonitor.UI.exe
```

### Verification Steps

**Critical Tests**:
1. ✅ Application launches without errors
2. ✅ Runs on machine **without** .NET 9 runtime installed
3. ✅ All UI features work (settings, overlay, system tray)
4. ✅ Nightscout API connectivity works
5. ✅ Glucose monitoring and alerts function properly

**File Size Check**:
- Self-contained executable should be ~150-200 MB
- If much smaller, it may be framework-dependent (error)

**Runtime Check**:
```powershell
# Check if .NET runtime is bundled
Get-ChildItem . -Recurse -Filter "*.dll" | Select-Object -First 10
# Should see many Microsoft.* DLLs if properly bundled
```

## Artifact Retention

- Artifacts are kept for **7 days** after upload
- Download them before they expire
- For permanent builds, merge the PR and create a release

## Current PR: Self-Contained Deployment

This PR (`copilot/bundle-related-framework-binaries`) will automatically generate artifacts because:
- Branch name starts with `copilot/`
- Build workflow runs on every push
- Artifacts uploaded with 7-day retention

**To test this PR**:
1. Wait for the current build to complete
2. Go to the PR → Checks → Build workflow
3. Download `GlucoseMonitor-win-x64-zip` artifact
4. Extract and test on Windows 11 machine
5. Verify it runs without .NET 9 runtime installed

## Troubleshooting

### No Artifacts Section

If you don't see artifacts:
- Check that the workflow completed successfully
- Verify the branch name starts with `copilot/` or it's a manual run
- Ensure the build and publish steps succeeded

### Download Issues

If download fails:
- Artifacts may have expired (>7 days old)
- You may need to be logged into GitHub
- Browser may block large downloads - try direct download link

### Runtime Errors

If the app won't run:
- Check Windows version (requires Windows 11 22H2+)
- Check Event Viewer for detailed error messages
- Verify all files were extracted from the ZIP

## Comparison: PR Build vs Release Build

| Aspect | PR Artifact | Release Build |
|--------|-------------|---------------|
| **Trigger** | Auto on `copilot/*` branches | Manual or git tag |
| **Retention** | 7 days | Permanent |
| **Format** | ZIP with all files | GitHub Release with checksums |
| **Version** | Build SHA | Tagged version |
| **Purpose** | Testing before merge | Production deployment |

## Next Steps After Testing

If the build works correctly:
1. Comment on the PR with test results
2. Approve the PR for merging
3. Merge to main branch
4. Create an official release

If issues are found:
1. Report the issue as a comment on the PR
2. Developer will fix and push updates
3. New artifacts will be generated automatically
4. Re-test with new artifacts
