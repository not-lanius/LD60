public static class GameCopy
{
    public static readonly string[] OpeningLines =
    {
        "Year 2077. The \"First Banana Impact\" wiped out 99.9% of all bananas on Earth.",
        "The remaining bananas were monopolized by the elite, locked tight inside the heavily guarded [Banana Center].",
        "Yet, a Qing Dynasty Taoist who tasted a banana once a century ago couldn't forget its sweetness—even in death.",
        "Resurrected by sheer craving, he learned the truth from a passerby. Upon hearing the fruit is now called \"Ba-na-na\", the Taoist snapped:",
        "\"Outrageous! Since they hoard the Ba-na-na, I shall TAKE IT MYSELF!\""
    };

    public static readonly string[] EndingLines =
    {
        "As the smoke clears over wrecked drones, the Taoist peels the golden banana.",
        "\"Mmm... sweet and creamy! Worth crawling out of my Qing Dynasty grave for!\"",
        "Yet, spotting a sign that reads [Banana Center: Outer Sector 1], the Taoist wipes his mouth and raises his peach-wood sword once more—",
        "\"One is barely an appetizer! Every Ba-na-na inside is MINE!\"\n\nThanks for playing our LD60 Demo! Please rate & leave a comment!"
    };

    public const string StelePrompt = "Press [E] to touch the ancient stele";

    public const string FireLine = "\"Stand between me and my banana? BURN! Behold the ancient art of [FIRE]!\"";
    public const string FireHint = "Time slowed! Copy the [FIRE] glyph shown above onto the yellow talisman. Hold [Left Click] to draw, [F] to cast, [R] to redraw.";
    public const string FireDescription = "[FIRE] — Draw like flames.\nLaunches a mid-range fireball. Ignites foes and explosive barrels.";

    public const string MountainStele = "You touched a relic inscribed with a bulletproof secret—the [MOUNTAIN] glyph!";
    public const string MountainLine = "\"Modern bullets are fast, but can they pierce a [MOUNTAIN]? Let's raise a rock wall!\"";
    public const string MountainDraw = "Copy the [MOUNTAIN] glyph shown above onto the talisman. Hold [Left Click] to draw, [F] to cast, [R] to redraw.";
    public const string MountainDescription = "[MOUNTAIN] — Draw 3 peaks.\nSummons a rock wall to block bullets. Explodes when destroyed!";

    public const string LightningStele = "Enlightenment! Learned the anti-air thunder art—the [LIGHTNING] glyph!";
    public const string LightningLine = "\"What are those buzzing iron birds?! Too high for my sword... Taste the Five Thunders!\"";
    public const string LightningDraw = "Copy the [LIGHTNING] glyph shown above onto the talisman. Hold [Left Click] to draw, [F] to cast, [R] to redraw.";
    public const string LightningDescription = "[LIGHTNING] — Draw a thunderbolt.\nCalls down sky thunder to zap airborne drones!";

    public const string GenericHint = "Copy the glyph shown above onto the talisman\n[F] Cast · [R] Redraw";
    public const string GenericDescription = "Draw a glyph on the talisman to invoke a spell.";
    public const string FireHintShort = "Copy [FIRE] above\n[F] Cast · [R] Redraw";
    public const string MountainHintShort = "Copy [MOUNTAIN] above\n[F] Cast · [R] Redraw";
    public const string LightningHintShort = "Copy [LIGHTNING] above\n[F] Cast · [R] Redraw";

    public static readonly string[] MoveJumpTutorial =
    {
        "Press [A] / [D] to Move",
        "[Space] to Jump"
    };

    public static readonly string[] DodgeTutorial =
    {
        "Press [Shift / K] to Dodge Roll!",
        "Brief invincibility — evade enemy fire!"
    };

    public static readonly string[] DoorTutorial =
    {
        "Press [E] to Open Door"
    };

    public static readonly string[] MeleeTutorial =
    {
        "Press [Left Click] to Slash!",
        "Hit enemies to gain MP (regenerates out of combat). On the talisman, [Left Click] draws instead."
    };

    public static readonly string[] FireTutorial =
    {
        FireLine,
        FireHint
    };

    public static readonly string[] MountainTutorial =
    {
        MountainStele,
        MountainLine,
        MountainDraw
    };

    public static readonly string[] LightningTutorial =
    {
        LightningStele,
        LightningLine,
        LightningDraw
    };
}
