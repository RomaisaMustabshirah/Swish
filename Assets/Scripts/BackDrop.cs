using System.Collections.Generic;
using UnityEngine;

public struct Tri
{
    public Color d, k, n;
    public Tri(string day, string dusk, string night)
    {
        d = Theme.Hex(day); k = Theme.Hex(dusk); n = Theme.Hex(night);
    }
    public Color At(float t)
    {
        return t < .5f ? Color.Lerp(d, k, t * 2f) : Color.Lerp(k, n, (t - .5f) * 2f);
    }
}

public class Backdrop
{
    const float XL = -300f, XW = 960f;
    readonly Transform root;
    readonly float horizon;
    float lastT = -1f;

    Tri cTop = new Tri("#4f9fe8", "#5b4a8f", "#090e22");
    Tri cHor = new Tri("#c5e8ff", "#ff9a5c", "#1c2748");
    Tri cHill = new Tri("#4f9d56", "#3d6a4a", "#13271e");
    Tri cBld = new Tri("#8fa6bd", "#6a6288", "#0f1730");
    Tri cGrass = new Tri("#63b85d", "#4a8a4e", "#173321");
    Tri cTrunk = new Tri("#6b4a2f", "#4d3a35", "#1a1410");
    Tri cLeaf = new Tri("#3f9148", "#2f6b3f", "#0f2a1a");
    Tri cFence = new Tri("#d5dbe2", "#a59fba", "#46506e");
    Tri cFloor = new Tri("#6e7480", "#5a5570", "#212737");
    Tri cCloud = new Tri("#ffffff", "#ffc9a8", "#2e3a66");
    Color sunDay = Theme.Hex("#ffe066"), sunSet = Theme.Hex("#ff8a3d"), moonC = Theme.Hex("#f3f0d8");

    SpriteRenderer skyBase, skyTop, sunGlow, sun, moonGlow, moon, grass, floorR;
    SpriteRenderer[] stars = new SpriteRenderer[40];
    float[] starPh = new float[40];
    SpriteRenderer[][] cloudC = new SpriteRenderer[5][];
    float[] cloudX = new float[5], cloudY = new float[5], cloudSp = new float[5], cloudSc = new float[5];
    static readonly float[,] CO = { { 0, 0, 40 }, { -26, 6, 30 }, { 26, 8, 32 }, { 8, -12, 28 } };
    List<SpriteRenderer> blds = new List<SpriteRenderer>(), wins = new List<SpriteRenderer>(),
        hills = new List<SpriteRenderer>(), trunks = new List<SpriteRenderer>(),
        leaves = new List<SpriteRenderer>(), fence = new List<SpriteRenderer>();

    SpriteRenderer Spr(string n, Sprite sp, int order)
    {
        var go = new GameObject(n);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.sortingOrder = order;
        return sr;
    }

    static void Box(SpriteRenderer sr, float x, float y, float w, float h)
    {
        sr.transform.position = Cfg.W(x + w * .5f, y + h * .5f);
        sr.transform.localScale = new Vector3(w * Cfg.S, h * Cfg.S, 1f);
    }

    static void Dot(SpriteRenderer sr, float cx, float cy, float d)
    {
        sr.transform.position = Cfg.W(cx, cy);
        sr.transform.localScale = new Vector3(d * Cfg.S, d * Cfg.S, 1f);
    }

