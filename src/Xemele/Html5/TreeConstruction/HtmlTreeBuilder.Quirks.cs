namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private static HtmlQuirksMode Quirks(HtmlDoctype d)
    {
        string p = d.PublicIdentifier ?? "";
        if (d.ForceQuirks || d.Name != "html" || EqualsAscii(p, "-//W3O//DTD W3 HTML Strict 3.0//EN//") || EqualsAscii(p, "-/W3C/DTD HTML 4.0 Transitional/EN") || EqualsAscii(p, "HTML") || EqualsAscii(d.SystemIdentifier ?? "", "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd")) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("+//Silmaril//dtd html Pro v0r11 19970101//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//AS//DTD HTML 3.0 asWedit + extensions//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//AdvaSoft Ltd//DTD HTML 3.0 asWedit + extensions//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0 Level 1//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0 Level 2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0 Strict Level 1//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0 Strict Level 2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0 Strict//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 2.1E//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 3.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 3.2 Final//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 3.2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML 3//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Level 0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Level 1//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Level 2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Level 3//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Strict Level 0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Strict Level 1//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Strict Level 2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Strict Level 3//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML Strict//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//IETF//DTD HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Metrius//DTD Metrius Presentational//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 2.0 HTML Strict//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 2.0 HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 2.0 Tables//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 3.0 HTML Strict//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 3.0 HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Microsoft//DTD Internet Explorer 3.0 Tables//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Netscape Comm. Corp.//DTD HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Netscape Comm. Corp.//DTD Strict HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//O'Reilly and Associates//DTD HTML 2.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//O'Reilly and Associates//DTD HTML Extended 1.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//SQ//DTD HTML 2.0 HoTMetaL + extensions//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//SoftQuad//DTD HoTMetaL PRO 4.0::19971010::extensions to HTML 4.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Spyglass//DTD HTML 2.0 Extended//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Sun Microsystems Corp.//DTD HotJava HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//Sun Microsystems Corp.//DTD HotJava Strict HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 3 1995-03-24//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 3.2 Draft//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 3.2 Final//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 3.2//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 3.2S Draft//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 4.0 Frameset//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML 4.0 Transitional//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML Experimental 19960712//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD HTML Experimental 970421//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3C//DTD W3 HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//W3O//DTD W3 HTML 3.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//WebTechs//DTD Mozilla HTML 2.0//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        if (p.StartsWith("-//WebTechs//DTD Mozilla HTML//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.Quirks;
        bool html4 = p.StartsWith("-//W3C//DTD HTML 4.01 Frameset//", StringComparison.OrdinalIgnoreCase) || p.StartsWith("-//W3C//DTD HTML 4.01 Transitional//", StringComparison.OrdinalIgnoreCase);
        if (html4 && string.IsNullOrEmpty(d.SystemIdentifier)) return HtmlQuirksMode.Quirks;
        if (html4 || p.StartsWith("-//W3C//DTD XHTML 1.0 Frameset//", StringComparison.OrdinalIgnoreCase) || p.StartsWith("-//W3C//DTD XHTML 1.0 Transitional//", StringComparison.OrdinalIgnoreCase)) return HtmlQuirksMode.LimitedQuirks;
        return HtmlQuirksMode.NoQuirks;
    }
    private static bool EqualsAscii(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
