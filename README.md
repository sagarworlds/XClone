# X (Twitter) Clone

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
- **Replies & Threads**: Reply to any post (or to a reply). Each post has its own thread page with a reply box, and profiles have a Posts and a Replies tab.
- **Reposts**: Repost/undo with one click. Reposts show up in your followers' timelines and on your profile with a "reposted" banner.
- **User Profiles**: Custom banners, avatars, display names, follower/following counts, join dates, and an interactive edit-profile modal.
- **Social Graph**: Follow and unfollow capabilities that seamlessly update timelines and recommendation widgets.
- **User Search & Recommendations**: Dynamic real-time user search and a "Who to follow" suggestion widget.
- **Responsive Theme**: Premium, Twitter-inspired dark mode using glassmorphic UI components, smooth transitions, and custom scrollbars.

---

## 📁 Repository Structure

```
XClone/
├── XCloneAPI/                # ASP.NET Core 10.0 Web API (Backend)
│   ├── Controllers/          # API endpoints (Auth, Posts, Likes, Retweets, Follows, Users)
│   ├── Data/                 # AppDbContext configuration
│   ├── DTOs/                 # Request/Response Data Transfer Objects
│   ├── Models/               # Entity Framework database schemas (User, Post, Like, Retweet, Follow)
│   └── Services/             # Domain logic (IAuthService, IPostService, etc.)
│
├── XCloneAPI.Tests/          # API integration tests (xUnit, real PostgreSQL)
│
└── x-clone-frontend/         # Angular 19+ Single Page Application (Frontend)
    ├── src/
    │   ├── app/
    │   │   ├── components/   # Standalone UI (Login, Register, Feed, Profile, PostDetail + shared PostCard, Composer, Sidebar, Widgets)
    │   │   ├── models/       # TypeScript Interfaces
    │   │   ├── services/     # Global ApiService with Signals state management
    │   │   └── app.routes.ts # SPA routing with functional auth guards
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
- **Framework**: Angular 19+ (Standalone API & Signals)
- **Styling**: Vanilla CSS (Custom variables, transitions, and layout grids)
- **Icons**: Google Material Symbols Outlined
- **Typography**: Inter Font family

---

## ⚙️ Getting Started

### Prerequisites
- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- [Node.js v20.19.6+](https://nodejs.org/)
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

## 🧪 Tests

`XCloneAPI.Tests` holds the API integration tests. They start the real API in-process and talk to a real PostgreSQL database (not a fake), so Postgres-specific behavior is exercised for real.

```bash
dotnet test XCloneAPI.Tests
```

- **Isolated:** every run creates its own database named `xclone_it_<random>` from the real EF migrations and drops it afterwards. Your development database is never touched.
- **Which server:** the tests use the PostgreSQL server from your `XCloneAPI` user-secrets connection string (see *Configure Secrets*). To use another server, for example in CI, set `XCLONE_TEST_CONNECTION`, e.g. `Host=localhost;Port=5432;Username=postgres;Password=<password>`.
- **What is covered:** auth and tokens, password hashing and legacy-hash upgrade, posts, replies, reposts, likes, follows, timelines and paging, privacy (no emails or hashes in responses), rate limiting, startup safety checks (placeholder secrets), CORS, and the migrations.
- **Frontend contract:** the tests read `x-clone-frontend/src/app/services/api.service.ts` and `models/types.ts` and check that every URL the Angular app calls exists on the API and that responses contain every field the TypeScript types declare, so the two sides can't silently drift apart again.

---

## 🎨 Design Guidelines

The frontend is styled using custom CSS properties matching X's layout guidelines:
- **Backgrounds**: Dark mode base `#000000` with widgets styled at `#15181c`.
- **Accents**: Classic Twitter blue `#1d9bf0` for call-to-actions and active states.
- **Glassmorphism**: Soft background blurring for headers and modal overlays to give a modern premium interface feel.
