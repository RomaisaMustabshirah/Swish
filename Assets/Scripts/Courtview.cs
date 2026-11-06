using UnityEngine;

public class Seg
{
    public readonly SpriteRenderer sr;

    public Seg(Transform parent, string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Art.Square;
        sr.sortingOrder = order;
        sr.enabled = false;
    }

    public void Set(Vector2 a, Vector2 b, float width, Color c)
    {
        Vector3 pa = Cfg.W(a.x, a.y), pb = Cfg.W(b.x, b.y), d = pb - pa;
        var t = sr.transform;
        t.position = (pa + pb) * .5f;
        t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(d.magnitude + width * Cfg.S * .5f, width * Cfg.S, 1f);
        sr.color = c;
        sr.enabled = true;
    }

    public void Hide() { sr.enabled = false; }
}

public class CourtView
{
    readonly SwishGame g;
    readonly Camera cam;
    readonly Transform root;
    readonly Backdrop back;

    SpriteRenderer glow, stem, board, strip, rimBar, shadow, ballR, rimDark, rimFront;
    SpriteRenderer pole, padding, baseR, cap;
    Seg ground, arm, brace;
    Seg[] ellA = new Seg[48], ellB = new Seg[32], net = new Seg[54];

    static readonly Color steelDay = new Color(.357f, .392f, .447f);
    static readonly Color steelNight = new Color(.165f, .188f, .235f);

    SpriteRenderer Spr(string name, Sprite sp, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = order;
        return sr;
    }

    public CourtView(SwishGame game, Camera camera)
    {
        g = game; cam = camera;
        root = new GameObject("Court").transform;
        root.SetParent(g.transform, false);

        back = new Backdrop(root);
        glow = Spr("Glow", Art.Glow, 1);
        ground = new Seg(root, "Ground", 3);
        for (int i = 0; i < ellA.Length; i++) ellA[i] = new Seg(root, "Ring", 3);
        for (int i = 0; i < ellB.Length; i++) ellB[i] = new Seg(root, "Ring", 3);

        pole = Spr("Pole", Art.Square, 8);
        cap = Spr("Cap", Art.Circle, 8);
        arm = new Seg(root, "Arm", 9);
        brace = new Seg(root, "Brace", 9);
        baseR = Spr("Base", Art.Square, 9);
        padding = Spr("Padding", Art.Square, 9);

        stem = Spr("Stem", Art.Square, 10);
        board = Spr("Board", Art.Square, 11);
        strip = Spr("Strip", Art.Square, 12);
        rimBar = Spr("RimBar", Art.Square, 13);
        shadow = Spr("Shadow", Art.Circle, 13);
        ballR = Spr("Ball", Art.BallFor(Store.Current), 15);
        for (int i = 0; i < net.Length; i++) net[i] = new Seg(root, "Net", 14);
        rimDark = Spr("RimDark", Art.Circle, 17);
        rimFront = Spr("RimFront", Art.Circle, 18);
    }

    static Color Alpha(Color c, float a) { c.a = a; return c; }

    static void SetRect(SpriteRenderer sr, float x, float y, float w, float h, Color c)
    {
        sr.transform.position = Cfg.W(x + w * .5f, y + h * .5f);
        sr.transform.localScale = new Vector3(w * Cfg.S, h * Cfg.S, 1f);
        sr.color = c;
    }

    static void Ellipse(Seg[] segs, float cx, float cy, float rx, float ry, float w, Color c)
    {
        int n = segs.Length;
        Vector2 prev = new Vector2(cx + rx, cy);
        for (int i = 1; i <= n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector2 p = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            segs[i - 1].Set(prev, p, w, c);
            prev = p;
        }
    }

    public void Layout()
    {
        back.Layout();
        ground.Set(new Vector2(-300, Cfg.GROUND), new Vector2(660, Cfg.GROUND), 2.5f, Alpha(Color.white, .45f));
        Ellipse(ellA, Cfg.WW / 2f, Cfg.GROUND + 34f, 150f, 24f, 2f, Alpha(Color.white, .22f));
        Ellipse(ellB, Cfg.WW / 2f, Cfg.GROUND + 34f, 28f, 7f, 2.5f, Alpha(Color.white, .55f));
    }

