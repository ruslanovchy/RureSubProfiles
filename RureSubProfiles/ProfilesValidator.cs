using System.Text.RegularExpressions;

namespace RureSubProfiles;

public partial class ProfilesValidator
{
    public const string NAME_REGEX = "^[a-zA-Z0-9_]{3,30}$";
    public const string BIO_REGEX = "^(?!.*(<script|javascript:|on\\w+=|<iframe|<img|<a\\s))[^\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F\\x7F]{0,1000}$";

    [GeneratedRegex(NAME_REGEX)]
    public partial Regex GetNameRegex();
    [GeneratedRegex(BIO_REGEX)]
    public partial Regex GetBioRegex();

    public bool IsValidName(string name) => GetNameRegex().IsMatch(name);
    public bool IsValidBio(string bio) => GetBioRegex().IsMatch(bio);
}
