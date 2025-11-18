# Vercel Deployment Verification Guide

## ✅ Pre-Deployment Checks (Completed)

### 1. `.vercelignore` File Created
**Location:** `frontend/.vercelignore`

**Excluded Items:**
- `node_modules/` (~323 MB)
- `.angular/` (~732 MB)
- `dist/` (~2.29 MB)
- Test files (`*.spec.ts`, `karma.conf.js`)
- IDE files (`.vscode/`, `.idea/`)
- Logs and environment files

**Expected Impact:** Upload size should be reduced from ~708 MB to < 50 MB (only source files)

### 2. Files Committed and Pushed
- ✅ `.vercelignore` file added
- ✅ All changes committed
- ✅ Pushed to GitHub main branch

## 🔍 How to Check Vercel Dashboard

### Step 1: Access Vercel Dashboard
1. Go to [https://vercel.com/dashboard](https://vercel.com/dashboard)
2. Sign in with your account
3. Select your project (likely named `integration-configurator` or similar)

### Step 2: Check Deployment Status
1. Look for the latest deployment in the deployments list
2. Check the status:
   - **Building** - Deployment in progress
   - **Ready** - Deployment successful
   - **Error** - Deployment failed (check logs)

### Step 3: Check Upload Size
1. Click on the latest deployment
2. Look for "Build Logs" or "Deployment Logs"
3. Find the upload size information:
   - Look for lines like: `Uploading...` or `Upload size: X MB`
   - Compare with previous deployments

**Expected:** Upload size should be significantly smaller (< 50 MB vs previous ~708 MB)

### Step 4: Verify Build Success
1. Check the build logs for:
   - ✅ TypeScript compilation success
   - ✅ No build errors
   - ✅ Build output created successfully
2. Check the deployment URL:
   - Click "Visit" or "Preview" button
   - Verify the app loads correctly
   - Test key functionality

## 📊 Expected Results

### Upload Size Comparison
- **Before:** ~708 MB (including node_modules and .angular)
- **After:** < 50 MB (only source files)
- **Reduction:** ~93% smaller

### Build Process
Vercel will:
1. Clone repository (small, only source files)
2. Install dependencies (`npm install` or `npm ci`)
3. Build Angular app (`ng build`)
4. Deploy built output

## 🧪 Local Verification

### Test Build Locally
```powershell
cd frontend
npm install
npm run build
```

**Expected:** Build should complete successfully without errors

### Check Build Output Size
```powershell
cd frontend/dist
Get-ChildItem -Recurse -File | Measure-Object -Property Length -Sum | Select-Object @{Name="TotalSizeMB";Expression={[math]::Round($_.Sum / 1MB, 2)}}
```

**Expected:** Build output should be reasonable size (typically 1-5 MB for Angular apps)

## 🐛 Troubleshooting

### If Upload Size is Still Large
1. Verify `.vercelignore` is in the `frontend/` directory
2. Check that it's committed to git
3. Verify Vercel is using the correct root directory
4. Check Vercel project settings:
   - Root Directory: Should be `frontend` (if frontend is a subdirectory)
   - Build Command: `npm run build` or `ng build`
   - Output Directory: `dist` or `dist/browser`

### If Build Fails
1. Check build logs in Vercel dashboard
2. Common issues:
   - Missing environment variables
   - TypeScript errors (should be fixed)
   - Missing dependencies
   - Build command incorrect

### If App Doesn't Load
1. Check deployment URL
2. Open browser console for errors
3. Verify API endpoints are accessible
4. Check CORS settings if API calls fail

## 📝 Vercel Configuration

### Recommended Settings
- **Framework Preset:** Angular
- **Root Directory:** `frontend` (if frontend is a subdirectory)
- **Build Command:** `npm run build` or `ng build --configuration production`
- **Output Directory:** `dist/browser` or `dist` (check angular.json)
- **Install Command:** `npm install` or `npm ci`

### Environment Variables
Ensure these are set in Vercel dashboard:
- `AZURE_SQL_SERVER`
- `AZURE_SQL_DATABASE`
- `AZURE_SQL_USER`
- `AZURE_SQL_PASSWORD`
- `AZURE_STORAGE_CONNECTION_STRING` (if needed)
- `AZURE_FUNCTION_APP_URL` (if needed)

## ✅ Success Criteria

- [ ] Deployment status shows "Ready"
- [ ] Upload size is < 50 MB
- [ ] Build completes without errors
- [ ] App loads correctly at deployment URL
- [ ] All functionality works as expected
- [ ] No console errors in browser

## 🔗 Useful Links

- Vercel Dashboard: https://vercel.com/dashboard
- Vercel Documentation: https://vercel.com/docs
- Angular Deployment Guide: https://angular.io/guide/deployment

