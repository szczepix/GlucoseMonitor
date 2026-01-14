# Merge and Release Guide

## PR Status: Ready to Merge ✅

This PR successfully implements self-contained deployment with all framework binaries bundled.

## Pre-Merge Checklist

- [x] All build errors resolved
- [x] Code review completed (no issues found)
- [x] Documentation updated
- [x] Build artifacts generated and tested
- [x] All commits pushed to branch

## Commits in This PR

1. `935fd98` - Enable self-contained single-file deployment for WinUI 3 app
2. `4247966` - Add comprehensive deployment documentation
3. `288bfc9` - Add implementation summary document
4. `e78380a` - Address code review feedback
5. `5f8ffa4` - Add build artifact generation for PR testing
6. `e37bb52` - Fix NuGet restore for win-x64 self-contained publish
7. `aeaa1ee` - Fix XML comment syntax error in project file
8. `cead3d8` - Add EnableMsixTooling for PublishSingleFile with WinUI 3

## How to Merge

### Option 1: GitHub UI (Recommended)
1. Go to the PR page on GitHub
2. Click "Merge pull request" button
3. Choose merge type:
   - **Squash and merge** - Combines all commits into one (cleaner history)
   - **Create a merge commit** - Preserves all individual commits
   - **Rebase and merge** - Replays commits on top of main
4. Click "Confirm merge"

### Option 2: Command Line
```bash
# Switch to main branch
git checkout main

# Pull latest changes
git pull origin main

# Merge the PR branch
git merge --no-ff copilot/bundle-related-framework-binaries

# Push to main
git push origin main
```

## How to Create a Release

### Automatic Release (Recommended)

The release workflow will automatically trigger when you:
1. Push to main branch (creates a prerelease)
2. Push a tag starting with 'v' (creates a full release)

**To create a stable release:**

```bash
# After merging to main, create and push a version tag
git checkout main
git pull origin main
git tag -a v2026.01.14 -m "Release v2026.01.14: Self-contained deployment"
git push origin v2026.01.14
```

The workflow will automatically:
- Build self-contained executables
- Create ZIP archives
- Generate checksums
- Create GitHub Release with artifacts
- Mark as stable release (not prerelease)

### Manual Release Trigger

You can also manually trigger a release:
1. Go to Actions → Release workflow
2. Click "Run workflow"
3. Select the main branch
4. Click "Run workflow"

## What Gets Released

The release will include:
- **GlucoseMonitor-win-x64.zip** (~150-200 MB)
  - Self-contained executable with .NET 9 runtime
  - Windows App SDK bundled
  - No installation required
  - Works on Windows 11 22H2+ without .NET runtime
  
- **GlucoseMonitor.MockServer-win-x64.zip**
  - Self-contained mock server for testing
  
- **checksums.txt**
  - SHA256 checksums for verification

## Version Naming

Current scheme: `v2026.01.14-{shortSha}` for auto-releases

For stable releases, use semantic versioning:
- `v2026.01.14` - Date-based version
- `v1.0.0` - Traditional semantic version

## Post-Release Steps

1. **Test the release:**
   - Download the release ZIP
   - Extract on Windows 11 machine
   - Run GlucoseMonitor.UI.exe
   - Verify it works without .NET runtime installed

2. **Update documentation:**
   - Update README.md with latest release version (if needed)
   - Update CHANGELOG.md (if exists)

3. **Communicate:**
   - Announce the release to users
   - Document any breaking changes
   - Provide upgrade instructions

## Rollback Plan

If issues are found after release:

1. **Hotfix:** Create a new branch from main, fix the issue, and create a new release
2. **Revert release:** Delete the tag and GitHub release (not recommended)
3. **Communication:** Notify users of the issue and provide workarounds

## Important Notes

- **File size:** Self-contained builds are ~150-200 MB (vs ~2-5 MB framework-dependent)
- **Runtime updates:** Requires rebuilding app for .NET security updates
- **Compatibility:** Requires Windows 11 22H2+ (build 22621+)
- **No .NET installation needed:** App includes all dependencies

## Success Criteria

✅ Single executable with all dependencies
✅ No .NET runtime installation required
✅ Compatible with existing update system
✅ MSI installer updated for self-contained deployment
✅ Comprehensive documentation provided
✅ Build artifacts tested and verified

## Support

For issues or questions:
- Check DEPLOYMENT-TESTING-GUIDE.md
- Check SELF-CONTAINED-DEPLOYMENT.md
- Check TESTING-PR-BUILDS.md
- Review build workflow logs

---

**Ready to merge and release!** 🚀