    static float EaseOutBack(float t)
    {
        float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    Vector2 NetPt(int i, int k, int N, int K)
    {
        float t = (float)i / N, lv = (float)k / K, w = Cfg.RIM_HALF - 3f;
        float f = 1f - .42f * lv + .32f * g.netBounce * Mathf.Sin(Mathf.PI * lv);
        float sway = Mathf.Sin(g.netT * 13f) * g.netBounce * 7f * lv - g.hoop.vx * .018f * lv;
        return new Vector2(g.hoop.x + (-w + 2f * w * t) * f + sway,
                           g.hoop.y + 2f + Cfg.NET_H * lv * (1f + .18f * g.netBounce));
    }

    public void Render()
    {
        Hoop h = g.hoop; Ball b = g.ball;
        float t = g.tod;
        float bx = h.x + Cfg.RIM_HALF + 7f;

        // camera (+ shake)
        Vector3 cp = g.camBase;
        if (g.shake > .05f)
            cp += new Vector3(Random.value - .5f, Random.value - .5f, 0f) * g.shake * Cfg.S;
        cam.transform.position = cp;

        back.Update(t, Time.deltaTime);
        cam.backgroundColor = back.TopColor(t);

        // spotlight on the hoop, stronger at night
        glow.transform.position = Cfg.W(h.x, h.y - 5f);
        glow.transform.localScale = Vector3.one * (520f * Cfg.S);
        glow.color = new Color(1f, .93f, .7f, Mathf.Lerp(.03f, .26f, t));

        // stand: fixed pole on the right + boom arm to the moving backboard
        Color steel = Color.Lerp(steelDay, steelNight, t);
        Color steelDark = Color.Lerp(steel, Color.black, .25f);
        float poleX = bx + Cfg.BOARD_W / 2f;
        float poleTop = g.hoop.baseY - 78f;
        SetRect(pole, poleX - 4.5f, poleTop, 9f, Cfg.GROUND - poleTop, steel);
        cap.transform.position = Cfg.W(poleX, poleTop);
        cap.transform.localScale = Vector3.one * (14f * Cfg.S);
        cap.color = steel;
        SetRect(baseR, poleX - 24f, Cfg.GROUND - 12f, 48f, 12f, steelDark);
        SetRect(padding, poleX - 6.5f, Cfg.GROUND - 76f, 13f, 64f, Theme.hoop);
        arm.Set(new Vector2(poleX, poleTop + 6f), new Vector2(bx + Cfg.BOARD_W / 2f, h.y - 34f), 7f, steel);
        brace.Set(new Vector2(poleX, poleTop + 150f), new Vector2(bx + Cfg.BOARD_W / 2f, h.y + 12f), 5f, steel);

        // board, stem, strip, rim bar
        SetRect(stem, h.x + Cfg.RIM_HALF, h.y - 3f, bx - Cfg.BOARD_W / 2f - (h.x + Cfg.RIM_HALF) + 1f, 6f, Theme.hoopDark);
        SetRect(board, bx - Cfg.BOARD_W / 2f, h.y - 62f, Cfg.BOARD_W, 84f, Theme.board);
        SetRect(strip, bx - Cfg.BOARD_W / 2f, h.y - 14f, Cfg.BOARD_W, 16f, Theme.hoop);
        SetRect(rimBar, h.x - Cfg.RIM_HALF, h.y - 3f, Cfg.RIM_HALF * 2f, 6f, Theme.hoop);

        // front rim
        Vector3 rp = Cfg.W(h.x - Cfg.RIM_HALF, h.y);
        rimDark.transform.position = rp;
        rimDark.transform.localScale = Vector3.one * ((Cfg.RIM_R + 2.6f) * 2f + 1.5f) * Cfg.S;
        rimDark.color = Theme.hoopDark;
        rimFront.transform.position = rp;
        rimFront.transform.localScale = Vector3.one * ((Cfg.RIM_R + 2.6f) * 2f) * Cfg.S;
        rimFront.color = Theme.hoop;

        // ball + shadow
        float s = EaseOutBack(Mathf.Clamp01(g.ballPop));
        bool show = s > .02f;
        ballR.enabled = show; shadow.enabled = show;
        if (show)
        {
            float hf = Mathf.Clamp01((Cfg.GROUND - Cfg.R - b.y) / 380f);
            shadow.transform.position = Cfg.W(b.x, Cfg.GROUND + 9f);
            shadow.transform.localScale = new Vector3(2f * Cfg.R * (1.15f - hf * .45f) * s * Cfg.S,
                                                      2f * Cfg.R * .28f * s * Cfg.S, 1f);
            shadow.color = new Color(0, 0, 0, .3f * (1f - hf * .75f));
            ballR.sprite = Art.BallFor(Store.Current);
            ballR.color = Color.white;
            ballR.transform.position = Cfg.W(b.x, b.y);
            ballR.transform.localScale = Vector3.one * (2f * Cfg.R * s * Cfg.S);
            ballR.transform.rotation = Quaternion.Euler(0, 0, -b.rot * Mathf.Rad2Deg);
        }

        // Show the ball behind the net only while it passes through it.
        bool ballInNet =
            g.scored &&
            b.y >= h.y &&
            b.y <= h.y + Cfg.NET_H + Cfg.R &&
            Mathf.Abs(b.x - h.x) < Cfg.RIM_HALF + Cfg.R;

        for (int i = 0; i < net.Length; i++)
        {
            net[i].sr.sortingOrder = ballInNet ? 16 : 14;
        }
        // net
        const int N = 6, K = 4;
        int idx = 0;
        for (int k = 0; k < K; k++)
            for (int i = 0; i < N; i++)
            {
                net[idx++].Set(NetPt(i, k, N, K), NetPt(i + 1, k + 1, N, K), 1.6f, Theme.net);
                net[idx++].Set(NetPt(i + 1, k, N, K), NetPt(i, k + 1, N, K), 1.6f, Theme.net);
            }
        for (int i = 0; i < N; i++)
            net[idx++].Set(NetPt(i, K, N, K), NetPt(i + 1, K, N, K), 1.6f, Theme.net);
    }
}