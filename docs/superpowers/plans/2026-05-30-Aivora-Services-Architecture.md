# Aivora.Services Architecture & Implementation Plan

## 1. Overview
The `Aivora.Services` project will follow the `UGem-Backend` reference architecture. This means organizing each business domain into its own folder containing `IService.cs`, `Service.cs`, `Request.cs`, and `Response.cs`. 

## 2. Core Infrastructure (Base & Models)
We need to set up the foundational classes used across all services:
- **`Base/Request.cs`**: Contains `PageRequest` for pagination.
- **`Base/Response.cs`**: Contains `PageResult<T>` for paginated responses.
- **`Models/ApiResponse.cs`**: A standardized API envelope (`Success`, `Message`, `Data`, `Errors`, `TraceId`).
- **`Exceptions/`**: Standardized domain exceptions (`NotFoundException`, `ValidationException`, `UnauthorizedException`) to be caught by a global exception middleware.

## 3. Services to Implement (Phase 1)
For the current task, we will focus on the **Identity & Authentication** domain.

### 3.1 JwtService
Handles generating and validating JWT tokens.
- **Files**: `JwtService/IService.cs`, `JwtService/Service.cs`
- **Responsibilities**: Generate Access Token, Generate Refresh Token.

### 3.2 IdentityService
Handles User Registration, Login, and Profile Management.
- **Files**: `IdentityService/IService.cs`, `IdentityService/Service.cs`, `IdentityService/Request.cs`, `IdentityService/Response.cs`
- **Models**:
  - `LoginRequest` (Email, Password)
  - `RegisterRequest` (Email, Password, Role [Client/Expert], FirstName, LastName)
  - `TokenResponse` (AccessToken, RefreshToken)
- **Dependencies**: `AppDbContext`, `IJwtService`, `BCrypt` (for password hashing).

## 4. Implementation Steps
1. Create `Base` and `Models` directories with foundational classes.
2. Install necessary NuGet packages in `Aivora.Services`:
   - `BCrypt.Net-Next`
   - `Microsoft.AspNetCore.Http.Abstractions` (for HttpContext if needed)
   - `System.IdentityModel.Tokens.Jwt`
   - `Microsoft.EntityFrameworkCore`
3. Implement `JwtService`.
4. Implement `IdentityService` (Registration, Login).
5. Register services in `Aivora.api/Program.cs`.
