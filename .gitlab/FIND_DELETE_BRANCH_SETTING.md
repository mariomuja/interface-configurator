# How to Find "Delete Source Branch" Setting in GitLab

The location of this setting varies by GitLab version and edition. Here's where to look:

## Method 1: In the Merge Request (Always Works)

### When Creating a Merge Request:

1. **Create Merge Request page:**
   - After selecting source and target branches
   - Scroll down to **"Options"** or **"Merge request options"** section
   - Look for checkbox: **"Delete source branch"** or **"Delete source branch when merge request is accepted"**
   - ✅ Check this box

### In an Existing Merge Request:

1. **Open the merge request**
2. **Look for one of these locations:**
   - **Right sidebar** → **"Options"** section → **"Delete source branch"** checkbox
   - **Near the merge button** → **"Delete source branch"** checkbox
   - **Merge request settings** (gear icon) → **"Delete source branch"**

## Method 2: Project Settings (May Not Be Available)

### Location 1: Settings → Repository

1. Go to **Settings** → **Repository**
2. Scroll to **"Merge requests"** section
3. Look for **"Delete source branch"** or **"Remove source branch after merge"**
4. Enable the checkbox

### Location 2: Settings → General → Merge requests

1. Go to **Settings** → **General**
2. Expand **"Merge requests"** section
3. Look for **"Delete source branch"** option

### Location 3: Settings → Merge requests

1. Go to **Settings** → **Merge requests**
2. Look for **"Merge options"** or **"Default merge options"**
3. Find **"Delete source branch"** checkbox

## If You Can't Find It

**This is normal!** The setting location depends on:
- GitLab version
- GitLab edition (Free/Premium/Ultimate)
- UI theme/version

### Solution: Use Per-MR Setting

**Always available:** Check the box in each merge request:
1. Open your merge request
2. Look near the merge button or in the sidebar
3. Check **"Delete source branch"** before merging

### Alternative: Use CI/CD Cleanup Job

If you can't find the setting, use the cleanup job in `.gitlab-ci.yml`:

1. **Add GitLab token:**
   - Settings → CI/CD → Variables
   - Add: `GITLAB_TOKEN` (your Personal Access Token with `api` scope)

2. **The cleanup job will automatically delete `ready/*` branches after merge**

## Visual Guide

### In Merge Request (Most Common):

```
┌─────────────────────────────────────┐
│ Merge Request: ready/myfeature     │
├─────────────────────────────────────┤
│                                     │
│ [Merge] [Options ▼]                │
│                                     │
│ Options:                            │
│ ☑ Delete source branch             │  ← Look here!
│ ☐ Squash commits                   │
│                                     │
└─────────────────────────────────────┘
```

### In Project Settings (If Available):

```
Settings → Repository → Merge requests
├─ Merge options
│  ├─ ☑ Delete source branch after merge
│  └─ ☐ Squash commits when merging
```

## Quick Test

1. Create a test merge request from `ready/test` to `main`
2. Before merging, look for **"Delete source branch"** checkbox
3. Check it
4. Merge
5. Verify branch is deleted: **Repository** → **Branches**

## Still Can't Find It?

**Use the CI/CD cleanup job instead:**
- It's already configured in `.gitlab-ci.yml`
- Just add `GITLAB_TOKEN` to CI/CD Variables
- It will automatically delete `ready/*` branches after merge

