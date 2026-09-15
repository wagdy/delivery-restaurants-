namespace RestaurantDelivery.Core.Common;

public static class OptionalText
{
    // Collapses the three ways a client can say "nothing here" - absent, empty string,
    // whitespace - into the single one the database stores: null.
    //
    // This matters for NameAr specifically. The Arabic name is optional, so an admin who
    // clears the field, or who types a space and deletes it, must end up in the same
    // state as one who never touched it. Without this, "" and " " would both be truthy
    // on the client and the storefront would render an empty or blank dish name in
    // Arabic instead of falling back to the English one. The client guards against this
    // too (see LocalNamePipe), but the fallback should not be the only thing standing
    // between a stray keystroke and a nameless menu item.
    public static string? NullIfBlank(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
