using System;
using System.Collections.Generic;
using UnityEngine;

public static class Art
{
    static Sprite _square, _circle, _glow;
    static Texture2D _circleTex;
    static Dictionary<string, Sprite> _balls = new Dictionary<string, Sprite>();

    static Texture2D Gen(int n, Func<float, float, Color> f)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        var px = new Color[n * n];
        float h = n * .5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = f((x + .5f - h) / h, (y + .5f - h) / h);
        t.SetPixels(px);
        t.Apply(false, false);
        return t;
    }

    public static Sprite Square
    {
        get
        {
            if (_square == null)
            {
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                t.filterMode = FilterMode.Point;
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                t.SetPixels(px); t.Apply();
                _square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4f);
            }
            return _square;
        }
    }

    public static Texture2D CircleTex
    {
        get
        {
            if (_circleTex == null)
                _circleTex = Gen(128, (nx, ny) =>
                {
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    return new Color(1, 1, 1, Mathf.Clamp01((1f - r) * 64f + .5f));
                });
            return _circleTex;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
                _circle = Sprite.Create(CircleTex, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128f);
            return _circle;
        }
    }

    public static Sprite Glow
    {
        get
        {
            if (_glow == null)
            {
                var t = Gen(128, (nx, ny) =>
                {
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    return new Color(1, 1, 1, Mathf.Clamp01(1f - r));
                });
                _glow = Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128f);
            }
            return _glow;
        }
    }

    static float Frac(float v) { return v - Mathf.Floor(v); }

    public static Sprite BallFor(Skin sk)
    {
        Sprite sp;
        if (_balls.TryGetValue(sk.id, out sp)) return sp;

        Color baseC = Theme.Hex(sk.baseHex), accC = Theme.Hex(sk.accentHex);
        Color seamC = Color.Lerp(baseC, Color.black, .6f);
        int pat = sk.pattern;

        var t = Gen(256, (nx, ny) =>
        {
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float a = Mathf.Clamp01((1f - r) * 128f + .5f);
            if (a <= 0f) return new Color(0, 0, 0, 0);

            bool alt = false;
            switch (pat)
            {
                case 1: alt = Frac(ny * 2.5f + .5f) < .4f; break;
                case 2:
                {
                    float fx = Frac(nx * 3.5f + .5f) - .5f, fy = Frac(ny * 3.5f + .5f) - .5f;
                    alt = fx * fx + fy * fy < .09f;
                    break;
                }
                case 3: alt = ((Mathf.FloorToInt(nx * 3f + 3f) + Mathf.FloorToInt(ny * 3f + 3f)) & 1) == 0; break;
                case 4: alt = Frac(r * 3f) < .5f; break;
                case 5: alt = Frac(Mathf.Atan2(ny, nx) / (2f * Mathf.PI) * 8f + 1f) < .5f; break;
            }
            Color c = alt ? accC : baseC;

            const float hw = .06f;
            float d = Mathf.Min(Mathf.Abs(nx), Mathf.Abs(ny));
            float dx = nx + 1.45f;
            if (Mathf.Abs(Mathf.Atan2(ny, dx)) < 1.2f)
                d = Mathf.Min(d, Mathf.Abs(Mathf.Sqrt(dx * dx + ny * ny) - 1f));
            dx = nx - 1.45f;
            if (Mathf.Abs(Mathf.Atan2(ny, -dx)) < 1.2f)
                d = Mathf.Min(d, Mathf.Abs(Mathf.Sqrt(dx * dx + ny * ny) - 1f));
            float cov = Mathf.Clamp01((hw - d) * 128f + .5f);
            c = Color.Lerp(c, seamC, cov);

            float shade = 1f - .3f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, r));
            shade *= Mathf.Lerp(1f, .7f, Mathf.InverseLerp(.93f, 1f, r));
            c.r *= shade; c.g *= shade; c.b *= shade;
            c.a = a;
            return c;
        });

        sp = Sprite.Create(t, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f), 256f);
        _balls[sk.id] = sp;
        return sp;
    }
}