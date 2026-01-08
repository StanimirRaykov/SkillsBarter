# SkillsBarter

A platform for exchanging skills and services directly between users without monetary transactions. The system manages the complete lifecycle: offers, proposals, agreements, milestones, deliverables, reviews, and dispute resolution.

**Core capabilities:**
- Browse and post skill offerings
- Propose and negotiate exchanges
- Track agreements with milestones and deliverables
- Build reputation through reviews
- Resolve disputes with moderator support
- Real-time notifications for all activities

---

## How It Works

### 1️⃣ Create Your Account

Sign up with email or use Google OAuth for quick access. After registration, verify your email address to activate your account. Set up your profile with information about your skills, interests, and what you're looking to exchange.

### 2️⃣ Add Your Skills

Define what you can offer to the community and what you're looking for in return. Skills are categorized for easy discovery. You might offer web development and seek graphic design, or teach languages while looking for cooking lessons.

### 3️⃣ Browse & Make Offers

Post public **Offers** advertising your available skills and services. Browse what others are offering using filters by category, skill type, or search keywords. Each offer includes details about what's being provided and what the poster is seeking in exchange.

### 4️⃣ Propose & Negotiate

Found someone with skills you need? Send them a **Proposal** explaining what you'd like from them and what you can offer in return. Proposals allow both parties to discuss terms, timelines, and expectations before formalizing anything.

### 5️⃣ Create Agreements

Once both parties agree on terms, create a formal **Agreement** that locks in the details. Agreements specify:
- What each person will deliver
- Expected timelines
- Any milestones or checkpoints
- Completion criteria

### 6️⃣ Track with Milestones

For larger or long-term projects, break the work into smaller **Milestones** with individual deadlines. Each milestone can have one or more **Deliverables**—the actual work submitted. This keeps projects organized and allows progress tracking at each stage.

### 7️⃣ Leave Reviews

After an agreement is completed, both parties can leave **Reviews** rating the experience. Reviews cover aspects like:
- Quality of work delivered
- Timeliness and reliability
- Communication and professionalism

These reviews build each user's reputation score, helping others make informed decisions about future exchanges.

### 8️⃣ Resolve Disputes

If something goes wrong during an exchange, either party can open a **Dispute**. The dispute system allows:
- Uploading evidence and documentation
- Discussion threads between parties
- Moderator intervention when needed
- Fair resolution based on submitted information

### 9️⃣ Stay Notified

Receive real-time notifications via SignalR for important events:
- New proposals on your offers
- Milestone completions
- Messages from exchange partners
- Dispute updates
- Review submissions

---

## Key Features

✅ **Fair Exchanges** – Trade skills directly without money  
✅ **Safe & Verified** – Email verification and secure login with Google 
✅ **Build Your Reputation** – Reviews and ratings show who's reliable  
✅ **Track Everything** – Milestones and deliverables keep projects organized  
✅ **Resolve Problems** – Built-in dispute system with moderator support  
✅ **Real-Time Updates** – Instant notifications for all important events  
✅ **Search & Filter** – Easily find the exact skills you're looking for  
✅ **Multiple User Levels** – Regular users, moderators, and admins to keep things running smoothly  

---

## API Overview

The backend exposes RESTful endpoints organized by domain:

**Authentication & Users**
- Registration, login, password reset, email verification
- OAuth integration (Google)
- Profile management and user preferences
- Admin tools for user management

**Skills & Offers**
- CRUD operations for skills and categories
- Create, update, delete offers
- Search and filter with pagination
- Cooldown enforcement to prevent spam

**Agreements & Workflow**
- Proposal creation and management
- Agreement lifecycle tracking
- Milestone and deliverable management
- Status transitions and validations

**Community & Trust**
- Review system with aggregated ratings
- Dispute creation and resolution
- Penalty tracking for violations
- Reputation scoring

**Real-Time Communication**
- SignalR notification hub
- Instant updates for all activities
- Notification history and read status

---

## Technical Details

**Stack:**
- ASP.NET Core 8 Web API
- Entity Framework Core with PostgreSQL
- ASP.NET Identity for authentication
- JWT tokens (access + refresh)
- SignalR for real-time notifications

**Security:**
- Email verification required
- Rate limiting per client IP
- Role-based authorization (Admin, Moderator, Freemium)
- Security headers on all responses
- JWT bearer token protection on authenticated endpoints

**Architecture:**
- Controller → Service → Repository pattern
- DTOs for request/response shapes
- Domain models with EF Core relationships
- Dependency injection throughout
- Migrations for database schema versioning
