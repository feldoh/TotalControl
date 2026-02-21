using UnityEngine;
using Verse;

namespace FactionLoadout.UISupport;

[StaticConstructorOnStartup]
public class Textures
{
    public static readonly Texture2D TC_Link = ContentFinder<Texture2D>.Get("UI/TC_Link");
}
