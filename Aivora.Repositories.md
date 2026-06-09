# Aivora.Repositories

Data access layer and database entities for Aivora.

## Overview
Built with Entity Framework Core and PostgreSQL, this layer manages the data persistence and entity definitions.

## Key Entities
- **User**: Core user identity.
- **JobPost**: Details of posted jobs.
- **Proposal**: Expert bids on jobs.
- **Project**: Active engagements between clients and experts.
- **Wallet & Payment**: Financial transaction data.

## Features
- **Interceptors**: Auditable entity tracking (CreatedBy, ModifiedBy).
- **Configurations**: Fluent API mappings for database constraints.

## Related
- [[Aivora-Backend-MOC|Backend MOC]]
- [[README|Project README]]
