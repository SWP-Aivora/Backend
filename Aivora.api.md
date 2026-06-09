# Aivora.api

Main API Gateway and Controllers for the Aivora Backend.

## Overview
This project serves as the entry point for all client requests. It handles authentication, routing, and real-time communication via SignalR.

## Key Components
- **Controllers**: Handles HTTP requests (Admin, AI, Auth, Job, etc.)
- **Hubs**: SignalR hubs for real-time chat (`ChatHub`).
- **Middlewares**: Custom exception handling and security.
- **Extensions**: JWT and Claims helper methods.

## Related
- [[Aivora-Backend-MOC|Backend MOC]]
- [[README|Project README]]
