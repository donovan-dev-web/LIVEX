using Prism.Content;

namespace Prism.Bots;

/// <summary>
/// Recipe knowledge a planner may use. PRISM-077 bots are server-side test clients: recipe
/// knowledge is content knowledge, exactly what SYNE will receive as "savoir des recettes".
/// </summary>
public sealed class BotRecipeBook
{
    private readonly Dictionary<ushort, RecipeDefinition> _byOutput = [];
    private readonly Dictionary<string, RecipeDefinition> _byKey = new(StringComparer.Ordinal);

    private BotRecipeBook(BotContentMap content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        foreach (var recipe in content.Pack.Recipes)
        {
            _byKey[recipe.Id] = recipe;
            foreach (var output in recipe.Outputs)
            {
                if (content.TryItemId(output.Item, out var outputItemId))
                {
                    _byOutput.TryAdd(outputItemId, recipe);
                }
            }
        }
    }

    public BotContentMap Content { get; }

    public static BotRecipeBook FromContent(BotContentMap content) => new(content);

    public bool TryFindRecipeFor(ushort itemTypeId, out RecipeDefinition recipe) =>
        _byOutput.TryGetValue(itemTypeId, out recipe!);

    public bool TryFindRecipe(string recipeId, out RecipeDefinition recipe) =>
        _byKey.TryGetValue(recipeId, out recipe!);
}
