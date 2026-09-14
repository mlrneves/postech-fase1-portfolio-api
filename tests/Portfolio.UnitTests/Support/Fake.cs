using System.Reflection;
using Portfolio.Core.AssetClasses;

namespace Portfolio.UnitTests.Support;

/// <summary>
/// Fabricas de entidades para os testes. As chaves geradas pelo banco (o Id
/// identity da classe de ativo) sao preenchidas por reflexao, ja que a entidade
/// nao expoe setter publico e nesses testes nao existe um DbContext para atribui-las.
/// </summary>
internal static class Fake
{
    public static AssetClass AssetClass(int id, string name)
    {
        var assetClass = new AssetClass(name);

        typeof(AssetClass)
            .GetProperty(nameof(Core.AssetClasses.AssetClass.Id), BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(assetClass, [id]);

        return assetClass;
    }

    public static AssetClass Shares() => AssetClass(1, "Ações");

    public static AssetClass FixedIncome() => AssetClass(2, "Renda Fixa");

    public static AssetClass Crypto() => AssetClass(5, "Criptomoedas");

    public static DateOnly Yesterday() => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
}
