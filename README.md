# TrendMarket Backend

ASP.NET Core Web API backend for **TrendMarket**, a demo multi-vendor e-commerce
platform. It serves the [TrendMarketApp](https://github.com/fatmana-ozcan/Trend-Market-App)
Expo/React Native client: product catalog, cart, checkout, order/shipment tracking,
product reviews, a coupon wallet, "30-day lowest price" tracking, and a
"recently viewed" feed.

## Tech Stack

| Layer          | Technology |
|----------------|------------|
| Runtime        | .NET 8 / ASP.NET Core Web API |
| Database       | SQLite via EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite`) |
| Auth           | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Password hashing | BCrypt.Net-Next |
| API docs       | Swashbuckle (Swagger UI at `/swagger`) |

## Project Structure

```
TrendMarketServer/
├── Controllers/     # HTTP endpoints (one controller per resource/domain)
├── Models/          # EF Core entities
├── Data/            # AppDbContext, DbSeeder, and in-memory stores (cart, SMS codes)
├── Services/        # TokenService (JWT issuance)
├── Migrations/       # EF Core migrations (schema history)
├── Properties/       # launchSettings.json
├── wwwroot/uploads/   # user-uploaded review photos (gitignored)
├── Program.cs         # composition root: DI, middleware, migrate + seed on boot
└── appsettings.json    # connection string, JWT signing config
```

### Controllers

| Controller | Route | Auth | Responsibility |
|---|---|---|---|
| `AuthController` | `/api/auth` | public (issues JWT) | Seller register / login / forgot-password |
| `CustomerAuthController` | `/api/customer-auth` | public (issues JWT) | Customer register / login / forgot-password |
| `ProductsController` | `/api/Products` | mixed | Catalog, search, cart, favorites, ratings, seller Q&A, reviews, recently-viewed, 30-day price drop |
| `SellerProductsController` | `/api/seller` | Seller | CRUD own products, dashboard stats (revenue/profit) |
| `SellerShipmentsController` | `/api/seller/shipments` | Seller | Update shipment status / courier info |
| `AddressesController` | `/api/addresses` | Customer | CRUD delivery addresses |
| `OrdersController` | `/api/orders` | Customer | Two-step checkout, order history, coupon earn/spend |
| `CouponsController` | `/api/coupons` | Customer | Coupon wallet balance & transaction history |

## Data Model

```mermaid
erDiagram
    SELLER ||--o{ PRODUCT : lists
    SELLER ||--o{ SHIPMENT : ships

    PRODUCT ||--o{ PRODUCT_PRICE_HISTORY : "price changes"
    PRODUCT ||--o{ PRODUCT_REVIEW : reviews
    PRODUCT ||--o{ PRODUCT_VIEW : "viewed by"
    PRODUCT ||--o{ ORDER_ITEM : "sold as"

    CUSTOMER ||--o{ ADDRESS : owns
    CUSTOMER ||--o{ ORDER : places
    CUSTOMER ||--o{ PRODUCT_REVIEW : writes
    CUSTOMER ||--o{ PRODUCT_VIEW : browses
    CUSTOMER ||--o{ COUPON_TRANSACTION : "earns / spends"

    ORDER ||--o{ SHIPMENT : "split into"
    ORDER ||--o{ COUPON_TRANSACTION : "generates"
    SHIPMENT ||--o{ ORDER_ITEM : contains

    SELLER {
        int Id PK
        string StoreName
        string Email
        string Phone
        string PasswordHash
    }
    PRODUCT {
        int Id PK
        int SellerId FK
        string Name
        string Category
        decimal Price
        decimal CostPrice
        int Stock
        int SoldCount
        int RatingSum
        int RatingCount
    }
    PRODUCT_PRICE_HISTORY {
        int Id PK
        int ProductId FK
        decimal Price
        datetime RecordedAt
    }
    PRODUCT_REVIEW {
        int Id PK
        int ProductId FK
        int CustomerId FK
        string Comment
        string ImageUrl
    }
    PRODUCT_VIEW {
        int Id PK
        int CustomerId FK
        int ProductId FK
        datetime ViewedAt
    }
    CUSTOMER {
        int Id PK
        string Name
        string Email
        string Phone
        string PasswordHash
    }
    ADDRESS {
        int Id PK
        int CustomerId FK
        string Title
        string City
        string District
        bool IsDefault
    }
    ORDER {
        int Id PK
        int CustomerId FK
        decimal TotalAmount
        decimal CouponUsed
        datetime CreatedAt
    }
    SHIPMENT {
        int Id PK
        int OrderId FK
        int SellerId FK
        string Status
        string CourierName
        string TrackingNumber
    }
    ORDER_ITEM {
        int Id PK
        int OrderId FK
        int ShipmentId FK
        int ProductId FK
        int Quantity
        decimal UnitPrice
    }
    COUPON_TRANSACTION {
        int Id PK
        int CustomerId FK
        int OrderId FK
        decimal Amount
        string Description
    }
```

## Request Flow

```mermaid
flowchart TD
    Client["TrendMarketApp<br/>(Expo client)"] -->|HTTPS + JSON| CORS["CORS middleware<br/>(AllowAll)"]
    CORS --> JwtMw["JWT Bearer auth middleware"]
    JwtMw --> Router["ASP.NET Core routing"]

    Router --> Public["Public endpoints<br/>(catalog, auth)"]
    Router --> CustomerOnly["[Authorize(Roles=Customer)]<br/>orders, addresses, coupons,<br/>recently-viewed, reviews"]
    Router --> SellerOnly["[Authorize(Roles=Seller)]<br/>seller products, shipments"]

    Public --> Ctrl["Controllers"]
    CustomerOnly --> Ctrl
    SellerOnly --> Ctrl

    Ctrl --> EF["AppDbContext (EF Core)"]
    Ctrl --> CartStore["CartStore<br/>(static in-memory dict)"]
    Ctrl --> VerifStore["VerificationStore<br/>(static in-memory dict,<br/>simulates SMS codes)"]
    Ctrl --> TokenSvc["TokenService<br/>(issues JWTs)"]

    EF --> DB[("SQLite<br/>trendmarket.db")]
```

## Auth Flow

Two independent identities share one JWT scheme, distinguished by a `role` claim
(`Customer` or `Seller`); `[Authorize(Roles = "...")]` on each controller enforces
the split.

```mermaid
sequenceDiagram
    participant C as Client
    participant A as Auth Controller<br/>(Customer or Seller)
    participant DB as SQLite
    participant T as TokenService

    C->>A: POST /register {email, password, ...}
    A->>DB: check email uniqueness
    A->>A: BCrypt.HashPassword
    A->>DB: insert Customer/Seller
    A->>T: GenerateToken(entity)
    T-->>A: JWT (NameIdentifier, Email, Role, name/storeName claims)
    A-->>C: { token, id, name }

    C->>A: POST /login {email, password}
    A->>DB: find by email
    A->>A: BCrypt.Verify(password, hash)
    A->>T: GenerateToken(entity)
    T-->>A: JWT
    A-->>C: { token, id, name }

    Note over C: subsequent requests include the token as an Authorization Bearer header
```

## Checkout Flow

Checkout is a two-step, card-verification-style flow. Cart contents live in the
process-wide `CartStore` (see **Design Notes**), not per-customer.

```mermaid
sequenceDiagram
    participant C as Client
    participant O as OrdersController
    participant V as VerificationStore
    participant DB as SQLite

    C->>O: POST /checkout/request-code {address, card, couponAmountToUse}
    O->>O: validate card fields (Luhn-ish format checks)
    O->>DB: validate address belongs to customer
    O->>DB: compute cart total + coupon balance
    O->>O: reject if couponAmountToUse > balance or > total
    O->>V: store {code, payload, expiresAt: +5min}
    O-->>C: { demoCode } (simulated SMS)

    C->>O: POST /checkout/confirm {code}
    O->>V: validate code + expiry
    O->>DB: re-check stock for every cart line
    O->>DB: create Order (total - couponUsed)
    loop per seller in cart
        O->>DB: create Shipment
        loop per product from that seller
            O->>DB: create OrderItem
            O->>DB: decrement Stock, increment SoldCount/Revenue
            O->>DB: credit CouponTransaction (+10% of line price)
        end
    end
    alt couponUsed > 0
        O->>DB: debit CouponTransaction (-couponUsed)
    end
    O->>O: clear CartStore
    O-->>C: { orderId }
```

## Getting Started

```bash
dotnet restore
dotnet run
```

- Applies pending EF Core migrations and seeds demo data automatically on startup
  (see `DbSeeder`).
- Swagger UI: `http://localhost:5050/swagger`
- Seeded demo seller account: `demo@trendmarket.com` / `demo1234`

## Design Notes

- **Cart is a single shared in-memory dictionary** (`Data/CartStore.cs`), not
  per-customer — a deliberate simplification for this demo; browsing/adding to
  cart requires no login, only the final "confirm order" step does.
- **SQLite + EF Core can't `SUM()` `decimal` columns server-side** (`NotSupportedException`,
  since SQLite stores them as `TEXT`). Coupon balance is aggregated client-side
  after materializing the rows (see `CouponsController`/`OrdersController`).
- **`appsettings.json` ships a placeholder JWT signing key** for out-of-the-box
  local running; replace it before any real deployment.
- **`ProductPriceHistory`** is written on every product create/update and backfilled
  idempotently at startup, powering the "lowest price in the last 30 days" badge
  shown only on products whose price has genuinely dropped.
