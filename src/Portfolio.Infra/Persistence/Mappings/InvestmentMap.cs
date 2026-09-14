using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Core.Investments;
using Portfolio.Infra.Persistence.Converters;

namespace Portfolio.Infra.Persistence.Mappings;

public class InvestmentMap : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.ToTable("Investments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.InvestorId).IsRequired();
        builder.Property(a => a.AssetClassId).IsRequired();

        // decimal(18,2) e nao float: dinheiro nao pode sofrer erro de arredondamento binario.
        builder.Property(a => a.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // DateOnly vira a coluna "date" do SQL Server: 3 bytes e nenhuma questao de fuso.
        builder.Property(a => a.InvestedOn)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(a => a.Institution).HasMaxLength(Investment.MaxInstitutionLength);
        builder.Property(a => a.Notes).HasMaxLength(Investment.MaxNotesLength);

        builder.Property(a => a.CreatedAt)
            .HasConversion(UtcDateTime.Converter)
            .IsRequired();

        builder.Property(a => a.ChangedAt)
            .HasConversion(UtcDateTime.NullableConverter);

        // Toda consulta de aporte comeca filtrando pelo investidor; o indice composto
        // com a data cobre tanto a listagem quanto a ordenacao dela.
        builder.HasIndex(a => new { a.InvestorId, a.InvestedOn })
            .HasDatabaseName("IX_Investments_Investor_InvestedOn");

        // Restrict: apagar uma classe de ativo em uso levaria os aportes junto.
        builder.HasOne(a => a.AssetClass)
            .WithMany(c => c.Investments)
            .HasForeignKey(a => a.AssetClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
