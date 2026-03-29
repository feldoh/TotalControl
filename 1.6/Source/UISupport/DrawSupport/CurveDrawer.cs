using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport.DrawSupport;

/// <summary>
/// Draws an editable <see cref="SimpleCurve"/> with add/remove point controls.
/// Used by RaidPointsTab and RaidLootTab.
/// </summary>
public static class CurveDrawer
{
    public static void DrawCurve(Listing_Standard listing, ref SimpleCurve curve, ref List<(string x, string y)> curvePointBuffer)
    {
        curvePointBuffer ??= [];

        for (int i = 0; i < curve.PointsCount; i++)
        {
            CurvePoint point = curve[i];
            if (curvePointBuffer.Count <= i)
                curvePointBuffer.Add((point.x.ToString(CultureInfo.InvariantCulture), point.y.ToString(CultureInfo.InvariantCulture)));

            Rect pointRect = listing.GetRect(Text.LineHeight + 3);

            Widgets.Label(pointRect.LeftHalf().LeftHalf(), "FactionLoadout_CurvePoint".Translate(i + 1, point.x, point.y));

            (string x, string y) buffer = curvePointBuffer[i];
            Widgets.TextFieldNumeric(pointRect.LeftHalf().RightHalf(), ref point.loc.x, ref buffer.x);
            Widgets.TextFieldNumeric(pointRect.RightHalf().LeftHalf(), ref point.loc.y, ref buffer.y);
            curvePointBuffer[i] = buffer;
            curve[i] = point;

            if (Widgets.ButtonText(pointRect.RightHalf().RightHalf(), "Remove".Translate()))
            {
                curve.Points.RemoveAt(i);
                curvePointBuffer.RemoveAt(i);
            }

            listing.GapLine();
        }

        if (listing.ButtonText("Add".Translate()))
        {
            CurvePoint p = curve.MaxByWithFallback(e => e.x, new CurvePoint(0, 0));
            float px = p.x + 1;
            float py = p.y + 1;
            ModCore.Debug($"Adding point {px}, {py}");
            curve.Add(px, py);
            curvePointBuffer.Add((px.ToString(CultureInfo.InvariantCulture), py.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
