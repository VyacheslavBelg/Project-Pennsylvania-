namespace Domovoy.Core.Services;

/// <summary>Регистр в подставляемых фразах.</summary>
public static class TextCase
{
    /// <summary>
    /// Первая буква строчная — для подстановки названия внутрь фразы. Аббревиатуры
    /// не трогаются: ToLowerInvariant целиком превращал «УК» и «АДС» в «ук» и «адс».
    /// </summary>
    public static string LowerFirst(string text) =>
        text is { Length: > 1 } && char.IsLower(text[1])
            ? char.ToLowerInvariant(text[0]) + text[1..]
            : text;
}
