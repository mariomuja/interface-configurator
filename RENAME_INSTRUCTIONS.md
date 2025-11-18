# Repository Rename Instructions

## ✅ Completed Changes

All references to `infrastructure-as-code` have been updated to `interface-configurator` in the following files:

### Updated Files:
- ✅ `package.json` - Project name updated
- ✅ `vercel/vercel.json` - Project name and output directory updated
- ✅ `frontend/package.json` - Frontend package name updated
- ✅ `api/package.json` - API package name updated
- ✅ `frontend/karma.conf.js` - Coverage directory updated
- ✅ `VERCEL_ENV_VARS.md` - Documentation updated
- ✅ `SETUP_GITHUB_SECRET.md` - GitHub URLs updated
- ✅ `SETUP_GITHUB_SECRET_SIMPLE.md` - GitHub URLs updated
- ✅ `.git/config` - Remote URL updated

## 🔄 Remaining Steps

### 1. Rename Local Folder

**Close Cursor/VS Code first**, then rename the folder:

```powershell
# Navigate to parent directory
cd C:\Users\mario

# Rename the folder
Rename-Item -Path "infrastructure-as-code" -NewName "interface-configurator"
```

Or manually:
1. Close Cursor/VS Code
2. Navigate to `C:\Users\mario\`
3. Right-click on `infrastructure-as-code` folder
4. Select "Rename"
5. Change to `interface-configurator`

### 2. Rename GitHub Repository

1. Go to: https://github.com/mariomuja/infrastructure-as-code/settings
2. Scroll down to the "Repository name" section
3. Change the name from `infrastructure-as-code` to `interface-configurator`
4. Click "Rename"

**Note**: GitHub will automatically redirect the old URL to the new one, but it's best to update all references.

### 3. Update Vercel Project Name (Optional)

If you have a Vercel project linked:
1. Go to Vercel Dashboard
2. Find your project
3. Go to Settings → General
4. Update the project name to `interface-configurator`

### 4. Reopen Project

After renaming the folder:
1. Open Cursor/VS Code
2. File → Open Folder
3. Navigate to `C:\Users\mario\interface-configurator`
4. Open the project

### 5. Verify Changes

After reopening, verify:
```powershell
# Check git remote
git remote -v
# Should show: https://github.com/mariomuja/interface-configurator.git

# Check package.json
cat package.json | Select-String "interface-configurator"
# Should show the new name
```

## 📝 Notes

- The Angular project name in `angular.json` was already `interface-configuration` (no change needed)
- All GitHub Actions workflows will continue to work after renaming
- Vercel deployments will continue to work (may need to reconnect if project name changes)
- Git history is preserved - no data loss

## ⚠️ Important

**Before renaming the folder:**
- Save all open files
- Close Cursor/VS Code
- Ensure no other processes are using the folder

**After renaming:**
- Update any bookmarks or shortcuts
- Update any CI/CD configurations that reference the folder path
- Update any documentation that references the old path



