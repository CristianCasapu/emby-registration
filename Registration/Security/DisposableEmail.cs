namespace Registration.Security;

/// <summary>
/// Domenii de e-mail de unica folosinta, cele mai des intalnite. Lista nu e completa;
/// adminul poate adauga domenii in configuratie.
/// </summary>
public static class DisposableEmail
{
    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "10minutemail.com", "10minutemail.net", "20minutemail.com", "33mail.com", "anonaddy.me", "burnermail.io",
        "dispostable.com", "dropmail.me", "emailondeck.com", "fakeinbox.com", "fakemail.net", "getairmail.com",
        "getnada.com", "guerrillamail.biz", "guerrillamail.com", "guerrillamail.de", "guerrillamail.info",
        "guerrillamail.net", "guerrillamail.org", "guerrillamailblock.com", "sharklasers.com", "grr.la", "pokemail.net",
        "spam4.me", "harakirimail.com", "inboxbear.com", "incognitomail.org", "jetable.org", "mailcatch.com",
        "maildrop.cc", "mailinator.com", "mailinator.net", "mailinator2.com", "mailnesia.com", "mailpoof.com",
        "mailsac.com", "mintemail.com", "mohmal.com", "moakt.com", "mytemp.email", "nada.email", "throwawaymail.com",
        "temp-mail.org", "temp-mail.io", "tempail.com", "tempmail.com", "tempmail.net", "tempmail.dev", "tempmailo.com",
        "tempm.com", "tempr.email", "tmail.ws", "tmpmail.org", "tmpmail.net", "trashmail.com", "trashmail.de",
        "trashmail.net", "trash-mail.com", "yopmail.com", "yopmail.fr", "yopmail.net", "cool.fr.nf", "jetable.fr.nf",
        "nospam.ze.tc", "nomail.xl.cx", "mega.zik.dj", "speed.1s.fr", "courriel.fr.nf", "moncourrier.fr.nf",
        "monemail.fr.nf", "monmail.fr.nf", "emailfake.com", "emailtemporanea.com", "emailtemporar.ro", "spamgourmet.com",
        "mailforspam.com", "spambox.us", "spamfree24.org", "wegwerfmail.de", "wegwerfmail.net", "einrot.com",
        "byom.de", "discard.email", "discardmail.com", "email-fake.com", "fakemailgenerator.com", "armyspy.com",
        "cuvox.de", "dayrep.com", "einrot.de", "fleckens.hu", "gustr.com", "jourrapide.com", "rhyta.com",
        "superrito.com", "teleworm.us", "mail.tm", "emlhub.com", "emltmp.com", "tmpeml.com", "tmpbox.net",
        "linshiyouxiang.net", "1secmail.com", "1secmail.net", "1secmail.org", "esiix.com", "wwjmp.com", "xojxe.com",
        "yoggm.com", "kzccv.com", "qiott.com", "vjuum.com", "laafd.com", "txcct.com", "rteet.com", "dpptd.com",
        "inboxkitten.com", "mailbox.in.ua", "trashinbox.com", "tempinbox.com", "spamdecoy.net", "mt2015.com",
        "thankyou2010.com", "binkmail.com", "bobmail.info", "chammy.info", "devnullmail.com", "letthemeatspam.com",
        "mailin8r.com", "mailinater.com", "notmailinator.com", "reallymymail.com", "safetymail.info", "sogetthis.com",
        "spamherelots.com", "spamhereplease.com", "suremail.info", "thisisnotmyrealemail.com", "tradermail.info",
        "veryrealemail.com", "zippymail.info", "mvrht.net", "mvrht.com", "crazymailing.com", "emailna.co",
        "fexbox.org", "fexpost.com", "fextemp.com", "mailbox92.biz", "ezztt.com", "gufum.com",
    };

    public static bool IsDisposable(string domain) =>
        Domains.Contains(domain) || Domains.Any(d => domain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
}
