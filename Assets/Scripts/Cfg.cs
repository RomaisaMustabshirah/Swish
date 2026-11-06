using UnityEngine;

public static class Cfg
{
    public const float WW = 360f, R = 18f, G = 1150f, MAXPULL = 150f;
    public const float RIM_HALF = 40f, RIM_R = 3.5f, BOARD_W = 9f, NET_H = 46f;
    public const float E_FLOOR = .58f, E_WALL = .55f, E_RIM = .55f, E_BOARD = .6f;
    public const float S = 0.01f;       // Unity world units per game pixel
    public const float ZOOM = 1.35f;    // higher = more zoomed in

    public static float WH = 640f, GROUND = 570f, MAXSPEED = 1000f;
    public static float X0 = 0f, X1 = 360f, Y0 = 0f;   // visible play area (set in Layout)

    public static Vector3 W(float x, float y)
    {
        return new Vector3((x - WW / 2f) * S, (WH / 2f - y) * S, 0f);
    }
}

public static class Theme
{
    public static Color bg, outer, floor, ink, ball, ballDark, hoop, hoopDark, board, net;

    public static Color Hex(string h)
    {
        Color c; ColorUtility.TryParseHtmlString(h, out c); c.a = 1f; return c;
    }

    static float Lum(Color c)
    {
        float[] v = { c.r, c.g, c.b };
        for (int i = 0; i < 3; i++)
            v[i] = v[i] <= .03928f ? v[i] / 12.92f : Mathf.Pow((v[i] + .055f) / 1.055f, 2.4f);
        return .2126f * v[0] + .7152f * v[1] + .0722f * v[2];
    }

    public static void Apply(string bgHex, string ballHex, string hoopHex)
    {
        bg = Hex(bgHex);
        bool light = Lum(bg) > .45f;
        floor = Color.Lerp(bg, Color.black, light ? .13f : .3f);
        outer = Color.Lerp(bg, Color.black, light ? .3f : .45f);
        ink = light ? Hex("#16181d") : Color.white;
        ball = Hex(ballHex);
        ballDark = Color.Lerp(ball, Color.black, .55f);
        hoop = Hex(hoopHex);
        hoopDark = Color.Lerp(hoop, Color.black, .4f);
        board = light ? Hex("#fbfaf6") : Hex("#f4f3ee");
        net = light ? new Color(30 / 255f, 30 / 255f, 34 / 255f, .8f) : new Color(1, 1, 1, .85f);
    }
}