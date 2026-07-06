# X (Twitter) Clone

A modern, full-stack clone of X (formerly Twitter) featuring a secure ASP.NET Core Web API backend, a PostgreSQL database, and a beautiful Angular single-page application (SPA) frontend.

---

## 🚀 Features

- **Authentication & Security**: Secure user registration and login using JWT (JSON Web Tokens).
- **Home Timeline Feed**: A live feed of posts from the users you follow, featuring a character-limited (280 chars) tweet composer.
- **Interactions**: Fast, optimistic UI updates for liking/unliking posts.
- **User Profiles**: Custom banners, avatars, display names, follower/following counts, join dates, and an interactive edit-profile modal.
- **Social Graph**: Follow and unfollow capabilities that seamlessly update timelines and recommendation widgets.
- **User Search & Recommendations**: Dynamic real-time user search and a "Who to follow" suggestion widget.
- **Responsive Theme**: Premium, Twitter-inspired dark mode using glassmorphic UI components, smooth transitions, and custom scrollbars.

---

## 📁 Repository Structure

```
XClone/
├── XCloneAPI/                # ASP.NET Core 10.0 Web API (Backend)
│   ├── Controllers/          # API endpoints (Auth, Posts, Likes, Follows, Users)
│   ├── Data/                 # AppDbContext configuration
│   ├── DTOs/                 # Request/Response Data Transfer Objects
│   ├── Models/               # Entity Framework database schemas (User, Post, Like, Follow)
│   └── Services/             # Domain logic (IAuthService, IPostService, etc.)
│
└── x-clone-frontend/         # Angular 19+ Single Page Application (Frontend)
    ├── src/
    │   ├── app/
    │   │   ├── components/   # Standalone UI Views (Login, Register, Feed, Profile)
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
- **Object Mapping**: AutoMapper (optional)

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

### Database Setup

1. Make sure your PostgreSQL server is running.
2. Open `XCloneAPI/appsettings.json` and configure your database connection string:
   ```json
   "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=x_clone_db;Username=postgres;Password=YOUR_PASSWORD"
   }
   ```
3. Run the migrations to initialize the database:
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
   *The API will start and listen on `http://localhost:5000` (or `https://localhost:7040` depending on launch profile).*

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

## 🎨 Design Guidelines

The frontend is styled using custom CSS properties matching X's layout guidelines:
- **Backgrounds**: Dark mode base `#000000` with widgets styled at `#15181c`.
- **Accents**: Classic Twitter blue `#1d9bf0` for call-to-actions and active states.
- **Glassmorphism**: Soft background blurring for headers and modal overlays to give a modern premium interface feel.
