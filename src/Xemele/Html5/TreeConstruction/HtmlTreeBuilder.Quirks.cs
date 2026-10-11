namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private static HtmlQuirksMode Quirks(HtmlDoctype d)
    {
        string p = d.PublicIdentifier ?? "";
        if (d.ForceQuirks || d.Name != "html" || EqualsAscii(p, "-//W3O//DTD W3 HTML Strict 3.0//EN//") || EqualsAscii(p, "-/W3C/DTD HTML 4.0 Transitional/EN") || EqualsAscii(p, "HTML") || EqualsAscii(d.SystemIdentifier ?? "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "+//Silmaril//dtd html Pro v0r11 19970101//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//AS//DTD HTML 3.0 asWedit + extensions//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//AdvaSoft Ltd//DTD HTML 3.0 asWedit + extensions//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0 Level 1//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0 Level 2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0 Strict Level 1//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0 Strict Level 2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0 Strict//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 2.1E//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 3.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 3.2 Final//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 3.2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML 3//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Level 0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Level 1//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Level 2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Level 3//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Strict Level 0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Strict Level 1//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Strict Level 2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Strict Level 3//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML Strict//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//IETF//DTD HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Metrius//DTD Metrius Presentational//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 2.0 HTML Strict//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 2.0 HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 2.0 Tables//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 3.0 HTML Strict//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 3.0 HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Microsoft//DTD Internet Explorer 3.0 Tables//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Netscape Comm. Corp.//DTD HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Netscape Comm. Corp.//DTD Strict HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//O'Reilly and Associates//DTD HTML 2.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//O'Reilly and Associates//DTD HTML Extended 1.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//SQ//DTD HTML 2.0 HoTMetaL + extensions//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//SoftQuad//DTD HoTMetaL PRO 4.0::19971010::extensions to HTML 4.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Spyglass//DTD HTML 2.0 Extended//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Sun Microsystems Corp.//DTD HotJava HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//Sun Microsystems Corp.//DTD HotJava Strict HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 3 1995-03-24//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 3.2 Draft//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 3.2 Final//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 3.2//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 3.2S Draft//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 4.0 Frameset//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 4.0 Transitional//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML Experimental 19960712//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD HTML Experimental 970421//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3C//DTD W3 HTML//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//W3O//DTD W3 HTML 3.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//WebTechs//DTD Mozilla HTML 2.0//")) return HtmlQuirksMode.Quirks;
        if (HtmlAscii.StartsWith(p, "-//WebTechs//DTD Mozilla HTML//")) return HtmlQuirksMode.Quirks;
        bool html4 = HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 4.01 Frameset//") || HtmlAscii.StartsWith(p, "-//W3C//DTD HTML 4.01 Transitional//");
        if (html4 && string.IsNullOrEmpty(d.SystemIdentifier)) return HtmlQuirksMode.Quirks;
        if (html4 || HtmlAscii.StartsWith(p, "-//W3C//DTD XHTML 1.0 Frameset//") || HtmlAscii.StartsWith(p, "-//W3C//DTD XHTML 1.0 Transitional//")) return HtmlQuirksMode.LimitedQuirks;
        return HtmlQuirksMode.NoQuirks;
    }
    private static bool EqualsAscii(string a, string b) => HtmlAscii.Equals(a, b);
}
