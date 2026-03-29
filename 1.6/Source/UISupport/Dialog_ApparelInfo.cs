using UnityEngine;
using Verse;

namespace FactionLoadout
{
    /// <summary>
    /// Simple apparel info dialog for use when Dialog_InfoCard is unavailable
    /// (e.g. main menu where Find.IdeoManager is null).
    /// Shows name, icon, layers, coverage and description.
    /// </summary>
    public class Dialog_ApparelInfo : Window
    {
        private readonly ThingDef _def;

        public override Vector2 InitialSize => new Vector2(440f, 280f);

        public Dialog_ApparelInfo(ThingDef def)
        {
            _def = def;
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(inRect.TopPartPixels(32f), _def.LabelCap);
            Text.Font = GameFont.Small;

            Rect body = inRect.BottomPartPixels(inRect.height - 40f);

            Texture2D icon = _def.uiIcon;
            if (icon != null)
            {
                Rect iconRect = new Rect(body.x, body.y, 64f, 64f);
                GUI.color = _def.uiIconColor;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            Rect textRect = new Rect(body.x + 72f, body.y, body.width - 72f, body.height - 36f);
            string info = DefUtils.BuildApparelTooltip(_def);
            Widgets.Label(textRect, info ?? string.Empty);
        }
    }
}
