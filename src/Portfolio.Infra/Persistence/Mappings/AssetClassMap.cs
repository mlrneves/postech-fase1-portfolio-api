using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Core.AssetClasses;
using Portfolio.Infra.Persistence.Converters;

namespace Portfolio.Infra.Persistence.Mappings;

public class AssetClassMap : IEntityTypeConfiguration<AssetClass>
{
    /// <summary>
    /// Data fixa para a carga inicial. Usar DateTime.UtcNow aqui deixaria a
    /// migration nao deterministica e ela apareceria como alterada a cada build.
    /// </summary>
    private static readonly DateTime SeedMoment = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AssetClass> builder)
    {
        builder.ToTable("AssetClasses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        builder.Property(a => a.Name)
            .HasMaxLength(AssetClass.MaxNameLength)
            .IsRequired();

        builder.Property(a => a.Slug)
            .HasMaxLength(AssetClass.MaxNameLength)
            .IsRequired()
            .IsUnicode(false);

        // A unicidade e do slug, nao do nome: "Ações" e "acoes" sao a mesma coisa.
        builder.HasIndex(a => a.Slug)
            .IsUnique()
            .HasDatabaseName("UX_AssetClasses_Slug");

        builder.Property(a => a.CreatedAt)
            .HasConversion(UtcDateTime.Converter)
            .IsRequired();

        builder.Property(a => a.ChangedAt)
            .HasConversion(UtcDateTime.NullableConverter);

        builder.Navigation(a => a.Investments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            Row(1, "Ações", "acoes"),
            Row(2, "Renda Fixa", "renda-fixa"),
            Row(3, "Fundos Imobiliários", "fundos-imobiliarios"),
            Row(4, "Tesouro Direto", "tesouro-direto"),
            Row(5, "Criptomoedas", "criptomoedas"));
    }

    private static object Row(int id, string name, string slug) => new
    {
        Id = id,
        Name = name,
        Slug = slug,
        CreatedAt = SeedMoment,
        ChangedAt = (DateTime?)null
    };
}