    static float Rf(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    static Sprite MakeFade()
    {
        var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
        var px = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++) px[y * 64 + x] = new Color(1, 1, 1, y / 63f);
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64f);
    }

    public Backdrop(Transform parent)
    {
        root = new GameObject("Backdrop").transform;
        root.SetParent(parent, false);
        horizon = Cfg.GROUND - 100f;
        var rnd = new System.Random(11);

        skyBase = Spr("SkyBase", Art.Square, -100);
        skyTop = Spr("SkyTop", MakeFade(), -99);

        for (int i = 0; i < stars.Length; i++)
        {
            stars[i] = Spr("Star", Art.Circle, -98);
            starPh[i] = Rf(rnd, 0f, 6.28f);
            Dot(stars[i], Rf(rnd, XL, XL + XW), Rf(rnd, 0f, 420f), Rf(rnd, 2f, 4.5f));
        }
        sunGlow = Spr("SunGlow", Art.Glow, -97);
        sun = Spr("Sun", Art.Circle, -96);
        moonGlow = Spr("MoonGlow", Art.Glow, -95);
        moon = Spr("Moon", Art.Circle, -94);

        for (int i = 0; i < 5; i++)
        {
            cloudX[i] = XL + i * (XW / 5f) + Rf(rnd, 0f, 80f);
            cloudY[i] = Rf(rnd, 60f, 330f);
            cloudSp[i] = Rf(rnd, 4f, 11f);
            cloudSc[i] = Rf(rnd, .8f, 1.5f);
            cloudC[i] = new SpriteRenderer[4];
            for (int j = 0; j < 4; j++) cloudC[i][j] = Spr("Cloud", Art.Circle, -93);
        }

        float x = XL;
        while (x < XL + XW)
        {
            float w = Rf(rnd, 26f, 56f), h = Rf(rnd, 45f, 130f);
            var b = Spr("Building", Art.Square, -92);
            Box(b, x, horizon - h, w, h + 12f); blds.Add(b);
            int rows = Mathf.Max(1, (int)(h / 24f));
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 2; c++)
                    if (rnd.NextDouble() < .6)
                    {
                        var wn = Spr("Window", Art.Square, -91);
                        Box(wn, x + 6f + c * (w * .5f - 3f), horizon - h + 8f + r * 22f, 6f, 8f);
                        wins.Add(wn);
                    }
            x += w + Rf(rnd, 2f, 10f);
        }

        for (int i = 0; i < 6; i++)
        {
            var hs = Spr("Hill", Art.Circle, -90);
            Dot(hs, XL + i * 180f + Rf(rnd, 0f, 60f), horizon + 95f, Rf(rnd, 260f, 340f));
            hills.Add(hs);
        }

        grass = Spr("Grass", Art.Square, -89);
        Box(grass, XL, horizon, XW, Cfg.GROUND - horizon);

        for (int i = 0; i < 8; i++)
        {
            float tx = XL + 40f + i * 120f + Rf(rnd, 0f, 40f);
            var tr = Spr("Trunk", Art.Square, -88);
            Box(tr, tx - 3f, horizon + 17f, 6f, 40f); trunks.Add(tr);
            var l1 = Spr("Leaf", Art.Circle, -87); Dot(l1, tx, horizon + 8f, 44f); leaves.Add(l1);
            var l2 = Spr("Leaf", Art.Circle, -87); Dot(l2, tx - 13f, horizon + 20f, 34f); leaves.Add(l2);
            var l3 = Spr("Leaf", Art.Circle, -87); Dot(l3, tx + 13f, horizon + 20f, 34f); leaves.Add(l3);
        }

        var rail1 = Spr("Rail", Art.Square, -86); Box(rail1, XL, Cfg.GROUND - 62f, XW, 3f); fence.Add(rail1);
        var rail2 = Spr("Rail", Art.Square, -86); Box(rail2, XL, Cfg.GROUND - 12f, XW, 3f); fence.Add(rail2);
        for (float px = XL; px < XL + XW; px += 18f)
        {
            var post = Spr("Post", Art.Square, -86);
            Box(post, px, Cfg.GROUND - 64f, 3f, 64f); fence.Add(post);
        }

        floorR = Spr("Asphalt", Art.Square, -80);
        Box(floorR, XL, Cfg.GROUND, XW, 400f);
    }

    public Color TopColor(float t) { return cTop.At(t); }

    // call at start and whenever the screen size changes
    public void Layout()
    {
        float y0 = Cfg.Y0;
        Box(skyBase, XL, y0 - 120f, XW, Cfg.WH - y0 + 400f);
        Box(skyTop, XL, y0 - 40f, XW, horizon - y0 + 40f);
        lastT = -1f;
    }

    static void SetAll(List<SpriteRenderer> l, Color c)
    {
        for (int i = 0; i < l.Count; i++) l[i].color = c;
    }

    void Recolor(float t)
    {
        skyBase.color = cHor.At(t);
        skyTop.color = cTop.At(t);
        SetAll(blds, cBld.At(t));
        SetAll(hills, cHill.At(t));
        grass.color = cGrass.At(t);
        SetAll(trunks, cTrunk.At(t));
        SetAll(leaves, cLeaf.At(t));
        Color f = cFence.At(t); f.a = .6f;
        SetAll(fence, f);
        floorR.color = cFloor.At(t);
        SetAll(wins, new Color(1f, .83f, .42f, Mathf.Clamp01((t - .5f) / .35f)));
    }

    // t: 0 = day, .5 = sunset, 1 = night
    public void Update(float t, float dt)
    {
        if (Mathf.Abs(t - lastT) > .0005f) { Recolor(t); lastT = t; }

        // sun sinks, moon rises (both pass behind the buildings)
        float st = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.25f));
        Color sc = Color.Lerp(sunDay, sunSet, Mathf.Clamp01(t * 1.25f));
        Dot(sun, 70f, 150f + st * 350f, 64f); sun.color = sc;
        Dot(sunGlow, 70f, 150f + st * 350f, 240f); sunGlow.color = new Color(sc.r, sc.g, sc.b, .45f);

        float mt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .45f) / .5f));
        bool mOn = mt > .001f;
        moon.enabled = mOn; moonGlow.enabled = mOn;
        Dot(moon, 270f, 520f - mt * 350f, 50f); moon.color = moonC;
        Dot(moonGlow, 270f, 520f - mt * 350f, 170f); moonGlow.color = new Color(1f, 1f, 1f, .25f);

        float sa = Mathf.Clamp01((t - .55f) / .35f);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].enabled = sa > .01f;
            stars[i].color = new Color(1f, 1f, 1f, sa * (.65f + .35f * Mathf.Sin(Time.time * 2f + starPh[i])));
        }

        Color cc = cCloud.At(t); cc.a = Mathf.Lerp(.95f, .35f, t);
        for (int i = 0; i < 5; i++)
        {
            cloudX[i] += cloudSp[i] * dt;
            if (cloudX[i] > XL + XW + 60f) cloudX[i] = XL - 60f;
            for (int j = 0; j < 4; j++)
            {
                float k = cloudSc[i];
                Dot(cloudC[i][j], cloudX[i] + CO[j, 0] * k, cloudY[i] + CO[j, 1] * k, CO[j, 2] * k);
                cloudC[i][j].color = cc;
            }
        }
    }
}