# XClone: project progress

X (Twitter) clone: ASP.NET Core 10 API (`XCloneAPI/`), Angular 21 SPA (`x-clone-frontend/`), PostgreSQL. Setup and usage are in [README.md](README.md); this file tracks **what is done, what is in progress and what is still pending**.

**Keep it current:** each feature branch moves its own item from *Pending* to *Completed* (or adds it to *In progress* while the branch is open) before it is merged.

_Last updated: 2026-09-20, after the cursor-paging merge (`master` at `a45ec60`)._

## How work is delivered

1. Every feature or fix gets its own branch (`feature/...`, `fix/...`, `docs/...`).
2. Tests for it are written and the whole suites pass (`dotnet test XCloneAPI.Tests`, `cd x-clone-frontend && npm test -- --watch=false`).
3. Merge into `master` with `git merge --no-ff`, push, and check that CI is green on the merge commit. CI runs only on pushes to `master` and on pull requests, not on other pushed branches.
4. Migrations are applied to the local database by the owner (`cd XCloneAPI && dotnet ef database update`). The local database was built by hand, so write migrations that tolerate missing objects (`DROP INDEX IF EXISTS`) and try them on a scratch database first.

## Completed

**Accounts and security**
- Register and login with JWT; route guards on the SPA.
- Security pass: rate limiting, password hashing with automatic upgrade of legacy hashes, no emails or hashes in any response, restricted CORS, and the API refuses to start with a placeholder secret.
- Secrets live in .NET user-secrets, never in committed files.

**Social features**
- Home timeline with a 280-character composer; optimistic likes.
- Follow and unfollow; user search and a "Who to follow" widget.
- Profiles: banner, avatar, display name, edit-profile modal, follower and following counts, and an exact post count (top-level posts plus reposts, as the Posts tab lists them; replies are not counted).
- Replies and thread pages; Posts and Replies tabs on profiles.
- Reposts and undo; reposts show in followers' timelines and on profiles.
- Notifications for replies and reposts (never for your own actions), an unread badge polled every 30 s and paused in hidden tabs, and a Notifications page that marks everything read on open.
- "Load more" on the feed, both profile tabs, reply threads and notifications, backed by cursor paging (`{ items, nextCursor }`), so posting or deleting between pages never repeats or skips an entry.

**Quality**
- 194 API integration tests against a real PostgreSQL database (a throwaway `xclone_it_<guid>` database per run), including tests that check the Angular app's URLs and types against the API.
- 137 frontend tests (Vitest, jsdom, Angular's fake HTTP backend).
- Tests were checked by deliberately breaking the code; every meaningful break was caught (one harmless equivalent mutant remains).
- GitHub Actions: API tests against a `postgres:17` service, then frontend tests, build and `npm audit`.

**Database migrations** (all applied to the local database): `InitialCreate`, `AddRepliesAndRetweets`, `AddNotifications`, `AddKeysetPagingIndexes`.

## In progress

Nothing. `master` is clean and CI is green.

## Pending

**Needs the owner**
- The two legacy dev accounts (`string`, `johndoe`) were created before the profile columns existed and still have NULL `bio`, `avatar_url` and `display_name` in the local database: backfill them with SQL, or drop and recreate the database.
- Rotate the local PostgreSQL password. An early commit put it in git history (the repository is public), so treat the old one as leaked and update the user-secret afterwards.

**Known limitations**
- Opening Notifications with more than 20 unread marks everything read before the later pages load, so entries on those pages show as already read.
- Posts made in another tab or by other people show up in the feed only after a refresh; there is no live update. Notifications are polled, not pushed.
- `profile.ts` is 0.13 kB over its component style budget, which gives a build warning (not an error).

**Not built yet** (ideas, not ordered or decided)
- Image and video posts: the API stores `mediaUrls`, but there is no upload endpoint and no UI to attach or show media.
- Followers and following lists: the API endpoints and `ApiService` methods exist, but no page uses them.
- Editing a post (delete exists), quote posts, bookmarks, mentions, hashtags, and post search (only user search exists).
- Direct messages.
- Password reset, email verification, and refresh tokens (only register and login exist).
- Deployment: Docker files and a hosting setup.
- End-to-end browser tests (today's tests are API integration and frontend unit tests).
- Run CI on feature branches (`feature/**`, `fix/**`) so they are tested before the merge, not just after.
