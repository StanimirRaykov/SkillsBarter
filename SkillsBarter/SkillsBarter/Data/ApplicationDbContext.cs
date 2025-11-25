using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkillsBarter.Models;

namespace SkillsBarter.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<SkillCategory> SkillCategories { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<OfferStatus> OfferStatuses { get; set; }
    public DbSet<Offer> Offers { get; set; }
    public DbSet<RequestThread> RequestThreads { get; set; }
    public DbSet<RequestMessage> RequestMessages { get; set; }
    public DbSet<Agreement> Agreements { get; set; }
    public DbSet<Milestone> Milestones { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Dispute> Disputes { get; set; }
    public DbSet<DisputeMessage> DisputeMessages { get; set; }
    public DbSet<Review> Reviews { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.UserName).HasColumnName("username");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.VerificationLevel).HasColumnName("verification_level").HasDefaultValue(0);
            entity.Property(e => e.ReputationScore).HasColumnName("reputation_score").HasColumnType("numeric").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        modelBuilder.Entity<SkillCategory>(entity =>
        {
            entity.ToTable("skill_categories");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Label).HasColumnName("label").IsRequired();
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.ToTable("skills");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CategoryCode).HasColumnName("category_code").IsRequired();

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Skills)
                .HasForeignKey(e => e.CategoryCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OfferStatus>(entity =>
        {
            entity.ToTable("offer_status");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasConversion<string>();
            entity.Property(e => e.Label).HasColumnName("label").IsRequired();
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.ToTable("offers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.StatusCode)
                .HasColumnName("status_code")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(OfferStatusCode.Active);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Offers)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Skill)
                .WithMany(s => s.Offers)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Status)
                .WithMany(s => s.Offers)
                .HasForeignKey(e => e.StatusCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequestThread>(entity =>
        {
            entity.ToTable("request_threads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.InitiatorId).HasColumnName("initiator_id");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Offer)
                .WithMany(o => o.RequestThreads)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Initiator)
                .WithMany(u => u.InitiatedThreads)
                .HasForeignKey(e => e.InitiatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Recipient)
                .WithMany(u => u.ReceivedThreads)
                .HasForeignKey(e => e.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequestMessage>(entity =>
        {
            entity.ToTable("request_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ThreadId).HasColumnName("thread_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.Body).HasColumnName("body").IsRequired();
            entity.Property(e => e.SentAt).HasColumnName("sent_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Agreement>(entity =>
        {
            entity.ToTable("agreements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.BuyerId).HasColumnName("buyer_id");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity.HasOne(e => e.Offer)
                .WithMany(o => o.Agreements)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Buyer)
                .WithMany(u => u.BuyerAgreements)
                .HasForeignKey(e => e.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Seller)
                .WithMany(u => u.SellerAgreements)
                .HasForeignKey(e => e.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.ToTable("milestones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.DueAt).HasColumnName("due_at");

            entity.HasOne(e => e.Agreement)
                .WithMany(a => a.Milestones)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.MilestoneId).HasColumnName("milestone_id");
            entity.Property(e => e.PayerId).HasColumnName("payer_id");
            entity.Property(e => e.PayeeId).HasColumnName("payee_id");
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric");
            entity.Property(e => e.Currency).HasColumnName("currency").HasDefaultValue("USD");
            entity.Property(e => e.PaymentType).HasColumnName("payment_type").IsRequired();
            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Agreement)
                .WithMany(a => a.Payments)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Milestone)
                .WithMany(m => m.Payments)
                .HasForeignKey(e => e.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Payer)
                .WithMany(u => u.PayerPayments)
                .HasForeignKey(e => e.PayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Payee)
                .WithMany(u => u.PayeePayments)
                .HasForeignKey(e => e.PayeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("disputes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.PaymentId).HasColumnName("payment_id");
            entity.Property(e => e.OpenedById).HasColumnName("opened_by_id");
            entity.Property(e => e.ReasonCode).HasColumnName("reason_code").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.ResolutionSummary).HasColumnName("resolution_summary");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");

            entity.HasOne(e => e.Agreement)
                .WithMany(a => a.Disputes)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Payment)
                .WithMany(p => p.Disputes)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.OpenedBy)
                .WithMany(u => u.OpenedDisputes)
                .HasForeignKey(e => e.OpenedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.ToTable("dispute_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.Body).HasColumnName("body").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Dispute)
                .WithMany(d => d.Messages)
                .HasForeignKey(e => e.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany(u => u.DisputeMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.Rating).HasColumnName("rating").IsRequired();
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(e => e.Recipient)
                .WithMany(u => u.ReviewsReceived)
                .HasForeignKey(e => e.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Reviewer)
                .WithMany(u => u.ReviewsGiven)
                .HasForeignKey(e => e.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Offer)
                .WithMany(o => o.Reviews)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
