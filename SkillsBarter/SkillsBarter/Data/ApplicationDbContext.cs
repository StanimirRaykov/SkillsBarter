using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SkillsBarter.Models;

namespace SkillsBarter.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // DbSets
    public DbSet<SkillCategory> SkillCategories { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<UserSkill> UserSkills { get; set; }
    public DbSet<OfferStatus> OfferStatuses { get; set; }
    public DbSet<Offer> Offers { get; set; }
    public DbSet<RequestThread> RequestThreads { get; set; }
    public DbSet<RequestMessage> RequestMessages { get; set; }
    public DbSet<Agreement> Agreements { get; set; }
    public DbSet<Milestone> Milestones { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Dispute> Disputes { get; set; }
    public DbSet<DisputeMessage> DisputeMessages { get; set; }
    public DbSet<DisputeEvidence> DisputeEvidence { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<ProposalHistory> ProposalHistories { get; set; }
    public DbSet<Deliverable> Deliverables { get; set; }
    public DbSet<Penalty> Penalties { get; set; }

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
            entity
                .Property(e => e.VerificationLevel)
                .HasColumnName("verification_level")
                .HasDefaultValue(0);
            entity
                .Property(e => e.ReputationScore)
                .HasColumnName("reputation_score")
                .HasColumnType("numeric")
                .HasDefaultValue(0);
            entity
                .Property(e => e.EmailVerificationToken)
                .HasColumnName("email_verification_token");
            entity
                .Property(e => e.EmailVerificationTokenExpiry)
                .HasColumnName("email_verification_token_expiry");
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity
                .Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
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
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CategoryCode).HasColumnName("category_code").IsRequired();

            entity
                .HasOne(e => e.Category)
                .WithMany(c => c.Skills)
                .HasForeignKey(e => e.CategoryCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserSkill>(entity =>
        {
            entity.ToTable("user_skills");
            entity.HasKey(e => new { e.UserId, e.SkillId });
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity
                .Property(e => e.AddedAt)
                .HasColumnName("added_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.User)
                .WithMany(u => u.UserSkills)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Skill)
                .WithMany(s => s.UserSkills)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OfferStatus>(entity =>
        {
            entity.ToTable("offer_status");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasColumnName("code").HasConversion<string>();
            entity.Property(e => e.Label).HasColumnName("label").IsRequired();

            entity.HasData(
                new OfferStatus { Code = OfferStatusCode.Active, Label = "Active" },
                new OfferStatus { Code = OfferStatusCode.Cancelled, Label = "Cancelled" },
                new OfferStatus
                {
                    Code = OfferStatusCode.UnderAgreement,
                    Label = "Under Agreement",
                },
                new OfferStatus { Code = OfferStatusCode.UnderReview, Label = "Under Review" },
                new OfferStatus { Code = OfferStatusCode.Completed, Label = "Completed" }
            );
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
            entity
                .Property(e => e.StatusCode)
                .HasColumnName("status_code")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(OfferStatusCode.Active);
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity
                .Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.User)
                .WithMany(u => u.Offers)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Skill)
                .WithMany(s => s.Offers)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Status)
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
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Offer)
                .WithMany(o => o.RequestThreads)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Initiator)
                .WithMany(u => u.InitiatedThreads)
                .HasForeignKey(e => e.InitiatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Recipient)
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
            entity
                .Property(e => e.SentAt)
                .HasColumnName("sent_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Sender)
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
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.Terms).HasColumnName("terms");
            entity
                .Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(AgreementStatus.Pending);
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");

            entity
                .HasOne(e => e.Offer)
                .WithMany(o => o.Agreements)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Requester)
                .WithMany(u => u.RequesterAgreements)
                .HasForeignKey(e => e.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Provider)
                .WithMany(u => u.ProviderAgreements)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.ToTable("milestones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.ResponsibleUserId).HasColumnName("responsible_user_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.DurationInDays).HasColumnName("duration_in_days");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.DueAt).HasColumnName("due_at");

            entity
                .HasOne(e => e.Agreement)
                .WithMany(a => a.Milestones)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity
                .HasOne(e => e.ResponsibleUser)
                .WithMany()
                .HasForeignKey(e => e.ResponsibleUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.MilestoneId).HasColumnName("milestone_id");
            entity.Property(e => e.TipFromUserId).HasColumnName("tip_from_user_id");
            entity.Property(e => e.TipToUserId).HasColumnName("tip_to_user_id");
            entity.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric");
            entity.Property(e => e.Currency).HasColumnName("currency").HasDefaultValue("USD");
            entity.Property(e => e.PaymentType).HasColumnName("payment_type").IsRequired();
            entity.Property(e => e.Provider).HasColumnName("provider");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity
                .Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Agreement)
                .WithMany(a => a.Payments)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Milestone)
                .WithMany(m => m.Payments)
                .HasForeignKey(e => e.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity
                .HasOne(e => e.TipFromUser)
                .WithMany(u => u.TipsSent)
                .HasForeignKey(e => e.TipFromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.TipToUser)
                .WithMany(u => u.TipsReceived)
                .HasForeignKey(e => e.TipToUserId)
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
            entity.Property(e => e.RespondentId).HasColumnName("respondent_id");
            entity
                .Property(e => e.ReasonCode)
                .HasColumnName("reason_code")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").IsRequired();
            entity
                .Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(DisputeStatus.Open);
            entity
                .Property(e => e.Resolution)
                .HasColumnName("resolution")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(DisputeResolution.None);
            entity.Property(e => e.Score).HasColumnName("score").HasDefaultValue(50);
            entity
                .Property(e => e.ComplainerDelivered)
                .HasColumnName("complainer_delivered")
                .HasDefaultValue(false);
            entity
                .Property(e => e.RespondentDelivered)
                .HasColumnName("respondent_delivered")
                .HasDefaultValue(false);
            entity
                .Property(e => e.ComplainerOnTime)
                .HasColumnName("complainer_on_time")
                .HasDefaultValue(false);
            entity
                .Property(e => e.RespondentOnTime)
                .HasColumnName("respondent_on_time")
                .HasDefaultValue(false);
            entity
                .Property(e => e.ComplainerApprovedBeforeDispute)
                .HasColumnName("complainer_approved_before_dispute")
                .HasDefaultValue(false);
            entity
                .Property(e => e.RespondentApprovedBeforeDispute)
                .HasColumnName("respondent_approved_before_dispute")
                .HasDefaultValue(false);
            entity.Property(e => e.ResolutionSummary).HasColumnName("resolution_summary");
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ResponseDeadline).HasColumnName("response_deadline");
            entity.Property(e => e.ResponseReceivedAt).HasColumnName("response_received_at");
            entity.Property(e => e.EscalatedAt).HasColumnName("escalated_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.ModeratorId).HasColumnName("moderator_id");
            entity.Property(e => e.ModeratorNotes).HasColumnName("moderator_notes");

            entity
                .HasOne(e => e.Agreement)
                .WithMany(a => a.Disputes)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Payment)
                .WithMany(p => p.Disputes)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity
                .HasOne(e => e.OpenedBy)
                .WithMany(u => u.OpenedDisputes)
                .HasForeignKey(e => e.OpenedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Respondent)
                .WithMany(u => u.RespondentDisputes)
                .HasForeignKey(e => e.RespondentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Moderator)
                .WithMany(u => u.ModeratedDisputes)
                .HasForeignKey(e => e.ModeratorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.AgreementId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.ResponseDeadline });
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.ToTable("dispute_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.Body).HasColumnName("body").IsRequired();
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Dispute)
                .WithMany(d => d.Messages)
                .HasForeignKey(e => e.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Sender)
                .WithMany(u => u.DisputeMessages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.ToTable("dispute_evidence");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity.Property(e => e.SubmittedById).HasColumnName("submitted_by_id");
            entity.Property(e => e.Link).HasColumnName("link").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").IsRequired();
            entity
                .Property(e => e.SubmittedAt)
                .HasColumnName("submitted_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Dispute)
                .WithMany(d => d.Evidence)
                .HasForeignKey(e => e.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.SubmittedBy)
                .WithMany(u => u.DisputeEvidence)
                .HasForeignKey(e => e.SubmittedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DisputeId);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.Rating).HasColumnName("rating").IsRequired();
            entity.Property(e => e.Body).HasColumnName("body");
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Recipient)
                .WithMany(u => u.ReviewsReceived)
                .HasForeignKey(e => e.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Reviewer)
                .WithMany(u => u.ReviewsGiven)
                .HasForeignKey(e => e.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Agreement)
                .WithMany(a => a.Reviews)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).HasColumnName("message").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired().HasMaxLength(64);
            entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ReadAt).HasColumnName("read_at");

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new
            {
                e.UserId,
                e.IsRead,
                e.CreatedAt,
            });
        });

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.ToTable("proposals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OfferId).HasColumnName("offer_id").IsRequired();
            entity.Property(e => e.ProposerId).HasColumnName("proposer_id").IsRequired();
            entity.Property(e => e.OfferOwnerId).HasColumnName("offer_owner_id").IsRequired();
            entity.Property(e => e.Terms).HasColumnName("terms").IsRequired();
            entity.Property(e => e.ProposerOffer).HasColumnName("proposer_offer").IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("deadline").IsRequired();
            entity
                .Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(ProposalStatus.PendingOfferOwnerReview);
            entity
                .Property(e => e.PendingResponseFromUserId)
                .HasColumnName("pending_response_from_user_id");
            entity
                .Property(e => e.ModificationCount)
                .HasColumnName("modification_count")
                .HasDefaultValue(0);
            entity.Property(e => e.LastModifiedByUserId).HasColumnName("last_modified_by_user_id");
            entity.Property(e => e.LastModifiedAt).HasColumnName("last_modified_at");
            entity.Property(e => e.DeclineReason).HasColumnName("decline_reason");
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id");
            entity.Property(e => e.ProposedMilestones).HasColumnName("proposed_milestones");

            entity
                .HasOne(e => e.Offer)
                .WithMany(o => o.Proposals)
                .HasForeignKey(e => e.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Proposer)
                .WithMany(u => u.SentProposals)
                .HasForeignKey(e => e.ProposerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.OfferOwner)
                .WithMany(u => u.ReceivedProposals)
                .HasForeignKey(e => e.OfferOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.PendingResponseFromUser)
                .WithMany()
                .HasForeignKey(e => e.PendingResponseFromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(e => e.LastModifiedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Agreement)
                .WithMany()
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OfferId);
            entity.HasIndex(e => e.ProposerId);
            entity.HasIndex(e => e.OfferOwnerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.OfferId, e.Status });
        });

        modelBuilder.Entity<ProposalHistory>(entity =>
        {
            entity.ToTable("proposal_histories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProposalId).HasColumnName("proposal_id").IsRequired();
            entity.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
            entity
                .Property(e => e.Action)
                .HasColumnName("action")
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.Terms).HasColumnName("terms").IsRequired();
            entity.Property(e => e.ProposerOffer).HasColumnName("proposer_offer").IsRequired();
            entity.Property(e => e.Deadline).HasColumnName("deadline").IsRequired();
            entity.Property(e => e.Message).HasColumnName("message");
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity
                .HasOne(e => e.Proposal)
                .WithMany(p => p.History)
                .HasForeignKey(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Actor)
                .WithMany(u => u.ProposalActions)
                .HasForeignKey(e => e.ActorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProposalId);
            entity.HasIndex(e => new { e.ProposalId, e.CreatedAt });
        });

        modelBuilder.Entity<Deliverable>(entity =>
        {
            entity.ToTable("deliverables");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id").IsRequired();
            entity.Property(e => e.SubmittedById).HasColumnName("submitted_by_id").IsRequired();
            entity.Property(e => e.Link).HasColumnName("link").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").IsRequired();
            entity
                .Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(DeliverableStatus.Submitted);
            entity.Property(e => e.RevisionReason).HasColumnName("revision_reason");
            entity
                .Property(e => e.SubmittedAt)
                .HasColumnName("submitted_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity
                .Property(e => e.RevisionCount)
                .HasColumnName("revision_count")
                .HasDefaultValue(0);

            entity
                .HasOne(e => e.Agreement)
                .WithMany(a => a.Deliverables)
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.SubmittedBy)
                .WithMany(u => u.Deliverables)
                .HasForeignKey(e => e.SubmittedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.AgreementId);
            entity.HasIndex(e => e.SubmittedById);
            entity.HasIndex(e => new { e.AgreementId, e.MilestoneId }).IsUnique();
        });

        modelBuilder.Entity<Penalty>(entity =>
        {
            entity.ToTable("penalties");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.AgreementId).HasColumnName("agreement_id").IsRequired();
            entity.Property(e => e.DisputeId).HasColumnName("dispute_id");
            entity
                .Property(e => e.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric")
                .IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasDefaultValue("EUR");
            entity
                .Property(e => e.Reason)
                .HasColumnName("reason")
                .HasConversion<string>()
                .IsRequired();
            entity
                .Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired()
                .HasDefaultValue(PenaltyStatus.Pending);
            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ChargedAt).HasColumnName("charged_at");

            entity
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(e => e.Agreement)
                .WithMany()
                .HasForeignKey(e => e.AgreementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Dispute)
                .WithMany()
                .HasForeignKey(e => e.DisputeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AgreementId);
            entity.HasIndex(e => e.Status);
        });

        // Apply UTC DateTime conversion for all DateTime properties
        // This ensures PostgreSQL timestamp with time zone columns work correctly
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
                    : v.Value.ToUniversalTime())
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }
}
