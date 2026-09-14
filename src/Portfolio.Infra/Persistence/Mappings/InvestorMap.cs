using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Core.Investors;
using Portfolio.Infra.Persistence.Converters;

namespace Portfolio.Infra.Persistence.Mappings;

public class InvestorMap : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.ToTable("Investors");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FullName)
            .HasMaxLength(Investor.MaxNameLength)
            .IsRequired();

        builder.Property(i => i.Email)
            .HasMaxLength(Investor.MaxEmailLength)
            .IsRequired();

        // O e-mail e a credencial de login: o indice unico e a garantia final de
        // que nao existem dois cadastros iguais, mesmo em requisicoes simultaneas.
        builder.HasIndex(i => i.Email)
            .IsUnique()
            .HasDatabaseName("UX_Investors_Email");

        builder.Property(i => i.PasswordHash)
            .HasMaxLength(100)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(i => i.RegisteredAt)
            .HasConversion(UtcDateTime.Converter)
            .IsRequired();

        builder.HasMany(i => i.Investments)
            .WithOne(a => a.Investor)
            .HasForeignKey(a => a.InvestorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Investments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
