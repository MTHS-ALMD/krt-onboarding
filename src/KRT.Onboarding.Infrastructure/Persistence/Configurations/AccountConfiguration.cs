using KRT.Onboarding.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KRT.Onboarding.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.HolderName)
            .HasMaxLength(Account.HolderNameMaxLength)
            .IsRequired();

        builder.Property(a => a.Cpf)
            .HasConversion(cpf => cpf.Value, value => Cpf.Create(value))
            .HasMaxLength(Cpf.Length)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(a => a.Cpf).IsUnique();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);

        // Concorrência otimista: evita que duas atualizações simultâneas
        // se sobrescrevam silenciosamente (shadow property, o domínio não conhece).
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Ignore(a => a.DomainEvents);
    }
}
