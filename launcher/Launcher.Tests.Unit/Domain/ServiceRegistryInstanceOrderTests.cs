using Launcher.Domain.Model;
using Launcher.Protocol.Model;
using Xunit;

namespace Launcher.Tests.Unit.Domain;

/// <summary>
/// L'ordre dans lequel le registre énumère les instances est visible dans l'interface :
/// la ligne d'un composant y désigne une instance précise tant qu'elle vit.
/// <para>
/// L'énumération d'un <c>Dictionary</c> suit ses emplacements internes, pas l'ordre des
/// insertions. Tant qu'aucune suppression n'a eu lieu, les deux coïncident ; après un
/// retrait puis un nouvel ajout, le nouvel élément occupe le trou libéré et apparaît
/// <em>devant</em> les survivants. C'est exactement le cycle d'un run — une instance
/// temporaire ajoutée puis retirée autour de l'exécution — si bien que la ligne du
/// composant dans l'interface peut basculer sur l'instance de run qui vient de démarrer,
/// ce qui se lit comme un redémarrage alors que rien n'a été arrêté.
/// </para>
/// <para>
/// Ces scénarios impliquent tous au moins un retrait : sans lui, l'ordre du
/// <c>Dictionary</c> reproduirait l'ordre d'insertion et le test ne prouverait rien.
/// </para>
/// </summary>
public sealed class ServiceRegistryInstanceOrderTests
{
    [Fact]
    public void Une_instance_creee_apres_un_retrait_ne_supplante_pas_les_survivants()
    {
        var registry = new ServiceRegistry();
        foreach (var id in new[] { "syne-0001", "syne-0002", "syne-0003", "syne-0004" })
        {
            registry.Add(Instance(id, "syne"));
        }

        // Deux runs successifs : une instance retirée, une instance ajoutée.
        registry.Remove("syne-0001");
        registry.Remove("syne-0003");
        registry.Add(Instance("syne-0005", "syne"));

        // L'instance nouvelle est la plus récente : elle ne doit pas devenir celle
        // que l'interface affiche à la place du service que l'opérateur a démarré.
        Assert.Equal("syne-0002", registry.FindByComponent("syne")?.InstanceId);
        Assert.Equal(
            new[] { "syne-0002", "syne-0004", "syne-0005" },
            registry.All().Select(instance => instance.InstanceId));
    }

    [Fact]
    public void Retirer_une_instance_ne_decale_pas_l_ordre_des_survivants()
    {
        var registry = new ServiceRegistry();
        var ids = new[] { "syne-0001", "echos-0001", "syne-0002", "prism-0001", "echos-0002" };
        foreach (var id in ids)
        {
            registry.Add(Instance(id, id[..id.IndexOf('-', StringComparison.Ordinal)]));
        }

        registry.Remove("echos-0001");
        registry.Remove("syne-0001");

        Assert.Equal(
            new[] { "syne-0002", "prism-0001", "echos-0002" },
            registry.All().Select(instance => instance.InstanceId));
        Assert.Equal("syne-0002", registry.FindByComponent("syne")?.InstanceId);
        Assert.Equal("echos-0002", registry.FindByComponent("echos")?.InstanceId);

        // Puis une instance de run entre : elle doit se ranger après les survivants,
        // pas dans le trou laissé par les retraits.
        registry.Add(Instance("syne-0003", "syne"));

        Assert.Equal(
            new[] { "syne-0002", "prism-0001", "echos-0002", "syne-0003" },
            registry.All().Select(instance => instance.InstanceId));
        Assert.Equal("syne-0002", registry.FindByComponent("syne")?.InstanceId);
    }

    [Fact]
    public void Une_instance_retiree_puis_reenregistree_reprend_sa_place_a_la_fin()
    {
        var registry = new ServiceRegistry();
        registry.Add(Instance("syne-0001", "syne"));
        registry.Add(Instance("syne-0002", "syne"));
        registry.Add(Instance("syne-0003", "syne"));

        registry.Remove("syne-0001");
        registry.Add(Instance("syne-0001", "syne"));

        // Elle a été réinscrite : elle passe donc derrière les survivants, et la
        // sélection d'instance revient au plus ancien service vivant.
        Assert.Equal("syne-0002", registry.FindByComponent("syne")?.InstanceId);
        Assert.Equal(
            new[] { "syne-0002", "syne-0003", "syne-0001" },
            registry.All().Select(instance => instance.InstanceId));
    }

    [Fact]
    public void Remplacer_une_instance_de_meme_identifiant_ne_la_duplique_pas_dans_l_enumeration()
    {
        var registry = new ServiceRegistry();
        registry.Add(Instance("syne-0001", "syne"));
        registry.Add(Instance("syne-0002", "syne"));

        // Un même identifiant réinscrit remplace l'entrée, il ne s'y ajoute pas.
        registry.Add(Instance("syne-0002", "syne"));
        registry.Remove("syne-0002");
        registry.Add(Instance("syne-0002", "syne"));

        Assert.Equal(new[] { "syne-0001", "syne-0002" }, registry.All().Select(i => i.InstanceId));
        Assert.Equal("syne-0001", registry.FindByComponent("syne")?.InstanceId);
    }

    private static ComponentInstance Instance(string instanceId, string componentId) => new()
    {
        InstanceId = instanceId,
        ComponentId = componentId,
        Installation = new ComponentInstallation
        {
            ComponentId = componentId,
            Location = $"/components/{componentId}",
            BinaryPresent = true,
            Manifest = new ComponentManifest
            {
                Id = componentId,
                Name = componentId,
                Type = "engine",
                Version = "1.0.0",
            },
        },
    };
}