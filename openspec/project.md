# Project Context

## Purpose
SoundAudio - Ứng dụng nghe nhạc trực tuyến với các tính năng:
- Phát nhạc streaming từ MongoDB GridFS
- Quản lý playlist cá nhân
- Quản lý album và nghệ sĩ
- Hệ thống người dùng với phân quyền (Admin, User, Artist)
- Hỗ trợ đăng nhập Google OAuth

## Tech Stack

### Backend
- **.NET 9.0** (Preview) - Framework chính
- **ASP.NET Core Web API** - REST API cho mobile/frontend
- **ASP.NET Core MVC** - Web frontend
- **ASP.NET Core Identity** - Authentication & Authorization
- **JWT Bearer** - Token-based authentication cho API
- **SignalR** - Realtime communication

### Database
- **MongoDB** - Lưu trữ music files, playlists, albums (GridFS cho file storage)
- **SQL Server** - Lưu trữ Identity users và roles
- **Entity Framework Core 9** - ORM cho SQL Server

### Frontend
- **Razor Views** - Server-side rendering
- **Bootstrap/CSS** - Styling
- **JavaScript** - Client-side interactivity

### Tools & Libraries
- **Swagger/OpenAPI** - API documentation
- **Google.Apis.Auth** - Google OAuth integration
- **MongoDB.Driver 3.5** - MongoDB client

## Project Structure
```
SoundAudio/
├── MyWebApp.Api/          # REST API (port 5289)
│   ├── Controllers/       # API Controllers
│   ├── Hubs/             # SignalR Hubs
│   └── Models/           # Request/Response DTOs
├── MyWebApp.Mvc/          # MVC Frontend (port 5255)
│   ├── Controllers/       # MVC Controllers
│   ├── Views/            # Razor Views
│   ├── Services/         # API Client Services
│   └── wwwroot/          # Static files (CSS, JS)
├── MyWebApp.Models/       # Shared Domain Models
├── MyWebApp.Data/         # Data Access Layer (EF DbContext)
├── MyWebApp.Services/     # Business Logic Services
└── openspec/             # OpenSpec documentation
```

## Project Conventions

### Code Style
- **Language**: C# với nullable reference types enabled
- **Naming**: PascalCase cho classes, methods, properties; camelCase cho local variables
- **Async**: Tất cả database operations phải async với suffix `Async`
- **API Response**: Sử dụng `ApiResponse<T>` wrapper cho consistent response format
- **Comments**: Sử dụng XML documentation cho public APIs

### Architecture Patterns
- **Layered Architecture**: Models → Data → Services → API/MVC
- **Repository Pattern**: Services trực tiếp access MongoDB collections
- **Dependency Injection**: Tất cả services được register trong Program.cs
- **API-first**: MVC frontend gọi API thông qua ApiService classes

### Authorization Pattern
- **Class-level**: `[Authorize(Roles = "Admin")]` cho admin controllers
- **Method-level**: `[AllowAnonymous]` cho public endpoints (GET music, images)
- **Dual Auth**: API hỗ trợ cả JWT và Cookie authentication

### Testing Strategy
- Chưa có unit tests
- Manual testing qua Swagger UI cho API
- Manual testing qua browser cho MVC

### Git Workflow
- **Main branch**: `main`
- **Commit style**: Tiếng Việt, mô tả ngắn gọn
- **No CI/CD**: Build và deploy thủ công

## Domain Context

### Core Entities
- **MusicFile**: Bài hát với metadata, GridFS file reference
- **Playlist**: Danh sách phát của user
- **Album**: Tập hợp bài hát của nghệ sĩ
- **Author**: Nghệ sĩ/tác giả
- **ApplicationUser**: User Identity với FullName, AvatarUrl
- **Lyric**: Lời bài hát (LRC format)

### User Roles
- **Admin**: Quản lý nhạc, album, users
- **Người dùng**: Nghe nhạc, tạo playlist
- **Nghệ sĩ**: Upload và quản lý nhạc của mình

### File Storage
- Music files: MongoDB GridFS (`fs.files`, `fs.chunks`)
- Images: MongoDB GridFS
- URL patterns:
  - Music: `/api/music/{fileName}` hoặc `/api/music/stream/{id}`
  - Images: `/api/images/{fileName}`

## Important Constraints

### Security
- Admin endpoints PHẢI có `[Authorize(Roles = "Admin")]`
- KHÔNG dùng `[AllowAnonymous]` cho endpoints thay đổi data
- JWT secret key trong appsettings (cần move sang secrets)

### Performance
- Sử dụng `enableRangeProcessing: true` cho streaming music
- GridFS cho large file storage

### Compatibility
- API phục vụ cả web frontend và mobile app
- Dual authentication (JWT cho mobile, Cookie cho web)

## External Dependencies

### Services
- **MongoDB**: `mongodb://localhost:27017` - Database chính
- **SQL Server**: Local instance cho Identity
- **Google OAuth**: Đăng nhập bằng Google account

### Ports
- **API**: http://localhost:5289
- **MVC**: http://localhost:5255

### Configuration Files
- `appsettings.json`: Connection strings, JWT settings
- Sensitive data nên move sang User Secrets hoặc Environment Variables
