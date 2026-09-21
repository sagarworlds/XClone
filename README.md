# X (Twitter) Clone

[![CI](https://github.com/sagarworlds/XClone/actions/workflows/ci.yml/badge.svg)](https://github.com/sagarworlds/XClone/actions/workflows/ci.yml)
[![C#](https://img.shields.io/badge/C%23-%23239120?style=flat&logo=c%23&logoColor=white)](https://docs.microsoft.com/dotnet/csharp)
[![TypeScript](https://img.shields.io/badge/TypeScript-%23007ACC?style=flat&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Angular](https://img.shields.io/badge/Angular-%23DD0031?style=flat&logo=angular&logoColor=white)](https://angular.io/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-%23336791?style=flat&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Node.js](https://img.shields.io/badge/Node.js-%23339933?style=flat&logo=node.js&logoColor=white)](https://nodejs.org/)
[![CSS](https://img.shields.io/badge/CSS-%231572B6?style=flat&logo=css3&logoColor=white)](https://developer.mozilla.org/docs/Web/CSS)
[![HTML5](https://img.shields.io/badge/HTML5-%23E34F26?style=flat&logo=html5&logoColor=white)](https://developer.mozilla.org/docs/Web/HTML)
[![JWT](https://img.shields.io/badge/JWT-JSON%20Web%20Tokens-yellow?style=flat)](https://jwt.io/)

A modern, full-stack clone of X (formerly Twitter) featuring a secure ASP.NET Core Web API backend, a PostgreSQL database, and a beautiful Angular single-page application (SPA) frontend.

---

## 🚀 Features

- **Authentication & Security**: Secure user registration and login using JWT (JSON Web Tokens).
- **Home Timeline Feed**: A live feed of posts from the users you follow, featuring a character-limited (280 chars) tweet composer.
- **Interactions**: Fast, optimistic UI updates for liking/unliking posts.
- **Load more**: The home timeline, profile tabs, reply threads and notifications load 20 entries at a time with a "Load more" button. Pages are read with cursors, so posting, deleting or undoing a repost in between (or other people doing the same) can never repeat or skip an entry, and a failed page can be retried without losing what is already on screen.
- **Mentions**: Write `@username` in a post or reply to name someone. If the account exists, the name becomes a link to their profile and they get a notification ("mentioned you in a post"). You are not told about your own mentions, and someone you reply to is told once, as a reply.
- **Hashtags**: A `#tag` in a post (or reply) is a link to that tag's page, which lists every post and reply that uses it, newest first, 20 at a time. The right-hand column shows what is trending: the five tags most used by posts of the last week.
- **Images**: Attach up to 4 images (PNG, JPEG, GIF or WebP, 5 MB each) to a post or a reply. They upload as soon as you pick them, with a preview you can remove, and show in a grid on the post; clicking one opens the full picture in a new tab.
- **Replies & Threads**: Reply to any post (or to a reply). Each post has its own thread page with a reply box, and profiles have a Posts and a Replies tab.
- **Reposts**: Repost/undo with one click. Reposts show up in your followers' timelines and on your profile with a "reposted" banner.
- **Notifications**: You are told when someone replies to or reposts one of your posts (never for your own actions). The sidebar shows an unread badge that refreshes every 30 seconds, and the Notifications page lists everything newest first, highlights what is new, and marks it all as read when you open it. Undoing a repost, or deleting the reply or the post, takes its notification back.
- **User Profiles**: Custom banners, avatars, display names, follower/following counts, an exact post count (top-level posts and reposts, matching the Posts tab; replies are not counted), join dates, and an interactive edit-profile modal.
- **Social Graph**: Follow and unfollow capabilities that seamlessly update timelines and recommendation widgets. The follower and following counts on a profile open the full lists (newest follow first, 20 at a time with "Load more"), each with its own follow button.
- **User Search & Recommendations**: Dynamic real-time user search and a "Who to follow" suggestion widget.
- **Responsive Theme**: Premium, Twitter-inspired dark mode using glassmorphic UI components, smooth transitions, and custom scrollbars.

---

## 📁 Repository Structure

```
XClone/
├── XCloneAPI/                # ASP.NET Core 10.0 Web API (Backend)
│   ├── Controllers/          # API endpoints (Auth, Posts, Likes, Retweets, Follows, Users, Notifications)
│   ├── Data/                 # AppDbContext configuration
│   ├── DTOs/                 # Request/Response Data Transfer Objects
│   ├── Models/               # Entity Framework database schemas (User, Post, Like, Retweet, Follow, Notification)
│   └── Services/             # Domain logic (IAuthService, IPostService, etc.)
│
├── XCloneAPI.Tests/          # API integration tests (xUnit, real PostgreSQL)
│
└── x-clone-frontend/         # Angular 21 Single Page Application (Frontend)
    ├── src/
    │   ├── app/
    │   │   ├── components/   # Standalone UI (Login, Register, Feed, Profile, PostDetail + shared PostCard, Composer, Sidebar, Widgets, LoadMore, Notifications)
    │   │   ├── models/       # TypeScript Interfaces
    │   │   ├── services/     # ApiService (signals state), PagedList (cursor paging), NotificationsService (unread badge polling)
    │   │   └── app.routes.ts # SPA routing with functional auth guards
    │   │   (each *.spec.ts sits next to the code it tests; shared test helpers are in src/testing/)
    │   ├── environments/     # Environment-specific API configuration
    │   └── styles.css        # Global CSS dark theme styles
    └── angular.json          # Angular workspace settings
```

---

## 🛠️ Tech Stack

### Backend
- **Framework**: ASP.NET Core 10.0
- **Database**: PostgreSQL (via Npgsql Entity Framework Core Provider)
- **Authentication**: JWT Bearer Authentication

### Frontend
- **Framework**: Angular 21 (standalone components, signals, zoneless change detection)
- **Testing**: Vitest + jsdom through Angular's `ng test`
- **Styling**: Vanilla CSS (Custom variables, transitions, and layout grids)
- **Icons**: Google Material Symbols Outlined
- **Typography**: Inter Font family

---

## ⚙️ Getting Started

### Prerequisites
- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20.19+, 22.12+ or 24+ (what Angular 21 requires)
- [PostgreSQL](https://www.postgresql.org/download/)

---

### Configure Secrets

The repository only contains placeholders (`CHANGE-ME...`) for the database password and the JWT signing key. Your real values live **outside the repo** in the .NET user-secrets store, which is loaded automatically when the API runs in Development. The API refuses to start while a placeholder is still in use.

```bash
cd XCloneAPI
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=x_clone_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "<a random string of 64+ characters>"
```

- Visual Studio: right-click the `XCloneAPI` project and choose **Manage User Secrets**.
- Any other environment: set the `ConnectionStrings__DefaultConnection` and `Jwt__Key` environment variables instead.
- Never put real values in `appsettings*.json`; those files are committed.

---

### Database Setup

1. Make sure your PostgreSQL server is running and the secrets above are configured.
2. Run the migrations to initialize the database:
   ```bash
   cd XCloneAPI
   dotnet ef database update
   ```

---

### Run the Backend

1. Navigate to the API folder:
   ```bash
   cd XCloneAPI
   ```
2. Start the web server:
   ```bash
   dotnet run
   ```
   *The API will start and listen on `http://localhost:5168` (or `https://localhost:7040` depending on launch profile). The frontend is configured for `http://localhost:5168/api` in `src/environments/`.*

---

### Run the Frontend

1. Navigate to the frontend folder:
   ```bash
   cd x-clone-frontend
   ```
2. Install the node packages:
   ```bash
   npm install
   ```
3. Start the Angular dev server:
   ```bash
   npm run start
   ```
   *The application will launch automatically on `http://localhost:4200`.*

---

### Mentions (API)

A post's response has `mentions`: the usernames its text names with `@` that are real accounts, as they are spelled on their profiles (the app links only these). Nothing extra is sent when creating a post.

- A mention is an `@` followed by 3-50 letters (a-z), digits or underscores (what a username is made of), not glued to a word in front (`me@example.com` is not one) and ending where the word ends. Names are matched without regard to case; each counts once per post; only the first 10 names of a post count.
- The people named are told in the same save as the post (`type: "mention"`, `postId` is the post that names them), never for their own posts, and never twice: someone who already gets a `reply` notification for the same post does not get a second one.
- **Usernames** may now only contain letters, digits and underscores (new accounts; a name that differs from an existing one only by case is taken too, since `@name` would otherwise be ambiguous). Older accounts keep working. If two older accounts differ only by case, `@name` finds the one spelled exactly like that, else the oldest.
- The app and the server use the same rule (`utils/text-entities.ts` and `MentionParser.cs`), tested against the same list of examples, `x-clone-frontend/src/testing/text-entities.json`. Posts written before mentions existed were filled in by the migration (links only, nobody is notified about them).

### Hashtags (API)

```
GET /api/posts/hashtag/sunset?take=20&cursor=...  -> { "items": [ ...posts and replies... ], "nextCursor": "..." }
GET /api/hashtags/trending?take=5                 -> [ { "tag": "sunset", "postsCount": 12 }, ... ]
```

- A hashtag is a `#` followed by 1-50 letters, digits or underscores with at least one letter (so `#2026` is not one), not glued to a word or symbol in front (`abc#def`, `##x` and `&#39;` are not). Tags are compared in lower case, each counts once per post, and only the first 10 of a post count. Any script works (`#東京`).
- The tags of a post are recorded when it is created and go when it goes. Posts written before hashtags existed were filled in by the migration.
- Both endpoints are public. `/api/posts/hashtag/{tag}` takes the tag with or without the `#`, in any case, and answers `400 { "message": "Invalid hashtag" }` for something that cannot be a tag.
- The app and the server use the same rule (`utils/text-entities.ts` and `HashtagParser.cs`), and both are tested against the same list of examples, `x-clone-frontend/src/testing/text-entities.json`, so they always agree on which words are links.

### Image uploads (API)

Images are uploaded first and attached afterwards:

```
POST /api/media            multipart form, field "file"  -> { "url": "/uploads/3f2a...c1.png" }
POST /api/posts            { "content": "...", "mediaUrls": ["/uploads/3f2a...c1.png"] }
GET  /uploads/3f2a...c1.png                               -> the image (public, cached for good)
```

- The server checks the **file itself** (its first bytes), never its name or content type: only PNG, JPEG, GIF and WebP are accepted (no SVG, which can carry scripts), up to 5 MB. It names the file itself (a random 32-character name), so nothing the client sends reaches the disk path.
- A post takes at most 4 images, each one an address this API handed out (none twice, none from other sites), and they must still exist. Deleting a post also deletes the images of the post and of all its replies.
- Images are served only from `/uploads`, with `X-Content-Type-Options: nosniff`; anything else that happens to be in the folder is a 404. Uploading needs a login and is limited per user (`RateLimiting:UploadPermitLimit`, default 30 a minute).
- They are stored in the folder `Uploads:Directory` (default `XCloneAPI/uploads`, git-ignored). That suits one server; several servers would need a shared store behind `IMediaStorage`.

### Paged lists (API)

The lists that grow without limit are read with cursors instead of `skip`: the home feed (`GET /api/posts/feed`), a profile's posts and replies (`GET /api/posts/user/{id}` and `.../replies`), a post's replies (`GET /api/posts/{id}/replies`), the notifications (`GET /api/notifications`), and a user's followers and following (`GET /api/users/{id}/followers` and `.../following`, newest follow first).

```
GET /api/posts/feed?take=20                     -> { "items": [ ... ], "nextCursor": "MTc4..." }
GET /api/posts/feed?take=20&cursor=MTc4...      -> the page right after that cursor
```

- `nextCursor` is opaque; pass it back as `cursor` to get the next page. It is `null` on the last page, so no extra empty request is needed.
- A cursor is a *position* (the sort key of the last entry you saw), not a count, so entries that appear or disappear elsewhere in the list never shift a page.
- `take` defaults to 10 (20 for notifications) and is clamped to 1-50. A malformed cursor answers `400 { "message": "Invalid cursor" }`.
- The feed and profile posts merge original posts and reposts, newest first; ties are broken by post id and reposter, so the order is total.

---

## 🧪 Tests

`XCloneAPI.Tests` holds the API integration tests. They start the real API in-process and talk to a real PostgreSQL database (not a fake), so Postgres-specific behavior is exercised for real.

```bash
dotnet test XCloneAPI.Tests
```

- **Isolated:** every run creates its own database named `xclone_it_<random>` from the real EF migrations and drops it afterwards. Your development database is never touched.
- **Which server:** the tests use the PostgreSQL server from your `XCloneAPI` user-secrets connection string (see *Configure Secrets*). To use another server, for example in CI, set `XCLONE_TEST_CONNECTION`, e.g. `Host=localhost;Port=5432;Username=postgres;Password=<password>`.
- **What is covered:** auth and tokens, password hashing and legacy-hash upgrade, posts, replies, reposts, likes, follows (including the followers and following lists), image uploads (what is accepted and refused, stored names, serving, attaching, clean-up), hashtags (the rule, the tag pages, trends, and the migration's backfill of old posts), mentions (the rule, who is named and who is told, replies, accounts that differ by case, usernames, and the backfill), notifications, timelines and cursor paging (including changes between pages and entries that share a moment), privacy (no emails or hashes in responses), rate limiting, startup safety checks (placeholder secrets), CORS, and the migrations.
- **Frontend contract:** the tests read `x-clone-frontend/src/app/services/api.service.ts` and `models/types.ts` and check that every URL the Angular app calls exists on the API and that responses contain every field the TypeScript types declare, so the two sides can't silently drift apart again.

### Frontend tests

```bash
cd x-clone-frontend
npm test
```

`npm test` runs in watch mode; add `-- --watch=false` for a single run (what CI does). No API or database is needed: components run in jsdom against Angular's fake HTTP backend, so every request a page makes is checked (URL, method, paging parameters) and answered by the test.

- **Paging:** `PagedList` (cursors, adding and removing entries locally, duplicates, cancelling, retry) and the Load more button, plus the feed, profile tabs and reply threads that use them.
- **People lists:** the followers/following page (both tabs, paging, empty and failing lists, switching profiles, the route matcher), the shared user row with its follow button, and the sidebar search and "Who to follow" widgets.
- **Notifications:** the unread-badge service (polling, hidden tabs, sign-out, stale answers), the sidebar badge, and the Notifications page.
- **Composer and images:** choosing, uploading, previewing and removing images, the limits, failing uploads, and that posting waits for uploads; the address helper that only ever loads our own images.
- **Mentions and sign-up:** mention links (only real accounts, in the spelling of their profile), the mention notifications, and the sign-up page's username rule and error messages.
- **Hashtags:** the tokenizer against the shared examples, the text component (links, no HTML, every character kept), the hashtag page, the Trends card, and the route table.
- **Post card:** what a post shows, that text is never treated as HTML, own-post rules, optimistic like and repost with rollback, delete (including a failed delete), and opening a thread.
- **Foundations:** `ApiService` requests and session handling, and the time formatter.

### Continuous integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every push to `master` and to `feature/**`, `fix/**`, `test/**`, `chore/**` and `docs/**` branches, and on every pull request, so a branch is tested before it is merged:

- **API integration tests:** `dotnet test` in Release mode against a throwaway `postgres:17` service container (via `XCLONE_TEST_CONNECTION`). The `.trx` results are uploaded as a build artifact.
- **Frontend tests and build:** `npm ci`, the unit tests, `npm run build`, and `npm audit --omit=dev --audit-level=high`, so a broken lockfile, a failing test, a build error or a new high-severity advisory in a runtime dependency fails the check.

---

## 🎨 Design Guidelines

The frontend is styled using custom CSS properties matching X's layout guidelines:
- **Backgrounds**: Dark mode base `#000000` with widgets styled at `#15181c`.
- **Accents**: Classic Twitter blue `#1d9bf0` for call-to-actions and active states.
- **Glassmorphism**: Soft background blurring for headers and modal overlays to give a modern premium interface feel.
