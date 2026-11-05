using KRT.Onboarding.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KRT.Onboarding.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(OutboxMessage.TypeMaxLength).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);

        // Índice filtrado: o worker só lê o que ainda não foi publicado.
        builder.HasIndex(m => m.OccurredAt)
            .HasFilter("[ProcessedAt] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
