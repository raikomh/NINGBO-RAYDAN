using BusinessSearcher.Domain.BoundedContext.Radar.Aggregates;
using BusinessSearcher.Domain.BoundedContext.Radar.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.Radar
{
    public class ClientConfiguration : IEntityTypeConfiguration<Client>
    {
        public void Configure(EntityTypeBuilder<Client> builder)
        {
            builder.ToTable("clients", "public");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

            builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
            builder.Property(c => c.PasswordHash).HasColumnName("password_hash").IsRequired();

            builder.Property(c => c.Plan)
                .HasColumnName("plan").HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(c => c.IsEmailVerified).HasColumnName("is_email_verified").IsRequired();

            // A diferencia de Tenant.IsApproved (default false): los clientes ya
            // existentes antes de este flag deben poder seguir logueándose, así que
            // el default a nivel de esquema es true. Client.Create() siempre pasa
            // false explícitamente para los registros nuevos, sin importar este default.
            builder.Property(c => c.IsApproved).HasColumnName("is_approved").HasDefaultValue(true).IsRequired();

            builder.Property(c => c.FcmToken).HasColumnName("fcm_token").HasMaxLength(500);
            builder.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);

            // Ubicación del cliente-vendedor (GPS al publicar productos).
            builder.Property(c => c.Latitude).HasColumnName("latitude");
            builder.Property(c => c.Longitude).HasColumnName("longitude");
            builder.Property(c => c.City).HasColumnName("city").HasMaxLength(120);

            // Dirección de texto (autoservicio, cargada/editada desde el perfil).
            builder.Property(c => c.Street).HasColumnName("street").HasMaxLength(200);
            builder.Property(c => c.State).HasColumnName("state").HasMaxLength(120);
            builder.Property(c => c.Country).HasColumnName("country").HasMaxLength(120);

            builder.Property(c => c.ReportsSubmitted).HasColumnName("reports_submitted").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.ConfirmationsReceived).HasColumnName("confirmations_received").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.ReputationScore).HasColumnName("reputation_score").HasDefaultValue(50).IsRequired();

            // ── Referidos ──
            builder.Property(c => c.ReferralCode).HasColumnName("referral_code").HasMaxLength(20);
            builder.Property(c => c.ReferredByClientId).HasColumnName("referred_by_client_id");
            builder.Property(c => c.PremiumUntil).HasColumnName("premium_until");
            builder.Property(c => c.ValidReferralsTotal).HasColumnName("valid_referrals_total").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.ValidReferralsThisMonth).HasColumnName("valid_referrals_this_month").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.ReferralMonthAnchor).HasColumnName("referral_month_anchor").HasDefaultValue(0).IsRequired();
            builder.HasIndex(c => c.ReferralCode).IsUnique();

            // ── Referidos de MiPymes ──
            builder.Property(c => c.MipymeReferralsTotal).HasColumnName("mipyme_referrals_total").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.MipymeReferralsThisMonth).HasColumnName("mipyme_referrals_this_month").HasDefaultValue(0).IsRequired();
            builder.Property(c => c.MipymeReferralMonthAnchor).HasColumnName("mipyme_referral_month_anchor").HasDefaultValue(0).IsRequired();

            // ── Saldo de CUP por referidos ──
            builder.Property(c => c.ReferralCupBalance).HasColumnName("referral_cup_balance")
                .HasColumnType("decimal(18,2)").HasDefaultValue(0m).IsRequired();
            builder.Property(c => c.ReferralCupPaidTotal).HasColumnName("referral_cup_paid_total")
                .HasColumnType("decimal(18,2)").HasDefaultValue(0m).IsRequired();

            builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            builder.OwnsOne(c => c.Email, email =>
            {
                email.Property(e => e.Value)
                    .HasColumnName("email").HasMaxLength(254).IsRequired();
                email.HasIndex(e => e.Value).IsUnique();
            });

            builder.HasMany(c => c.RefreshTokens).WithOne()
                .HasForeignKey(rt => rt.ClientId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(c => c.PasswordResetTokens).WithOne()
                .HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(c => c.EmailVerificationTokens).WithOne()
                .HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(c => c.ReferralCupTransactions).WithOne()
                .HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(c => c.PremiumClaims).WithOne()
                .HasForeignKey(c => c.ClientId).OnDelete(DeleteBehavior.Cascade);

            builder.Ignore(c => c.DomainEvents);
        }
    }

    public class ClientPremiumClaimConfiguration : IEntityTypeConfiguration<ClientPremiumClaim>
    {
        public void Configure(EntityTypeBuilder<ClientPremiumClaim> builder)
        {
            builder.ToTable("client_premium_claims", "public");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(c => c.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30).IsRequired();
            builder.Property(c => c.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
            builder.Property(c => c.ProofReference).HasColumnName("proof_reference").HasMaxLength(300).IsRequired();
            builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(c => c.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(c => c.ReviewNote).HasColumnName("review_note").HasMaxLength(300);
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(c => c.ClientId);
            builder.HasIndex(c => c.Status);

            builder.Ignore(c => c.DomainEvents);
        }
    }

    public class ClientRefreshTokenConfiguration : IEntityTypeConfiguration<ClientRefreshToken>
    {
        public void Configure(EntityTypeBuilder<ClientRefreshToken> builder)
        {
            builder.ToTable("client_refresh_tokens", "public");
            builder.HasKey(rt => rt.Id);
            builder.Property(rt => rt.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(rt => rt.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(rt => rt.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(rt => rt.RevokedAt).HasColumnName("revoked_at");
            builder.Property(rt => rt.ReplacedByToken).HasColumnName("replaced_by_token").HasMaxLength(200);
            builder.Property(rt => rt.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(45);
            builder.Property(rt => rt.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");
            builder.HasIndex(rt => rt.Token).IsUnique();
            builder.HasIndex(rt => rt.ClientId);
            builder.Ignore(rt => rt.DomainEvents);
        }
    }

    public class ClientPasswordResetTokenConfiguration : IEntityTypeConfiguration<ClientPasswordResetToken>
    {
        public void Configure(EntityTypeBuilder<ClientPasswordResetToken> builder)
        {
            builder.ToTable("client_password_reset_tokens", "public");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(t => t.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(t => t.IsUsed).HasColumnName("is_used").IsRequired();
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            builder.HasIndex(t => t.Token).IsUnique();
            builder.HasIndex(t => t.ClientId);
            builder.Ignore(t => t.DomainEvents);
        }
    }

    public class ClientEmailVerificationTokenConfiguration : IEntityTypeConfiguration<ClientEmailVerificationToken>
    {
        public void Configure(EntityTypeBuilder<ClientEmailVerificationToken> builder)
        {
            builder.ToTable("client_email_verification_tokens", "public");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(t => t.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(t => t.IsUsed).HasColumnName("is_used").IsRequired();
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            builder.HasIndex(t => t.Token).IsUnique();
            builder.HasIndex(t => t.ClientId);
            builder.Ignore(t => t.DomainEvents);
        }
    }

    public class ClientProductConfiguration : IEntityTypeConfiguration<ClientProduct>
    {
        public void Configure(EntityTypeBuilder<ClientProduct> builder)
        {
            builder.ToTable("client_products", "public");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

            builder.Property(p => p.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            builder.Property(p => p.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200).IsRequired();
            builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(1000);
            builder.Property(p => p.Price).HasColumnName("price").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            builder.Property(p => p.IsAvailable).HasColumnName("is_available").IsRequired();
            builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasColumnType("text");

            builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(p => p.ClientId);
            builder.HasIndex(p => p.NormalizedName);
            builder.HasIndex(p => p.IsAvailable);

            builder.Ignore(p => p.DomainEvents);
        }
    }

    public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
    {
        public void Configure(EntityTypeBuilder<Referral> builder)
        {
            builder.ToTable("referrals", "public");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(r => r.InviterClientId).HasColumnName("inviter_client_id").IsRequired();
            builder.Property(r => r.InvitedClientId).HasColumnName("invited_client_id").IsRequired();
            builder.Property(r => r.Status)
                .HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(r => r.ValidatedAt).HasColumnName("validated_at");
            builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(r => r.InviterClientId);
            builder.HasIndex(r => r.InvitedClientId).IsUnique(); // 1 referido por cuenta invitada (antifraude)

            builder.Ignore(r => r.DomainEvents);
        }
    }

    public class ReferralCupTransactionConfiguration : IEntityTypeConfiguration<ReferralCupTransaction>
    {
        public void Configure(EntityTypeBuilder<ReferralCupTransaction> builder)
        {
            builder.ToTable("referral_cup_transactions", "public");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(t => t.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(t => t.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(t => t.Type)
                .HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(t => t.Note).HasColumnName("note").HasMaxLength(500);
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(t => t.ClientId);

            builder.Ignore(t => t.DomainEvents);
        }
    }

    public class ReferralMonthlySettlementConfiguration : IEntityTypeConfiguration<ReferralMonthlySettlement>
    {
        public void Configure(EntityTypeBuilder<ReferralMonthlySettlement> builder)
        {
            builder.ToTable("referral_monthly_settlements", "public");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(s => s.Year).HasColumnName("year").IsRequired();
            builder.Property(s => s.Month).HasColumnName("month").IsRequired();
            builder.Property(s => s.WinnerClientId).HasColumnName("winner_client_id");
            builder.Property(s => s.PrizeAmount).HasColumnName("prize_amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(s => s.TotalValidReferrals).HasColumnName("total_valid_referrals").IsRequired();
            builder.Property(s => s.SettledAt).HasColumnName("settled_at").IsRequired();
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(s => new { s.Year, s.Month }).IsUnique();

            builder.Ignore(s => s.DomainEvents);
        }
    }

    public class MipymeReferralConfiguration : IEntityTypeConfiguration<MipymeReferral>
    {
        public void Configure(EntityTypeBuilder<MipymeReferral> builder)
        {
            builder.ToTable("mipyme_referrals", "public");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(r => r.ReferrerClientId).HasColumnName("referrer_client_id").IsRequired();
            builder.Property(r => r.ReferredTenantId).HasColumnName("referred_tenant_id").IsRequired();
            builder.Property(r => r.Status)
                .HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(r => r.ValidatedAt).HasColumnName("validated_at");
            builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(r => r.ReferrerClientId);
            builder.HasIndex(r => r.ReferredTenantId).IsUnique(); // 1 referido por MiPyme (antifraude)

            builder.Ignore(r => r.DomainEvents);
        }
    }

    public class MipymeReferralMonthlySettlementConfiguration : IEntityTypeConfiguration<MipymeReferralMonthlySettlement>
    {
        public void Configure(EntityTypeBuilder<MipymeReferralMonthlySettlement> builder)
        {
            builder.ToTable("mipyme_referral_monthly_settlements", "public");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(s => s.Year).HasColumnName("year").IsRequired();
            builder.Property(s => s.Month).HasColumnName("month").IsRequired();
            builder.Property(s => s.WinnerClientId).HasColumnName("winner_client_id");
            builder.Property(s => s.PrizeAmount).HasColumnName("prize_amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(s => s.TotalValidMipymeReferrals).HasColumnName("total_valid_mipyme_referrals").IsRequired();
            builder.Property(s => s.SettledAt).HasColumnName("settled_at").IsRequired();
            builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(s => new { s.Year, s.Month }).IsUnique();

            builder.Ignore(s => s.DomainEvents);
        }
    }

    public class AvailabilityReportConfiguration : IEntityTypeConfiguration<AvailabilityReport>
    {
        public void Configure(EntityTypeBuilder<AvailabilityReport> builder)
        {
            builder.ToTable("availability_reports", "public");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

            builder.Property(r => r.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            builder.Property(r => r.NormalizedProductName).HasColumnName("normalized_product_name").HasMaxLength(200).IsRequired();
            builder.Property(r => r.StoreId).HasColumnName("store_id");
            builder.Property(r => r.PlaceName).HasColumnName("place_name").HasMaxLength(200).IsRequired();
            builder.Property(r => r.City).HasColumnName("city").HasMaxLength(120).IsRequired();
            builder.Property(r => r.Municipality).HasColumnName("municipality").HasMaxLength(120);

            builder.Property(r => r.Status)
                .HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(r => r.QueueStatus)
                .HasColumnName("queue_status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(r => r.Source)
                .HasColumnName("source").HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.Property(r => r.Price).HasColumnName("price").HasColumnType("decimal(18,2)");
            builder.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(3);
            builder.Property(r => r.PhotoUrl).HasColumnName("photo_url").HasColumnType("text");

            builder.Property(r => r.ReporterClientId).HasColumnName("reporter_client_id").IsRequired();
            builder.Property(r => r.ReporterReputationSnapshot).HasColumnName("reporter_reputation_snapshot").IsRequired();
            builder.Property(r => r.ReportedAt).HasColumnName("reported_at").IsRequired();
            builder.Property(r => r.LastActivityAt).HasColumnName("last_activity_at").IsRequired();

            builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

            builder.OwnsOne(r => r.Location, loc =>
            {
                loc.Property(l => l.Latitude).HasColumnName("latitude").IsRequired();
                loc.Property(l => l.Longitude).HasColumnName("longitude").IsRequired();
            });
            builder.Navigation(r => r.Location).IsRequired();

            builder.HasMany(r => r.Confirmations).WithOne()
                .HasForeignKey(c => c.ReportId).OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => r.NormalizedProductName);
            builder.HasIndex(r => r.City);
            builder.HasIndex(r => r.ReportedAt);

            builder.Ignore(r => r.DomainEvents);
        }
    }

    public class ReportConfirmationConfiguration : IEntityTypeConfiguration<ReportConfirmation>
    {
        public void Configure(EntityTypeBuilder<ReportConfirmation> builder)
        {
            builder.ToTable("report_confirmations", "public");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(c => c.ReportId).HasColumnName("report_id").IsRequired();
            builder.Property(c => c.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(c => c.Agrees).HasColumnName("agrees").IsRequired();
            builder.Property(c => c.ReportedStatus)
                .HasColumnName("reported_status").HasConversion<string>().HasMaxLength(20);
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(c => c.ReportId);
            builder.HasIndex(c => new { c.ReportId, c.ClientId }).IsUnique();

            builder.Ignore(c => c.DomainEvents);
        }
    }

    public class AvailabilityAlertConfiguration : IEntityTypeConfiguration<AvailabilityAlert>
    {
        public void Configure(EntityTypeBuilder<AvailabilityAlert> builder)
        {
            builder.ToTable("availability_alerts", "public");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

            builder.Property(a => a.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(a => a.ProductNameFilter).HasColumnName("product_name_filter").HasMaxLength(200).IsRequired();
            builder.Property(a => a.NormalizedProductName).HasColumnName("normalized_product_name").HasMaxLength(200).IsRequired();
            builder.Property(a => a.RadiusKm).HasColumnName("radius_km");
            builder.Property(a => a.MaxPrice).HasColumnName("max_price").HasColumnType("decimal(18,2)");
            builder.Property(a => a.City).HasColumnName("city").HasMaxLength(120);
            builder.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
            builder.Property(a => a.LastTriggeredAt).HasColumnName("last_triggered_at");
            builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            builder.OwnsOne(a => a.Center, center =>
            {
                center.Property(l => l.Latitude).HasColumnName("center_latitude");
                center.Property(l => l.Longitude).HasColumnName("center_longitude");
            });

            builder.HasIndex(a => a.ClientId);
            builder.HasIndex(a => a.IsActive);

            builder.Ignore(a => a.DomainEvents);
        }
    }

    public class CommunityQuestionConfiguration : IEntityTypeConfiguration<CommunityQuestion>
    {
        public void Configure(EntityTypeBuilder<CommunityQuestion> builder)
        {
            builder.ToTable("community_questions");
            builder.HasKey(q => q.Id);
            builder.Property(q => q.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(q => q.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(q => q.AskerName).HasColumnName("asker_name").HasMaxLength(120).IsRequired();
            builder.Property(q => q.Text).HasColumnName("text").HasMaxLength(500).IsRequired();
            builder.Property(q => q.City).HasColumnName("city").HasMaxLength(120);
            builder.Property(q => q.ProductName).HasColumnName("product_name").HasMaxLength(200);
            builder.Property(q => q.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");

            builder.HasMany(q => q.Answers).WithOne().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(q => q.Answers).HasField("_answers").UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(q => q.City);
            builder.HasIndex(q => q.CreatedAt);
            builder.Ignore(q => q.DomainEvents);
        }
    }

    public class CommunityAnswerConfiguration : IEntityTypeConfiguration<CommunityAnswer>
    {
        public void Configure(EntityTypeBuilder<CommunityAnswer> builder)
        {
            builder.ToTable("community_answers");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(a => a.QuestionId).HasColumnName("question_id").IsRequired();
            builder.Property(a => a.ClientId).HasColumnName("client_id").IsRequired();
            builder.Property(a => a.AnswererName).HasColumnName("answerer_name").HasMaxLength(120).IsRequired();
            builder.Property(a => a.Text).HasColumnName("text").HasMaxLength(500).IsRequired();
            builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(a => a.QuestionId);
            builder.Ignore(a => a.DomainEvents);
        }
    }
}
