using Xemele.Parsing.Recovery;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private RecoveryDiagnostics? recovery;
    private List<int>? duplicateAttributes;
    private int discardedDepth, recoveryCloses;
    private bool outsideTextReported, outsideCData;
    private bool RecoveryEnabled => recovery is not null;

    internal void EnableRecovery(RecoveryDiagnostics diagnostics) => recovery = diagnostics;
    private bool Repair(string code, string message, XmlLocation location)
    {
        if (recovery is null) return false;
        if (recovery.Repair(code, message, location, out var terminal)) return true;
        diagnostic ??= terminal;
        return false;
    }
    private bool RecoverOutsideText(XmlLocation location)
    {
        if (!RecoveryEnabled) return false;
        if (outsideTextReported) return true;
        outsideTextReported = true;
        return Repair("XML.Recovery.OutsideRoot", "Discarded text outside the root.", location);
    }
    private bool RecoverOutsideCData()
    {
        if (!Repair("XML.Recovery.OutsideRoot", "Discarded CDATA outside the root.", opening)) return false;
        outsideCData = true;
        return true;
    }
    private void PrepareRecoveryTag() => duplicateAttributes?.Clear();
    private bool RecoverDuplicateAttribute(int index, XmlLocation location)
    {
        if (!Repair("XML.Recovery.DuplicateAttribute", "Retained the first expanded attribute name.", location)) return false;
        (duplicateAttributes ??= []).Add(index);
        return true;
    }
    private bool FinishRecoveryTag()
    {
        if (!RecoveryEnabled) return true;
        if (duplicateAttributes is { Count: > 0 })
        {
            int write = 0, duplicate = 0, oldCount = attributeCount;
            for (int read = 0; read < oldCount; read++)
            {
                Work(read);
                if (duplicate < duplicateAttributes.Count && duplicateAttributes[duplicate] == read) { duplicate++; continue; }
                attributes[write++] = attributes[read];
            }
            Array.Clear(attributes, write, oldCount - write);
            attributeCount = write;
        }
        if (rootClosed && frames.Count == 0)
        {
            if (!Repair("XML.Recovery.OutsideRoot", "Discarded an additional root element.", opening)) return false;
            discardedDepth = 1;
        }
        return true;
    }
    private bool RecoverClosingTag(int length)
    {
        if (!RecoveryEnabled) return false;
        var q = token.AsSpan(0, length); int colon = Colon(q);
        var prefix = colon < 0 ? ReadOnlySpan<char>.Empty : q[..colon];
        var local = colon < 0 ? q : q[(colon + 1)..];
        int match = frames.Count - 1;
        for (; match >= 0; match--)
        {
            Work(match);
            if (Equal(prefix, frames[match].Name.Prefix) && Equal(local, frames[match].Name.Local)) break;
        }
        if (match < 0)
        {
            if (!prefix.IsEmpty)
            {
                Resolve(q, false, opening);
                if (diagnostic is not null) return true;
            }
            Repair("XML.Recovery.UnmatchedClose", "Discarded an unmatched closing tag.", opening);
            return true;
        }
        if (entitySources is { Count: > 0 } && match < entitySources[^1].ElementDepth)
        {
            Reject("XML.Syntax", "Entity cannot close an element from its parent source.", opening);
            return true;
        }
        for (int n = frames.Count - 1; n > match; n--)
        {
            Work(n);
            if (!Repair("XML.Recovery.AncestorClose", "Closed an open element before an ancestor closing tag.", opening)) return true;
        }
        recoveryCloses = frames.Count - match;
        deliveredName = frames[^1].Name; delivery = Delivery.Close;
        return true;
    }
    private bool RecoverPhysicalEof(XmlLocation eof)
    {
        if (!RecoveryEnabled || state != State.Data || !rootSeen || frames.Count == 0) return false;
        // Finish pending text before emitting synthetic closes. Entity EOF is
        // handled by FinishEntitySource and never reaches this physical hook.
        if (textActive) { EndText(eof); return true; }
        for (int n = frames.Count - 1; n >= 0; n--)
        {
            Work(n);
            if (!Repair("XML.Recovery.EofClose", "Closed an open element at physical EOF.", eof)) return true;
        }
        opening = eof; recoveryCloses = frames.Count;
        deliveredName = frames[^1].Name; delivery = Delivery.Close;
        return true;
    }
    private void AdvanceRecoveryClose()
    {
        if (recoveryCloses == 0) return;
        if (--recoveryCloses != 0) { deliveredName = frames[^1].Name; delivery = Delivery.Close; }
    }
    private bool DrainDiscardedRecovery()
    {
        if (!RecoveryEnabled) return false;
        activeCancellation.ThrowIfCancellationRequested();
        if (delivery == Delivery.Text)
        {
            // The DOM already ignores outside-root XML S/text. Only CDATA or
            // a validated discarded subtree needs callback suppression.
            if (discardedDepth == 0 && !outsideCData)
            {
                if (fragmentEnded) outsideTextReported = false;
                return false;
            }
            textStarted = true;
            if (fragmentPending)
            {
                fragmentPending = false;
                if (fragmentEnded)
                {
                    textActive = textStarted = outsideCData = outsideTextReported = false;
                    budget.Reset(Xemele.Internal.Operations.Quota.TokenCharacters);
                }
            }
            delivery = Delivery.None;
            return true;
        }
        if (discardedDepth == 0) return false;
        if (delivery == Delivery.UnresolvedReference || (delivery == Delivery.None && unresolvedPending))
        {
            ClearUnresolvedReference(); delivery = Delivery.None; return true;
        }
        if (delivery == Delivery.Tag)
        {
            delivery = emptyTag ? Delivery.Close : Delivery.None;
            Array.Clear(attributes, 0, attributeCount);
            return true;
        }
        if (delivery == Delivery.Close)
        {
            delivery = Delivery.None; PopFrame(); AdvanceRecoveryClose();
            if (frames.Count < discardedDepth) discardedDepth = 0;
            return true;
        }
        return false;
    }
}
