using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Backend.Service.Api;

public static class ArgValidation
{
    // User Signature Stuff
    /*
     * Socials Regex Pattern (google is your friend)
     * Did not touch them but tested them with some input to validate them
     */
    private const string DiscordPattern =
        @"(https?:\/\/)?(www\.)?(discord\.(gg|io|me|li)|discordapp\.com\/invite)\/.+[a-z]";

    private const string FacebookPattern =
        @"(?:https?:\/\/)?(?:www\.)?facebook\.com\/.(?:(?:\w)*#!\/)?(?:pages\/)?(?:[\w\-]*\/)*([\w\-\.]*)";

    private const string InstagramPattern =
        @"(?:(?:http|https):\/\/)?(?:www.)?(?:instagram.com|instagr.am|instagr.com)\/(\w+)";

    private const string SpotifyPattern =
        @"^(https:\/\/open.spotify.com\/user\/spotify\/playlist\/|spotify:user:spotify:playlist:)([a-zA-Z0-9]+)(.*)$";

    private const string TelegramPattern = @"(https?:\/\/)?(www[.])?(telegram|t)\.me\/([\.a-zA-Z0-9_-]*)\/?$";

    private const string YoutubePattern = @"(https?:\/\/)?(www\.)?youtube\.com\/(channel|user)\/[\w-]+";

    private const string TwitterPattern =
        @"(?:http:\/\/)?(?:www\.)?twitter\.com\/(?:(?:\w)*#!\/)?(?:pages\/)?(?:[\w\-]*\/)*([\w\-]*)";

    /*
     * Additional Regex patterned designed for our purposes.
     * Tested them with some input to validate them.
     * Fixing and extending them is quite likely.
     */
    /*
     * Username Pattern:
     * Only allow lowercase (looks better in the URI)
     * Name start with at least 3 letters
     * And may end with any number
     * username ends with no new line
     * The max length is checked elsewhere
     */
    private const string UsernamePattern = @"^[a-z]{3,}[0-9]*\z";

    /*
     * Title Pattern:
     * Allow all cases.
     * Start with at least 1 letter or number.
     * Allow 1 space between letters or numbers
     * Title ends without a new line.
     * The max length is checked elsewhere
     */
    private const string TitlePattern = @"^(([a-zA-Z0-9]+(\s[a-zA-Z0-9]+))+\z)";


    public static bool CheckLimit(int value)
    {
        //return value <= 100;
        return value > 0;
    }


    public static bool CheckLimit(int value, bool filterSet)
    {
        if ( filterSet ) return value >= -1;
        return value > 0;
    }


    public static bool CheckOffset(int value)
    {
        return value >= 0;
    }


    public static bool CheckFieldName(string value)
    {
        // We allow for names:
        // Latin symbols in both cases
        // Digits
        // _ (Underscore)
        return Regex.IsMatch(value, @"^[a-zA-Z0-9_]+$");
    }


    public static bool CheckExtendedFilterName(string value)
    {
        // We allow for names:
        // Latin symbols in both cases
        // Digits
        // _ (Underscore)
        // Space
        return Regex.IsMatch(value, @"^[a-zA-Z0-9_ ]+$");
    }


    public static bool CheckOrderDirection(string value)
    {
        return value.ToUpper() == "ASC" || value.ToUpper() == "DESC";
    }


    public static bool CheckName(string value)
    {
        // We allow all symbols for names
        return true;
    }


    public static bool CheckChain(string value)
    {
        return value.ToLower() == "main";
    }


    public static bool CheckSymbol(string value, bool allowMultipleSymbols = false)
    {
        // We allow for names:
        // Latin symbols in both cases
        // Digits
        // , (Comma) - if allowMultipleSymbols == true
        return Regex.IsMatch(value, allowMultipleSymbols ? @"^[a-zA-Z0-9,]+$" : @"^[a-zA-Z0-9]+$");
    }


    public static bool CheckAddress(string value)
    {
        // We allow for names:
        // Latin symbols in both cases
        // Digits
        // , (Comma)
        // _ (Underscore)
        // :
        return Regex.IsMatch(value, @"^[a-zA-Z0-9,_:]+$");
    }


