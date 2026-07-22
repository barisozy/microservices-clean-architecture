# Repository Governance & Setup (Phase 0)

## 1. Branch Protection Rules (`main` branch)

The following governance rules are established for the `main` branch:

- **Require Pull Request Reviews**: Direct commits to `main` are prohibited. All updates must be submitted via feature branch Pull Requests and approved prior to merging.
- **Require Status Checks**: Required CI/CD status checks (e.g., build, test, and workflow validation scripts under `.github/workflows/`) must pass cleanly before merging PRs.
- **Enforce Signed Commits**: All commits targeting `main` must be cryptographically signed (`gpg` / `ssh` / `smime`).
- **Disallow Direct Pushes & Force Pushes**: Direct pushes and force-push overrides to `main` are restricted.

---

## 2. GitHub Projects Kanban Board Setup

A project board has been configured with the following workflow columns:

1. **`Todo`**: Backlog of issues ready for development.
2. **`In Progress`**: Active feature development on dedicated feature branches.
3. **`In Review`**: Pull Requests currently undergoing review and status check validation.
4. **`Done`**: Merged issues and pull requests integrated into `main`.
