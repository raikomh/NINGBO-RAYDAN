using BusinessSearcher.Domain.BoundedContext.TenantManagement.Aggregates;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Entities;
using BusinessSearcher.Domain.BoundedContext.TenantManagement.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ChatMessage configuration lives here together with other TenantManagement entity configs

namespace BusinessSearcher.Infrastructure.Persistence.Configurations.TenantManagement
{
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("tenants", "public");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            builder.Property(t => t.BusinessName)
                .HasColumnName("business_name")
                .HasMaxLength(150)
                .IsRequired();

            builder.Property(t => t.PasswordHash)
                .HasColumnName("password_hash")
                .IsRequired();

            builder.Property(t => t.Plan)
                .HasColumnName("plan")
                .HasMaxLength(20)
                .HasDefaultValue("Standard")
                .IsRequired();

            builder.Property(t => t.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(t => t.Type)
                .HasColumnName("tenant_type")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(TenantType.Retail)
                .IsRequired();

            builder.Property(t => t.FcmToken)
                .HasColumnName("fcm_token")
                .HasMaxLength(500);

            builder.Property(t => t.PhoneNumber)
                .HasColumnName("phone_number")
                .HasMaxLength(30);

            // Cliente (bounded context Radar) cuyo código de referido usó esta MiPyme al
            // registrarse. Guid suelto, sin navegación/FK cruzada (Client vive en otro DbContext).
            builder.Property(t => t.ReferredByClientId)
                .HasColumnName("referred_by_client_id");

            builder.Property(t => t.LastPaymentDate)
                .HasColumnName("last_payment_date");

            builder.Property(t => t.NextPaymentDate)
                .HasColumnName("next_payment_date");

            // El nombre de columna va explícito, como en el resto del mapeo. No depender de
            // UseSnakeCaseNamingConvention es lo que evita el fallo que dejó las columnas
            // "LastSyncedAt" y "SyncApiKeyHash" en la base mientras la aplicación consultaba
            // last_synced_at y sync_api_key_hash: TenantManagementDbContextFactory (la fábrica
            // de tiempo de diseño, que EF usa con prioridad sobre el host) no aplicaba esa
            // convención, así que el modelo de diseño no coincidía con el de ejecución.
            builder.Property(t => t.LastSyncedAt)
                .HasColumnName("last_synced_at");

            builder.Property(t => t.SyncApiKeyHash)
                .HasColumnName("sync_api_key_hash")
                .HasMaxLength(128);

            // Cada push/pull resuelve el negocio buscando por este hash, así que sin índice cada
            // sincronización recorre la tabla entera de tenants. Único además porque una clave
            // identifica a un solo negocio: es lo que impide que una instalación toque otra tienda.
            // Los NULL no colisionan en PostgreSQL, así que los negocios sin clave no estorban.
            builder.HasIndex(t => t.SyncApiKeyHash).IsUnique();

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at");

            // Email como owned entity (Value Object)
            builder.OwnsOne(t => t.Email, email =>
            {
                email.Property(e => e.Value)
                    .HasColumnName("email")
                    .HasMaxLength(254)
                    .IsRequired();
                email.HasIndex(e => e.Value).IsUnique();
            });

            // Relación con TenantPayments
            builder.HasMany(t => t.Payments)
                .WithOne()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con PaymentClaims (reportes de pago del propio tenant)
            builder.HasMany(t => t.PaymentClaims)
                .WithOne()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con RefreshTokens
            builder.HasMany(t => t.RefreshTokens)
                .WithOne()
                .HasForeignKey(rt => rt.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con PasswordResetTokens
            builder.HasMany(t => t.PasswordResetTokens)
                .WithOne()
                .HasForeignKey(prt => prt.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con EmailVerificationTokens
            builder.HasMany(t => t.EmailVerificationTokens)
                .WithOne()
                .HasForeignKey(evt => evt.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(t => t.IsEmailVerified)
                .HasColumnName("is_email_verified")
                .IsRequired();

            builder.Property(t => t.IsApproved)
                .HasColumnName("is_approved")
                .HasDefaultValue(false)
                .IsRequired();

            // Ignorar eventos de dominio (no se persisten)
            builder.Ignore(t => t.DomainEvents);
        }
    }

    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_tokens", "public");
            builder.HasKey(rt => rt.Id);

            builder.Property(rt => rt.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(rt => rt.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(rt => rt.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(rt => rt.RevokedAt).HasColumnName("revoked_at");
            builder.Property(rt => rt.ReplacedByToken).HasColumnName("replaced_by_token").HasMaxLength(200);
            builder.Property(rt => rt.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(45);
            builder.Property(rt => rt.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(rt => rt.Token).IsUnique();
            builder.HasIndex(rt => rt.TenantId);

            builder.Ignore(rt => rt.DomainEvents);
        }
    }

    public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
    {
        public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
        {
            builder.ToTable("password_reset_tokens", "public");
            builder.HasKey(prt => prt.Id);

            builder.Property(prt => prt.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(prt => prt.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(prt => prt.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(prt => prt.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(prt => prt.IsUsed).HasColumnName("is_used").IsRequired();
            builder.Property(prt => prt.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(prt => prt.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(prt => prt.Token).IsUnique();
            builder.HasIndex(prt => prt.TenantId);

            builder.Ignore(prt => prt.DomainEvents);
        }
    }

    public class TenantPaymentConfiguration : IEntityTypeConfiguration<TenantPayment>
    {
        public void Configure(EntityTypeBuilder<TenantPayment> builder)
        {
            builder.ToTable("tenant_payments", "public");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
            builder.Property(p => p.Reference).HasColumnName("reference").HasMaxLength(200);
            builder.Property(p => p.PaymentDate).HasColumnName("payment_date").IsRequired();
            builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        }
    }

    public class PaymentClaimConfiguration : IEntityTypeConfiguration<PaymentClaim>
    {
        public void Configure(EntityTypeBuilder<PaymentClaim> builder)
        {
            builder.ToTable("payment_claims", "public");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(c => c.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30).IsRequired();
            builder.Property(c => c.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
            builder.Property(c => c.ProofReference).HasColumnName("proof_reference").HasMaxLength(300).IsRequired();
            builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(c => c.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(c => c.ReviewNote).HasColumnName("review_note").HasMaxLength(300);
            builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(c => c.TenantId);
            builder.HasIndex(c => c.Status);

            builder.Ignore(c => c.DomainEvents);
        }
    }

    public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
    {
        public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
        {
            builder.ToTable("email_verification_tokens", "public");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(t => t.Token).HasColumnName("token").HasMaxLength(200).IsRequired();
            builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(t => t.IsUsed).HasColumnName("is_used").IsRequired();
            builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(t => t.Token).IsUnique();
            builder.HasIndex(t => t.TenantId);

            builder.Ignore(t => t.DomainEvents);
        }
    }

    public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
    {
        public void Configure(EntityTypeBuilder<ChatMessage> builder)
        {
            builder.ToTable("chat_messages", "public");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(m => m.SenderName)
                .HasColumnName("sender_name").HasMaxLength(150).IsRequired();
            builder.Property(m => m.SenderType)
                .HasColumnName("sender_type").HasConversion<string>().HasMaxLength(10).IsRequired();
            builder.Property(m => m.Text)
                .HasColumnName("text").HasColumnType("text").IsRequired();
            builder.Property(m => m.IsRead).HasColumnName("is_read").IsRequired();
            builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(m => m.TenantId);
            builder.HasIndex(m => new { m.TenantId, m.CreatedAt });
            builder.HasIndex(m => new { m.TenantId, m.IsRead });
        }
    }

    public class MarketingVisitConfiguration : IEntityTypeConfiguration<MarketingVisit>
    {
        public void Configure(EntityTypeBuilder<MarketingVisit> builder)
        {
            builder.ToTable("marketing_visits", "public");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(v => v.Source).HasColumnName("source").HasMaxLength(100).IsRequired();
            builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(v => v.Source);
            builder.Ignore(v => v.DomainEvents);
        }
    }
}
