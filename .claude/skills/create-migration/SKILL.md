---
name: create-migration
description: Tạo mới EF Core migration với validation script
disable-model-invocation: true
---

# Create Migration Skill

Tạo mới EF Core migration cho PostgreSQL database với validation script để đảm bảo syntax đúng.

## Usage

```bash
/create-migration
```

## Process

1. Phân tích model changes trong Aivora.Repositories
2. Tạo migration với dotnet ef
3. Validate syntax PostgreSQL
4. Kiểm tra references giữa entities
5. Chạy dotnet ef database update preview

## Commands

```bash
# Thêm vào settings.json
{
  "skills": {
    "create-migration": {
      "command": "dotnet ef migrations add {name} --project Aivora.Repositories --startup-project Aivora.api"
    }
  }
}
```

## Validation Script

```bash
# Validate migration syntax
psql -h localhost -U postgres -d aivora -f MigrationName.sql

# Check references
grep -r "public.*\.*Id" Aivora.Repositories/Entities/
```