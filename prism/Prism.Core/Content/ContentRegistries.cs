namespace Prism.Core.Content;

public sealed class ContentRegistries
{
    public ContentRegistries(
        IEnumerable<string> materialKeys,
        IEnumerable<string> itemKeys,
        IEnumerable<string> tagKeys,
        IEnumerable<string> recipeKeys)
    {
        Materials = new StableIdRegistry(materialKeys);
        Items = new StableIdRegistry(itemKeys);
        Tags = new StableIdRegistry(tagKeys);
        Recipes = new StableIdRegistry(recipeKeys);
    }

    public StableIdRegistry Materials { get; }
    public StableIdRegistry Items { get; }
    public StableIdRegistry Tags { get; }
    public StableIdRegistry Recipes { get; }
}
