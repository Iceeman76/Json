---
apply: always
---

# C# Coding Rules for AI Agent

## 🎯 Goal
Generate modern, clean, and production-ready C# code that is:
- Readable
- Maintainable
- Testable
- Idiomatic for .NET (latest LTS)

---

## 🧱 General Principles

- Do not put comments in the code
- Code should be in English
- Prefer clarity over cleverness
- Avoid unnecessary abstractions
- Write self-explanatory code (minimize comments)
- Use expressive naming
- Follow SOLID where reasonable (not dogmatically)

---

## 🧾 Naming Conventions

- Use **PascalCase** for:
    - Classes
    - Methods
    - Properties
- Use **camelCase** for:
    - Local variables
    - Parameters
- Use **meaningful names**:
    - ❌ `x`, `data`, `obj`
    - ✅ `customer`, `orderItems`, `totalPrice`

---

## 🧩 Code Style

- Use **file-scoped namespaces**
- Namespaces should correspond to file location
- Records and classes should be sealed by default
- Prefer **records** for immutable data
- Prefer **init-only setters** where possible
- Use **expression-bodied members** when it improves readability
- Keep methods small and focused

```csharp
public record CustomerDto(string Id, string Name);
