# DriveAndGo System Architecture & Technical Documentation
### Complete UML, ERD, and Data Flow Diagrams (DFD) for System Documentation & Thesis Defense

---

## Table of Contents
1. [Executive System Overview](#1-executive-system-overview)
2. [UML Use Case Diagram (Like Reference Sample)](#2-uml-use-case-diagram)
3. [UML Class Diagram (Domain Model & OOP Architecture)](#3-uml-class-diagram)
4. [UML Sequence Diagram (End-to-End Booking & Telematics Flow)](#4-uml-sequence-diagram)
5. [Complete Entity-Relationship Diagram (ERD)](#5-complete-entity-relationship-diagram-erd)
6. [Comprehensive Data Dictionary](#6-comprehensive-data-dictionary)
7. [Data Flow Diagrams (DFD)](#7-data-flow-diagrams-dfd)
   - [7.1 DFD Level 0: Context Diagram](#71-dfd-level-0-context-diagram)
   - [7.2 DFD Level 1: System Decomposition Diagram](#72-dfd-level-1-system-decomposition-diagram)
   - [7.3 DFD Level 2: Booking & Payment Subsystem](#73-dfd-level-2-booking--payment-subsystem)

---

## 1. Executive System Overview

**DriveAndGo** is an enterprise-grade car rental and fleet management ecosystem consisting of:
* **Admin Operations Center (Desktop & Web)**: WinForms/WebView2 hybrid React dashboard for real-time telematics, fleet assignment, revenue tracking, OCR document verification, dynamic pricing, and automated compliance.
* **Customer & Driver Mobile Application**: React Native mobile client for vehicle discovery, instant reservation, digital key check-in, in-trip GPS routing, driver bidding, split billing, and digital payment receipts.
* **Backend Web API Core**: .NET 10 ASP.NET Web API with PostgreSQL database, real-time SignalR hubs, JWT authentication, and multi-tier cloud storage.

---

## 2. UML Use Case Diagram

The diagram below reflects the standard industry use case modeling format (matching the reference template), detailing the functional boundaries of DriveAndGo and the interactions of its 4 primary actors:
* **Admin / Fleet Manager**
* **Customer / Renter**
* **Chauffeur / Driver**
* **External Systems (Payment Gateways, Weather & GPS Telematics)**

```mermaid
flowchart LR
    %% Actors
    subgraph LeftActors [" "]
        direction TB
        Admin["fa:fa-user-tie Admin / Fleet Manager"]
        Driver["fa:fa-id-badge Chauffeur / Driver"]
    end

    subgraph SystemBoundary [" DriveAndGo Core System Boundary "]
        direction TB
        UC1(["UC-01: Authentication & RBAC Login"])
        UC2(["UC-02: Manage Fleet & Add Vehicle Units"])
        UC3(["UC-03: Live GPS Telematics & Geofence Monitor"])
        UC4(["UC-04: Browse Fleet & Book Vehicle"])
        UC5(["UC-05: Select Add-Ons & Apply Promo Code"])
        UC6(["UC-06: Process Payment & Split Billing"])
        UC7(["UC-07: Verify Payment & Approve Rental"])
        UC8(["UC-08: Submit Chauffeur Bid for Trips"])
        UC9(["UC-09: Assign Chauffeur to Booking"])
        UC10(["UC-10: In-Trip Chat & Dispatch Messaging"])
        UC11(["UC-11: Report Incident & Damage Claim"])
        UC12(["UC-12: Vehicle Health & Maintenance Logging"])
        UC13(["UC-13: OCR Driver Document Verification"])
        UC14(["UC-14: Rate Vehicle & Driver Service"])
        UC15(["UC-15: View Analytics & Revenue Reports"])
        UC16(["UC-16: Blockchain Contract & Audit Log"])
    end

    subgraph RightActors [" "]
        direction TB
        Customer["fa:fa-user Customer / Renter"]
        ExternalSensors["fa:fa-satellite IoT Telematics / Weather API"]
        PaymentGateway["fa:fa-credit-card Payment Gateways (GCash / Maya / Bank)"]
    end

    %% Admin Connections
    Admin --> UC1
    Admin --> UC2
    Admin --> UC3
    Admin --> UC7
    Admin --> UC9
    Admin --> UC10
    Admin --> UC12
    Admin --> UC13
    Admin --> UC15
    Admin --> UC16

    %% Driver Connections
    Driver --> UC1
    Driver --> UC8
    Driver --> UC10
    Driver --> UC11
    Driver --> UC14

    %% Customer Connections
    Customer --> UC1
    Customer --> UC4
    Customer --> UC5
    Customer --> UC6
    Customer --> UC10
    Customer --> UC11
    Customer --> UC14

    %% External Systems Connections
    ExternalSensors --> UC3
    ExternalSensors --> UC12
    PaymentGateway --> UC6
    PaymentGateway --> UC7
```

---

## 3. UML Class Diagram

This diagram showcases the Object-Oriented Domain Entities and their business logic associations in the C# .NET 10 API backend:

```mermaid
classDiagram
    class User {
        +int UserId
        +string FullName
        +string Email
        +string PasswordHash
        +string Phone
        +string Role
        +string IdPhotoUrl
        +DateTime CreatedAt
        +Register()
        +Login()
        +UpdateProfile()
    }

    class Driver {
        +int DriverId
        +int UserId
        +string LicenseNo
        +string LicensePhotoUrl
        +string Status
        +decimal RatingAvg
        +int TotalTrips
        +decimal CashOnHand
        +SubmitBid()
        +UpdateStatus()
    }

    class Vehicle {
        +int VehicleId
        +string PlateNo
        +string Brand
        +string Model
        +string Type
        +int CC
        +string Status
        +decimal RatePerDay
        +decimal RateWithDriver
        +string PhotoUrl
        +double Latitude
        +double Longitude
        +int HealthScore
        +int OdometerKm
        +decimal ExpresswayRfidBalance
        +UpdateLocation()
        +LockTelematics()
        +FlagMaintenance()
    }

    class Rental {
        +int RentalId
        +int CustomerId
        +int VehicleId
        +int? DriverId
        +DateTime StartDate
        +DateTime EndDate
        +string Status
        +decimal TotalAmount
        +string PaymentMethod
        +string PaymentStatus
        +string QrCode
        +Approve()
        +Reject()
        +StartTrip()
        +CompleteRental()
    }

    class Transaction {
        +int TransactionId
        +int RentalId
        +decimal Amount
        +string Type
        +string Method
        +string ProofUrl
        +string Status
        +DateTime PaidAt
        +VerifyProof()
        +ProcessRefund()
    }

    class DriverBid {
        +int BidId
        +int RentalId
        +int DriverId
        +decimal BidAmount
        +string Status
        +DateTime CreatedAt
        +AcceptBid()
        +DeclineBid()
    }

    class AddOn {
        +int AddOnId
        +string Name
        +decimal DailyRate
        +decimal FlatRate
        +bool IsActive
    }

    class RentalAddOn {
        +int RentalAddOnId
        +int RentalId
        +int AddOnId
        +int Quantity
        +decimal Subtotal
    }

    class MaintenanceLog {
        +int MaintenanceId
        +int VehicleId
        +string ServiceType
        +decimal Cost
        +int OdometerAtService
        +DateTime ServiceDate
    }

    class Issue {
        +int IssueId
        +int RentalId
        +int ReporterId
        +string IssueType
        +string Description
        +string ImageUrl
        +string Status
        +Resolve()
    }

    class BlockchainBlock {
        +int BlockIndex
        +string PrevHash
        +string CurrentHash
        +string ActionType
        +DateTime Timestamp
        +string DataPayload
    }

    %% Relationships
    User "1" <|-- "1" Driver : specializes
    User "1" --> "0..*" Rental : books as Customer
    Driver "1" --> "0..*" Rental : drives
    Vehicle "1" --> "0..*" Rental : rented in
    Rental "1" --> "1..*" Transaction : generates
    Rental "1" --> "0..*" DriverBid : receives
    Rental "1" --> "0..*" RentalAddOn : includes
    AddOn "1" --> "0..*" RentalAddOn : defines
    Vehicle "1" --> "0..*" MaintenanceLog : serviced by
    Rental "1" --> "0..*" Issue : reports
    Rental "1" --> "0..*" BlockchainBlock : audited by
```

---

## 4. UML Sequence Diagram
### End-to-End Booking, Chauffeur Bidding, Telematics & Settlement

```mermaid
sequenceDiagram
    autonumber
    actor Customer as Renter (Customer)
    actor Admin as Fleet Admin
    actor Driver as Chauffeur (Driver)
    participant API as DriveAndGo Web API
    participant Hub as SignalR Realtime Hub
    participant DB as PostgreSQL Database
    participant Storage as Cloud Object Storage

    Customer->>API: POST /api/rentals (Vehicle, Dates, With-Driver)
    API->>DB: INSERT INTO rentals (status='pending')
    API->>Hub: Broadcast 'NewBookingAlert'
    Hub-->>Admin: Show in Admin Dashboard Pending Deck
    Hub-->>Driver: Broadcast Chauffeur Trip Invitation

    Driver->>API: POST /api/driver-bids (BidAmount)
    API->>DB: INSERT INTO driver_bids (status='pending')
    API-->>Customer: Notify new driver offer received

    Admin->>API: POST /api/rentals/{id}/approve (Assign Driver)
    API->>DB: UPDATE rentals SET status='approved', driver_id=X
    API->>DB: UPDATE vehicles SET status='rented'
    API->>DB: UPDATE drivers SET status='assigned'
    API->>Hub: Notify Driver & Customer 'Booking Approved'

    Customer->>Storage: Upload Payment Receipt (GCash/Maya)
    Storage-->>Customer: Return https://storage.../receipt.jpg
    Customer->>API: POST /api/transactions (RentalId, Amount, ProofUrl)
    API->>DB: INSERT INTO transactions (status='pending')

    Admin->>API: POST /api/transactions/{id}/verify
    API->>DB: UPDATE transactions SET status='paid'
    API->>DB: UPDATE rentals SET payment_status='paid'
    API->>DB: INSERT INTO blockchain_ledger (Hash, Action='PaymentVerified')
    API-->>Customer: Issue Digital QR Contract & Unlocks Telematics
```

---

## 5. Complete Entity-Relationship Diagram (ERD)

This Crow's Foot ERD models all 23 database tables, their primary keys (`PK`), foreign keys (`FK`), and cardinalities across the complete operational database:

```mermaid
erDiagram
    USERS ||--o| DRIVERS : "has profile"
    USERS ||--o{ RENTALS : "books"
    USERS ||--o{ ISSUES : "reports"
    USERS ||--o{ NOTIFICATIONS : "receives"
    USERS ||--o{ MESSAGES : "sends"
    USERS ||--o{ RATINGS : "submits as customer"

    DRIVERS ||--o{ DRIVER_PAYOUT_ACCOUNTS : "owns"
    DRIVERS ||--o{ DRIVER_EMERGENCY_CONTACTS : "lists"
    DRIVERS ||--o{ DRIVER_DOCUMENTS : "submits"
    DRIVERS ||--o{ DRIVER_INCIDENTS : "penalized"
    DRIVERS ||--o{ DRIVER_BIDS : "places"
    DRIVERS ||--o{ RENTALS : "chauffeurs"
    DRIVERS ||--o{ RATINGS : "evaluated"

    VEHICLES ||--o{ RENTALS : "assigned to"
    VEHICLES ||--o{ MAINTENANCE_LOGS : "services"
    VEHICLES ||--o{ EXPENSES : "incurs"
    VEHICLES ||--o{ LOCATION_LOGS : "tracks"
    VEHICLES ||--o{ TOLL_LOGS : "crosses"
    VEHICLES ||--o{ RATINGS : "evaluated"

    RENTALS ||--o{ TRANSACTIONS : "bills"
    RENTALS ||--o{ RENTAL_ADD_ONS : "selects"
    RENTALS ||--o{ DRIVER_BIDS : "solicits"
    RENTALS ||--o{ EXTENSIONS : "extends"
    RENTALS ||--o{ ISSUES : "flags"
    RENTALS ||--o{ SPLIT_PAYMENTS : "splits"
    RENTALS ||--o{ MESSAGES : "contains"
    RENTALS ||--o{ RATINGS : "reviews"
    RENTALS ||--o{ BLOCKCHAIN_LEDGER : "audits"

    ADD_ONS ||--o{ RENTAL_ADD_ONS : "cataloged in"

    USERS {
        int user_id PK
        string full_name
        string email UK
        string password_hash
        string phone
        string role
        string id_photo_url
        string selfie_photo_url
        timestamptz created_at
    }

    DRIVERS {
        int driver_id PK
        int user_id FK
        string license_no UK
        string license_photo_url
        string status
        numeric rating_avg
        int total_trips
        numeric cash_on_hand
        string verification_status
    }

    VEHICLES {
        int vehicle_id PK
        string plate_no UK
        string brand
        string model
        string type
        int cc
        string status
        numeric rate_per_day
        numeric rate_with_driver
        string photo_url
        string color
        double latitude
        double longitude
        int odometer_km
        int health_score
        numeric expressway_rfid_balance
    }

    RENTALS {
        int rental_id PK
        int customer_id FK
        int vehicle_id FK
        int driver_id FK
        timestamptz start_date
        timestamptz end_date
        string destination
        string status
        numeric total_amount
        string payment_method
        string payment_status
        string qr_code
        timestamptz created_at
    }

    TRANSACTIONS {
        int transaction_id PK
        int rental_id FK
        numeric amount
        string type
        string method
        string proof_url
        string status
        timestamptz paid_at
    }

    ADD_ONS {
        int add_on_id PK
        string name
        numeric daily_rate
        numeric flat_rate
        boolean is_active
    }

    RENTAL_ADD_ONS {
        int rental_addon_id PK
        int rental_id FK
        int add_on_id FK
        int quantity
        numeric subtotal
    }

    DRIVER_BIDS {
        int bid_id PK
        int rental_id FK
        int driver_id FK
        numeric bid_amount
        string status
        timestamptz created_at
    }

    EXTENSIONS {
        int extension_id PK
        int rental_id FK
        int added_days
        numeric added_fee
        string status
        timestamptz requested_at
    }

    ISSUES {
        int issue_id PK
        int rental_id FK
        int reporter_id FK
        string issue_type
        text description
        string image_url
        string status
        timestamptz reported_at
    }

    MAINTENANCE_LOGS {
        int log_id PK
        int vehicle_id FK
        string service_type
        numeric cost
        int odometer_km
        timestamptz service_date
    }

    EXPENSES {
        int expense_id PK
        int vehicle_id FK
        string category
        numeric amount
        string receipt_url
        timestamptz incurred_at
    }

    SPLIT_PAYMENTS {
        int split_id PK
        int rental_id FK
        string payer_name
        string payer_phone
        numeric share_amount
        string status
    }

    BLOCKCHAIN_LEDGER {
        int block_id PK
        int block_index
        string prev_hash
        string current_hash
        int rental_id FK
        string action_type
        jsonb payload_data
        timestamptz timestamp
    }
```

---

## 6. Comprehensive Data Dictionary

| Table Name | Primary Key | Foreign Keys | Key Columns & Types | Description |
| :--- | :--- | :--- | :--- | :--- |
| **`users`** | `user_id` (SERIAL) | None | `email` (VARCHAR), `role` (VARCHAR), `password_hash` (TEXT) | System actors (Customers, Drivers, Admins, SuperAdmins). |
| **`drivers`** | `driver_id` (SERIAL) | `user_id` ➔ `users` | `license_no` (VARCHAR), `status` (VARCHAR), `rating_avg` (NUMERIC) | Professional chauffeur workforce profile & performance metrics. |
| **`driver_documents`**| `doc_id` (SERIAL) | `driver_id` ➔ `drivers` | `doc_type` (VARCHAR), `file_url` (TEXT), `status` (VARCHAR) | LTO license, NBI, police, drug test compliance vault. |
| **`vehicles`** | `vehicle_id` (SERIAL) | None | `plate_no` (VARCHAR), `brand` (VARCHAR), `rate_per_day` (NUMERIC), `status` (VARCHAR) | Fleet repository including IoT telematics coordinates, speed, and health score. |
| **`rentals`** | `rental_id` (SERIAL) | `customer_id`, `vehicle_id`, `driver_id` | `status` (VARCHAR), `total_amount` (NUMERIC), `start_date` (TIMESTAMPTZ) | Core business contract binding renter, vehicle, driver, and dates. |
| **`transactions`** | `transaction_id` (SERIAL)| `rental_id` ➔ `rentals` | `amount` (NUMERIC), `method` (VARCHAR), `proof_url` (TEXT), `status` (VARCHAR) | Financial audit records for payments, refunds, deposits, and extensions. |
| **`driver_bids`** | `bid_id` (SERIAL) | `rental_id`, `driver_id` | `bid_amount` (NUMERIC), `status` (VARCHAR) | Auction bidding records submitted by drivers for with-driver bookings. |
| **`add_ons`** | `add_on_id` (SERIAL) | None | `name` (VARCHAR), `daily_rate` (NUMERIC), `flat_rate` (NUMERIC) | Accessories catalog (GPS Navigator, Baby Seat, Luggage Rack). |
| **`rental_add_ons`** | `rental_addon_id` | `rental_id`, `add_on_id` | `quantity` (INT), `subtotal` (NUMERIC) | Line items of add-ons selected per rental reservation. |
| **`promo_codes`** | `promo_id` (SERIAL) | None | `code` (VARCHAR), `discount_type` (VARCHAR), `discount_value` (NUMERIC) | Discount coupons and promotional campaigns. |
| **`maintenance_logs`**| `log_id` (SERIAL) | `vehicle_id` ➔ `vehicles` | `service_type` (VARCHAR), `cost` (NUMERIC), `odometer_km` (INT) | Scheduled vehicle maintenance, oil changes, brake pads, and repairs. |
| **`expenses`** | `expense_id` (SERIAL) | `vehicle_id` ➔ `vehicles` | `category` (VARCHAR), `amount` (NUMERIC), `receipt_url` (TEXT) | Fleet operating costs (Fuel, Cleaning, Tolls, Emergency Repairs). |
| **`split_payments`** | `split_id` (SERIAL) | `rental_id` ➔ `rentals` | `payer_name` (VARCHAR), `share_amount` (NUMERIC), `status` (VARCHAR) | Peer-to-peer split billing records for group travel expenses. |
| **`issues`** | `issue_id` (SERIAL) | `rental_id`, `reporter_id` | `issue_type` (VARCHAR), `description` (TEXT), `image_url` (TEXT) | Road accidents, vehicle damage, flat tire, or engine issues. |
| **`blockchain_ledger`**| `block_id` (SERIAL) | `rental_id` ➔ `rentals` | `prev_hash` (TEXT), `current_hash` (TEXT), `action_type` (VARCHAR) | Immutable SHA-256 cryptographic chain preventing booking fraud. |

---

## 7. Data Flow Diagrams (DFD)

### 7.1 DFD Level 0: Context Diagram
The Level 0 Context Diagram establishes the highest-level view of information exchange between DriveAndGo and all external entities:

```mermaid
flowchart TD
    %% Entities
    Customer["fa:fa-user Customer / Renter"]
    Admin["fa:fa-user-tie Admin / Fleet Manager"]
    Driver["fa:fa-id-badge Chauffeur / Driver"]
    PaymentGateway["fa:fa-credit-card Payment Gateway\n(GCash / Maya / Bank)"]
    IoTTLS["fa:fa-satellite IoT Telematics &\nWeather Sensors"]

    %% Core System
    System(["0.0\nDriveAndGo Fleet &\nRental System"])

    %% Customer Flows
    Customer -->|"Registration & Login Credentials"| System
    Customer -->|"Reservation Requests & Destination"| System
    Customer -->|"Payment Proof & QR Codes"| System
    Customer -->|"Ratings & Incident Reports"| System
    System -->|"Available Fleet & Live Rates"| Customer
    System -->|"Digital Contract & Booking Confirmation"| Customer
    System -->|"Trip Status & In-App Messages"| Customer

    %% Admin Flows
    Admin -->|"Fleet Unit Configuration & Rates"| System
    Admin -->|"Booking Approval / Rejection Decisions"| System
    Admin -->|"Driver Compliance & Document Approval"| System
    Admin -->|"Geofence Boundary & Maintenance Orders"| System
    System -->|"Live Dashboard Telematics & Fleet Map"| Admin
    System -->|"Financial Ledger & Revenue Insights"| Admin
    System -->|"Critical Alerts (Engine, Flood, Overdue)"| Admin

    %% Driver Flows
    Driver -->|"License & Compliance Documents"| System
    Driver -->|"Trip Bids & Availability Status"| System
    Driver -->|"Mileage & Cash-on-Hand Reports"| System
    System -->|"Trip Assignment & Route Guidance"| Driver
    System -->|"Payout Statements & Passenger Details"| Driver

    %% External Systems
    IoTTLS -->|"GPS Lat/Lng, Speed, Fuel Level & Weather"| System
    System -->|"Remote Engine Lock / Telematics Command"| IoTTLS
    System -->|"Payment Verification Query"| PaymentGateway
    PaymentGateway -->|"Webhook Payment Settlement Confirmation"| System
```

---

### 7.2 DFD Level 1: System Decomposition Diagram
Decomposes the core system into seven major functional processes, showing the data repositories (Data Stores D1 to D6) utilized:

```mermaid
flowchart TD
    %% Entities
    Customer["Customer"]
    Admin["Fleet Admin"]
    Driver["Driver"]
    Sensors["IoT Sensors"]
    PaymentGW["Payment Gateway"]

    %% Processes
    P1(["1.0\nAuthentication &\nUser Management"])
    P2(["2.0\nFleet Ops &\nIoT Telematics"])
    P3(["3.0\nBooking & Rental\nLifecycle Engine"])
    P4(["4.0\nPayment & Split\nBilling Engine"])
    P5(["5.0\nChauffeur Bidding\n& Dispatch"])
    P6(["6.0\nMaintenance &\nExpense Control"])
    P7(["7.0\nAI Insights &\nBlockchain Audit"])

    %% Data Stores
    D1[("D1: Users & Driver Profiles")]
    D2[("D2: Fleet & IoT Coordinates")]
    D3[("D3: Rentals & Bookings")]
    D4[("D4: Transactions & Ledgers")]
    D5[("D5: Maintenance & Expenses")]
    D6[("D6: Blockchain Audit Vault")]

    %% Process 1.0 Flows
    Customer -->|Sign Up / Credentials| P1
    Admin -->|Admin Auth| P1
    Driver -->|Driver Onboarding| P1
    P1 <-->|Read / Write Accounts| D1

    %% Process 2.0 Flows
    Admin -->|Add / Edit Vehicles| P2
    Sensors -->|Telemetry Lat, Lng, Fuel| P2
    P2 <-->|Update Vehicle State| D2
    P2 -->|Live Telematics Feed| Admin

    %% Process 3.0 Flows
    Customer -->|Booking Parameters| P3
    D2 -->|Check Availability| P3
    P3 <-->|Create / Update Rental Contract| D3
    Admin -->|Approve / Reject| P3
    P3 -->|Booking Status & QR Code| Customer

    %% Process 5.0 Flows
    P3 -->|Trigger Open Trips| P5
    Driver -->|Submit Trip Bid| P5
    P5 <-->|Store / Accept Bid| D3
    P5 -->|Assigned Driver| P3

    %% Process 4.0 Flows
    Customer -->|Submit Payment Proof| P4
    P4 -->|Verify Transaction| PaymentGW
    PaymentGW -->|Confirmation| P4
    P4 <-->|Record Payment & Settlement| D4
    P4 -->|Mark Paid| P3

    %% Process 6.0 Flows
    Sensors -->|Odometer / Engine Warning| P6
    Admin -->|Log Repair / OCR Receipt| P6
    P6 <-->|Write Service & Fuel Costs| D5

    %% Process 7.0 Flows
    D3 -->|Trip Completed Events| P7
    D4 -->|Settlement Hashes| P7
    P7 <-->|Append SHA-256 Blocks| D6
    P7 -->|Revenue & Predictive Analytics| Admin
```

---

### 7.3 DFD Level 2: Booking & Payment Subsystem

A microscopic trace of how customer reservation requests flow through pricing, discount calculation, proof verification, and immutable ledger anchoring:

```mermaid
flowchart LR
    Customer["Customer / Renter"]
    Admin["Finance / Admin"]

    subgraph Subsystem [" Level 2 Decomposition: Booking & Settlement "]
        P31(["3.1 Validate Vehicle\nAvailability & Schedule"])
        P32(["3.2 Calculate Rental\nRates & Add-Ons"])
        P33(["3.3 Apply Promo Code\n& Compute Discounts"])
        P34(["3.4 Generate Contract\n& Issue Invoice"])
        P41(["4.1 Upload & Parse\nPayment Proof (OCR)"])
        P42(["4.2 Admin Settlement\nVerification"])
        P43(["4.3 Cryptographic Hash\n& Blockchain Seal"])
    end

    D2[("D2: Vehicles")]
    D3[("D3: Rentals")]
    D4[("D4: Transactions")]
    D6[("D6: Blockchain")]

    Customer -->|"Dates, Vehicle ID"| P31
    P31 <-->|"Check Availability"| D2
    P31 -->|"Confirmed Available"| P32

    Customer -->|"Select Add-Ons"| P32
    P32 -->|"Base Total"| P33

    Customer -->|"Enter Promo Code"| P33
    P33 -->|"Final Net Payable"| P34
    P34 -->|"Store New Booking"| D3
    P34 -->|"Invoice & QR Code"| Customer

    Customer -->|"Upload GCash/Maya Receipt"| P41
    P41 -->|"Store Proof"| D4
    P41 -->|"Pending Review Alert"| Admin

    Admin -->|"Verify Reference No."| P42
    P42 -->|"Update Status='Paid'"| D4
    P42 -->|"Activate Booking"| D3
    P42 -->|"Trigger Audit Anchor"| P43
    P43 -->|"Record Block Hash"| D6
    P42 -->|"Active Rental Voucher"| Customer
```

---

## 8. Summary & Capstone / Documentation Readiness

* **UML Use Case**: Fully outlines the user interactions and roles of all actors with system boundaries matching academic software engineering standards.
* **UML Class Diagram**: Maps out clean 1-to-Many and Object-Oriented inheritance between Users, Drivers, Fleet Vehicles, Contracts, and Bids.
* **UML Sequence Diagram**: Demonstrates synchronous & asynchronous events across WebSockets (SignalR), Controllers, and Cloud Storage.
* **ERD & Data Dictionary**: 100% accurate Crow's Foot ERD detailing all 23 database tables with data types, constraints, and relationships.
* **DFD Levels 0, 1, and 2**: Standard Gane & Sarson / Yourdon notation mapping the inputs, outputs, processes, and data stores of the entire platform.