    public static bool CheckCollectionSlug(string value)
    {
        // We allow for names:
        // Latin symbols in both cases
        // Digits
        // _ (Underscore)
        // - (Dash)
        return Regex.IsMatch(value, @"^[a-zA-Z0-9_\-]+$");
    }


    public static bool CheckTokenId(string value)
    {
        return Regex.IsMatch(value, @"^[_\-a-zA-Z0-9,]+$");
    }


    public static bool CheckLink(string value)
    {
        var result = Uri.TryCreate(value, UriKind.Absolute, out var uriResult)
                     && ( uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps
                                                                || uriResult.Scheme.ToLower() == "ipfs" ||
                                                                uriResult.Scheme.ToLower() == "ipfs-video" );

        return result;
    }


    public static bool CheckBase10(string value)
    {
        return Regex.IsMatch(value, @"^[0-9]+$");
    }


    public static bool CheckBase16(string value)
    {
        return Regex.IsMatch(value, @"^[xa-fA-F0-9]+$");
    }


    public static bool CheckBase64(string value)
    {
        return Regex.IsMatch(value, @"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$");
    }


    public static bool CheckUnixTimestamp(string value, bool onlySecondsAllowed = true)
    {
        return Regex.IsMatch(value, @"^[0-9]+$") && ( !onlySecondsAllowed || value.Length <= 10 );
    }


    public static bool CheckNumber(string value)
    {
        return Regex.IsMatch(value, @"^[0-9]+$");
    }


    public static bool CheckNonzeroPrice(string value)
    {
        if ( string.IsNullOrEmpty(value) ) return false;

        return !Regex.IsMatch(value, @"^0+$") && CheckNumber(value);
    }


    private static bool CheckLink(string link, string pattern)
    {
        return !string.IsNullOrEmpty(link) && Regex.IsMatch(link, pattern);
    }


    public static bool CheckDiscordLink(string link)
    {
        return CheckLink(link, DiscordPattern);
    }


    public static bool CheckFacebookLink(string link)
    {
        return CheckLink(link, FacebookPattern);
    }


    public static bool CheckInstagramLink(string link)
    {
        return CheckLink(link, InstagramPattern);
    }


    public static bool CheckSpotifyLink(string link)
    {
        return CheckLink(link, SpotifyPattern);
    }


    public static bool CheckTelegramLink(string link)
    {
        return CheckLink(link, TelegramPattern);
    }


    public static bool CheckYoutubeLink(string link)
    {
        return CheckLink(link, YoutubePattern);
    }


    public static bool CheckTwitterLink(string link)
    {
        return CheckLink(link, TwitterPattern);
    }


    /*
     * Use MailAddress(System.net.mail) to ensure a RFC822 Email address
     * see: http://www.ex-parrot.com/~pdw/Mail-RFC822-Address.html
     * We need to check if input address equals output address
     * because:
     * 'user1@hotmail.com; user2@gmail.com' is valid but extracted is only
     * 'user1@hotmail.com'
     */
    public static bool CheckEmailAddress(string address)
    {
        try
        {
            var extractedAddress = new MailAddress(address).Address;
            return extractedAddress == address;
        }
        catch ( FormatException )
        {
            return false;
        }
    }


    public static bool CheckUsername(string name)
    {
        return Regex.IsMatch(name, UsernamePattern);
    }


    public static bool CheckTitle(string title)
    {
        return Regex.IsMatch(title, TitlePattern);
    }


    public static bool CheckString(string value, bool charactersOnly = false)
    {
        return Regex.IsMatch(value, charactersOnly ? @"^[a-zA-Z_ ]+$" : @"^[a-zA-Z0-9_ ]+$");
    }


    public static bool CheckHash(string value, bool lowercase = false)
    {
        return Regex.IsMatch(value, lowercase ? @"^[a-zA-Z0-9]+$" : @"^[A-Z0-9]+$");
    }


    public static bool CheckSearch(string value)
    {
        return Regex.IsMatch(value, @"^[a-zA-Z0-9,_: ]+$");
    }
}
