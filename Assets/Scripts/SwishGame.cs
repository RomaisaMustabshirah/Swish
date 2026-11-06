using UnityEngine;

public enum Mode { Home, Settings, Play, Pause, Over, Shop }
public enum Phase { Ready, Aim, Fly, Settle }

[System.Serializable]
public class Ball { public float x, y, vx, vy, rot, vr; }

[System.Serializable]
public class Hoop
{
    public float x = 162, y = 200, vx, vy, baseX = 162, baseY = 200;
    public float ax, ay, wx = 1, wy = 1, phX, phY, yOff;
}

public class SwishGame : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("Swish Street").AddComponent<SwishGame>(); }

    // ---- state ----
    public Mode mode = Mode.Play;
    public Phase phase = Phase.Ready;
    public int score, lives = 3, swishes, rimIns, best;
    public bool newBest, guide = true, hintSeen;
    public float netBounce, netT, shake, ballPop = 1f;
    public Ball ball = new Ball();
    public Hoop hoop = new Hoop();
    public float tod;                                       // 0 = day, 1 = night
    public Vector3 camBase = new Vector3(0f, 0f, -10f);
    const float NIGHT_AT = 30f;                             // score at which it is full night
    Vector2 aimDown, aimCur;

    // Resting spot AND physics floor: the ball's bottom touches the centre of the inner circle
    public float RestY { get { return Cfg.GROUND + 34f - Cfg.R; } }

    bool ending, touched;
    public bool scored;
    float resolveT, flight, hopT, lastHit, lastFloor;
    int lastW, lastH;
    Camera cam;
    CourtView view;

    struct HT { public float ax, ay, wx, wy, yo; }

    // ---------------- setup ----------------
    void Awake()
    {
        Input.simulateMouseWithTouches = true;

        // smoother rendering: own the frame rate, smooth edges
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        QualitySettings.antiAliasing = 4;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Store.Load();
        Theme.Apply("#1b2433", Store.Current.baseHex, "#e63b2e");
        best = PlayerPrefs.GetInt("swish-street-best", 0);
        hintSeen = PlayerPrefs.GetInt("swish-street-seen", 0) == 1;

        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.rotation = Quaternion.identity;

        view = new CourtView(this, cam);
        Fx.Init(transform);
        Layout();
        Layout();
        hoop.x = hoop.baseX;
        hoop.y = hoop.baseY;

        ResetBall();
        mode = Mode.Home;

        SwishMusic.Initialize();
    }

    void Layout()
    {
        lastW = Screen.width; lastH = Screen.height;
        float aspect = (float)Screen.width / Screen.height;
        float ch = Mathf.Clamp(Cfg.WW / Cfg.ZOOM / aspect, 470f, 640f);   // visible height in game px
        float visW = ch * aspect;

        Cfg.WH = 640f;
        Cfg.GROUND = 570f;
        Cfg.Y0 = Cfg.WH - ch;
        float half = Mathf.Min(visW * .5f, Cfg.WW * .5f);
        Cfg.X0 = Cfg.WW * .5f - half;
        Cfg.X1 = Cfg.WW * .5f + half;

        hoop.baseY = Mathf.Round(Cfg.Y0 + ch * .27f);
        hoop.baseX = Cfg.WW / 2f;                       // basket in the middle of the screen
        float h = RestY - (hoop.baseY - 36f - 30f);
        Cfg.MAXSPEED = Mathf.Sqrt(2f * Cfg.G * Mathf.Max(140f, h)) * 1.2f;

        cam.orthographicSize = Cfg.S * ch / 2f;
        camBase = new Vector3(0f, (ch - Cfg.WH) * .5f * Cfg.S, -10f);
        cam.transform.position = camBase;

        if (mode != Mode.Play || phase == Phase.Ready) ball.y = RestY;
        view.Layout();
    }

    // ---------------- main loop ----------------
    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH) Layout();

        // smoothDeltaTime removes frame-time jitter, which is what makes motion look stuttery
        float dt = Mathf.Min(Time.smoothDeltaTime, .05f);

        HandleInput();
        Tick(dt);
        view.Render();
    }

    void Tick(float dt)
    {
        if (mode == Mode.Pause) return;
        float todTarget = (mode == Mode.Play || mode == Mode.Over) ? Mathf.Clamp01(score / NIGHT_AT) : 0f;
        tod += (todTarget - tod) * Mathf.Min(1f, dt * .9f);
        netT += dt;
        netBounce = Mathf.Max(0f, netBounce - dt * 1.3f);
        shake = Mathf.Max(0f, shake - dt * 22f);

        if (mode == Mode.Home || mode == Mode.Settings)
        {
            Advance(dt);
            hopT += dt;

            ball.x = Cfg.WW / 2f;
            ball.y = RestY
                - 72f * ((hopT * 0.85f) % 1f)
                * (1f - ((hopT * 0.85f) % 1f));

            ball.rot += dt * 2.4f;
            ballPop = 1f;
        }
        else if (mode == Mode.Play)
        {
            if (phase == Phase.Ready) ballPop = Mathf.Min(1f, ballPop + dt * 4.5f);
            Advance(dt);
        }
        Fx.Tick(dt);
    }

    void Advance(float dt)
    {
        if (dt <= 0f) return;                           // avoid divide-by-zero in Step
        dt = Mathf.Min(dt, 1f / 20f);
        int n = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 240f)));
        float h = dt / n;
        for (int i = 0; i < n; i++) Step(h);
    }

    // ---------------- hoop movement ----------------
    HT Targets()
    {
        if (mode == Mode.Home || mode == Mode.Settings)
            return new HT { ax = 26, ay = 0, wx = .7f, wy = 1, yo = 24 };
        int s = score;
        if (s < 5) return new HT { ax = 0, ay = 0, wx = 1, wy = 1.2f, yo = 0 };
        if (s < 12) return new HT { ax = 40 + (s - 5) * 4, ay = 0, wx = .95f + (s - 5) * .05f, wy = 1.2f, yo = 0 };
        if (s < 20) return new HT { ax = 0, ay = 22 + (s - 12) * 1.7f, wx = 1, wy = 1.15f + (s - 12) * .06f, yo = 0 };
        return new HT
        {
            ax = Mathf.Min(78f, 50f + (s - 20) * 2f), ay = Mathf.Min(36f, 24f + (s - 20)),
            wx = Mathf.Min(2.1f, 1.3f + (s - 20) * .04f), wy = Mathf.Min(2f, 1.45f + (s - 20) * .04f), yo = 0
        };
    }

    void MoveHoop(float h)
    {
        HT tg = Targets();
        float k = Mathf.Min(1f, h * 1.6f);
        hoop.ax += (tg.ax - hoop.ax) * k; hoop.ay += (tg.ay - hoop.ay) * k;
        hoop.wx += (tg.wx - hoop.wx) * k; hoop.wy += (tg.wy - hoop.wy) * k;
        hoop.yOff += (tg.yo - hoop.yOff) * Mathf.Min(1f, h * 3f);
        hoop.phX += hoop.wx * h; hoop.phY += hoop.wy * h;
        hoop.x = hoop.baseX + hoop.ax * Mathf.Sin(hoop.phX);
        hoop.y = hoop.baseY + hoop.yOff + hoop.ay * Mathf.Sin(hoop.phY);
    }

    // ---------------- physics ----------------
    void Step(float h)
    {
        float ox = hoop.x, oy = hoop.y;
        MoveHoop(h);
        hoop.vx = (hoop.x - ox) / h; hoop.vy = (hoop.y - oy) / h;
        if (mode != Mode.Play || (phase != Phase.Fly && phase != Phase.Settle)) return;

        flight += h;
        float prevRel = ball.y - oy;
        ball.vy += Cfg.G * h; ball.x += ball.vx * h; ball.y += ball.vy * h; ball.rot += ball.vr * h;

        if (ball.x < Cfg.X0 + Cfg.R) { ball.x = Cfg.X0 + Cfg.R; if (ball.vx < 0) ball.vx = -ball.vx * Cfg.E_WALL; }
        if (ball.x > Cfg.X1 - Cfg.R) { ball.x = Cfg.X1 - Cfg.R; if (ball.vx > 0) ball.vx = -ball.vx * Cfg.E_WALL; }
        if (ball.y < Cfg.Y0 + Cfg.R) { ball.y = Cfg.Y0 + Cfg.R; if (ball.vy < 0) ball.vy = -ball.vy * .4f; }

        float floorY = RestY;           // same height the ball rests at, so there is no jump on launch
        if (ball.y > floorY)
        {
            ball.y = floorY;
            if (ball.vy > 0)
            {
                float imp = ball.vy;
                ball.vy = -ball.vy * Cfg.E_FLOOR; ball.vx *= .9f;
                if (Mathf.Abs(ball.vy) < 45f) ball.vy = 0f;
                if (imp > 110f && Time.time - lastFloor > .08f) { lastFloor = Time.time; Audio.Play(Snd.Floor, imp); }
            }
            if (Mathf.Abs(ball.vy) < 1f) { ball.vx *= 1f - 1.6f * h; ball.vr = ball.vx / Cfg.R; }
        }

        HitCircle(hoop.x - Cfg.RIM_HALF, hoop.y, Cfg.RIM_R, Cfg.E_RIM);
        HitCircle(hoop.x + Cfg.RIM_HALF, hoop.y, Cfg.RIM_R, Cfg.E_RIM);
        HitBoard();

        if (phase == Phase.Fly && !scored)
        {
            float rel = ball.y - hoop.y;
            if (prevRel < 0f && rel >= 0f && ball.vy > 0f && Mathf.Abs(ball.x - hoop.x) < Cfg.RIM_HALF - 3f) OnScore();
            else if (ball.vy > 0f && rel > 80f) OnMiss();
            else if (flight > 7f) OnMiss();
        }
        if (scored && ball.y < hoop.y + Cfg.NET_H + Cfg.R && Mathf.Abs(ball.x - hoop.x) < Cfg.RIM_HALF + 6f)
        {
            ball.vx *= 1f - Mathf.Min(.9f, 7f * h);
            ball.vx += (hoop.x - ball.x) * 18f * h;
            if (ball.vy > 360f) ball.vy = 360f;
        }
        if (phase == Phase.Settle)
        {
            resolveT -= h;
            if (resolveT <= 0f) { if (ending) GameOver(); else ResetBall(); }
        }
    }

    void HitCircle(float cx, float cy, float rr, float e)
    {
        float dx = ball.x - cx, dy = ball.y - cy, m = Cfg.R + rr, d2 = dx * dx + dy * dy;
        if (d2 >= m * m) return;
        float d = Mathf.Sqrt(d2); if (d <= 0f) d = .0001f;
        float nx = dx / d, ny = dy / d;
        ball.x = cx + nx * m; ball.y = cy + ny * m;
        float rvx = ball.vx - hoop.vx, rvy = ball.vy - hoop.vy, vn = rvx * nx + rvy * ny;
        if (vn < 0f)
        {
            rvx -= (1f + e) * vn * nx; rvy -= (1f + e) * vn * ny;
            ball.vx = rvx + hoop.vx; ball.vy = rvy + hoop.vy; ball.vr = ball.vx * .03f;
            Contact(false, -vn);
        }
    }

    void HitBoard()
    {
        float bx = hoop.x + Cfg.RIM_HALF + 7f, left = bx - Cfg.BOARD_W / 2f, right = bx + Cfg.BOARD_W / 2f;
        float top = hoop.y - 62f, bot = hoop.y + 22f;
        float cx = Mathf.Clamp(ball.x, left, right), cy = Mathf.Clamp(ball.y, top, bot);
        float dx = ball.x - cx, dy = ball.y - cy, d2 = dx * dx + dy * dy, nx, ny;
        if (d2 >= Cfg.R * Cfg.R) return;
        if (d2 == 0f) { nx = -1f; ny = 0f; ball.x = left - Cfg.R; }
        else
        {
            float d = Mathf.Sqrt(d2); nx = dx / d; ny = dy / d;
            ball.x = cx + nx * Cfg.R; ball.y = cy + ny * Cfg.R;
        }
        float rvx = ball.vx - hoop.vx, rvy = ball.vy - hoop.vy, vn = rvx * nx + rvy * ny;
        if (vn < 0f)
        {
            rvx -= (1f + Cfg.E_BOARD) * vn * nx; rvy -= (1f + Cfg.E_BOARD) * vn * ny;
            ball.vx = rvx + hoop.vx; ball.vy = rvy + hoop.vy; ball.vr = ball.vx * .03f;
            Contact(true, -vn);
        }
    }

    void Contact(bool board, float speed)
    {
        touched = true;
        if (speed > 70f && Time.time - lastHit > .055f)
        {
            lastHit = Time.time;
            Audio.Play(board ? Snd.Board : Snd.Rim, speed);
        }
        shake = Mathf.Max(shake, Mathf.Min(5f, speed / 130f));
    }

    // ---------------- ball flow ----------------
    void ResetBall()
    {
        ball.x = Cfg.WW / 2f;
        ball.y = RestY;

        ball.vx = 0f;
        ball.vy = 0f;
        ball.vr = 0f;
        ball.rot = 0f;

        ballPop = 0f;
        phase = Phase.Ready;
        touched = false;
        scored = false;
        flight = 0f;
    }

    void Launch(Vector2 d)
    {
        float k = Cfg.MAXSPEED / Cfg.MAXPULL;
        ball.vx = d.x * k * .55f; ball.vy = d.y * k; ball.vr = ball.vx * .03f;
        phase = Phase.Fly; flight = 0f; touched = false; scored = false;
        hintSeen = true; PlayerPrefs.SetInt("swish-street-seen", 1);
        Audio.Play(Snd.Shoot);
    }

    void OnScore()
    {
        scored = true;

        bool swish = !touched;          // no rim or board contact
        int pts = swish ? 2 : 1;
        int prev = score;

        score += pts;       // Current game score
        Store.Add(pts);     // Coins earned
        earned += pts;

        if (swish) swishes++;
        else rimIns++;

        netBounce = swish ? 1.6f : 1f;  // net snaps harder on a swish
        phase = Phase.Settle; resolveT = 1.05f;

        float px = Mathf.Clamp(hoop.x, 80f, 280f);

        if (swish)
        {
            // Big centered callout, a smaller +2 under it, a larger burst and a screen punch
            Fx.Popup("SWISH!", Cfg.WW / 2f, hoop.y + Cfg.NET_H + 90f, Theme.ball, 60f, 1.4f);
            Fx.Popup("+2", px, hoop.y + Cfg.NET_H + 140f, Theme.ink, 34f, 1.2f);
            Fx.Burst(hoop.x, hoop.y + 10f, 50);
            shake = Mathf.Max(shake, 4f);
            Audio.Play(Snd.Score2);
            Audio.Play(Snd.Swish);
        }
        else
        {
            Fx.Popup("+1", px, hoop.y + Cfg.NET_H + 70f, Theme.ink, 30f, 1.1f);
            Fx.Burst(hoop.x, hoop.y + 10f, 14);
            Audio.Play(Snd.Score1);
        }

        if (prev < 5 && score >= 5) Fx.Popup("HOOP IS MOVING!", Cfg.WW / 2f, Cfg.WH * .5f, Theme.ink, 34f, 2f);
        else if (prev < 12 && score >= 12) Fx.Popup("NOW UP AND DOWN!", Cfg.WW / 2f, Cfg.WH * .5f, Theme.ink, 34f, 2f);
        else if (prev < 20 && score >= 20) Fx.Popup("BOTH WAYS!", Cfg.WW / 2f, Cfg.WH * .5f, Theme.ink, 36f, 2f);
    }

    void OnMiss()
    {
        phase = Phase.Settle; resolveT = .85f; lives--;
        if (lives <= 0) ending = true;
        Fx.Popup("MISS", Mathf.Clamp(ball.x, 60f, 300f), Mathf.Min(Cfg.GROUND - 70f, hoop.y + 120f), Theme.ink, 30f, .9f);
        Audio.Play(Snd.Miss);
    }

    // ---------------- modes ----------------
    public void StartGame()
    {
        score = 0; lives = 3; swishes = 0; rimIns = 0; ending = false; newBest = false;
        Fx.Clear();
        earned = 0;
        ResetBall();
        mode = Mode.Play;
    }

    void GameOver()
    {
        mode = Mode.Over;
        if (score > best)
        {
            best = score; newBest = true;
            PlayerPrefs.SetInt("swish-street-best", best);
            PlayerPrefs.Save();
        }
        Audio.Play(Snd.Over);
    }

    // ---------------- input ----------------
    public Vector2 AimVec()
    {
        Vector2 d = aimCur - aimDown;
        float len = d.magnitude;
        if (len > Cfg.MAXPULL) d *= Cfg.MAXPULL / len;
        return d;
    }

    bool ReadPointer(out Vector2 pos, out bool pressed, out bool down, out bool released)
    {
#if ENABLE_INPUT_SYSTEM
        var p = UnityEngine.InputSystem.Pointer.current;
        if (p == null) { pos = default; pressed = down = released = false; return false; }
        pos = p.position.ReadValue();
        pressed = p.press.wasPressedThisFrame;
        down = p.press.isPressed;
        released = p.press.wasReleasedThisFrame;
        return true;
#else
        pos = Input.mousePosition;
        pressed = Input.GetMouseButtonDown(0);
        down = Input.GetMouseButton(0);
        released = Input.GetMouseButtonUp(0);
        return true;
#endif
    }

    Vector2 ScreenToGame(Vector2 sp)
    {
        float wh = cam.orthographicSize * 2f;
        float wx = (sp.x - Screen.width * .5f) / Screen.height * wh + camBase.x;
        float wy = (sp.y - Screen.height * .5f) / Screen.height * wh + camBase.y;
        return new Vector2(wx / Cfg.S + Cfg.WW / 2f, Cfg.WH / 2f - wy / Cfg.S);
    }

    void HandleInput()
    {
        if (mode != Mode.Play) return;
        Vector2 sp; bool pressed, down, released;
        if (!ReadPointer(out sp, out pressed, out down, out released)) return;
        float guiS = Mathf.Min(Screen.width / Cfg.WW, Screen.height / 600f);
        float top = Screen.height - (Screen.safeArea.y + Screen.safeArea.height);

        Vector2 guiPos = new Vector2(sp.x, Screen.height - sp.y);

        Rect pauseRect = new Rect(
            Screen.width - 48f * guiS,
            top + 58f * guiS,
            34f * guiS,
            34f * guiS
        );

        if (pressed && pauseRect.Contains(guiPos))
            return;

        Vector2 gp = ScreenToGame(sp);

        if (phase == Phase.Ready && pressed && ballPop >= .6f)
        {
            aimDown = aimCur = gp; phase = Phase.Aim;
        }
        else if (phase == Phase.Aim)
        {
            aimCur = gp;
            if (released)
            {
                Vector2 d = AimVec();
                if (d.y < -10f) Launch(d); else phase = Phase.Ready;
            }
        }
    }

    // ---------------- HUD + shop (simple on-screen GUI for now) ----------------
    GUIStyle sBig, sMid, sHuge, sBtn, sTitle, sTiny, sLeft;
    GUIStyle panelStyle, cardStyle, selStyle;
    float styleScale;

    static Texture2D panelTexture;
    static Texture2D cardTexture;
    static Texture2D selTexture;
    static Texture2D buttonTexture;
    static Texture2D buttonHoverTexture;

    int earned;
    Mode shopReturn = Mode.Over;
    string shopMsg = "";
    float shopMsgT;

    static readonly Color gold = new Color(1f, .76f, .1f);

    // Rounded rectangle with an anti-aliased edge. radius is in texture pixels.
    Texture2D MakeRoundedTexture(Color color, int size, float radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + .5f, fy = y + .5f;
                float qx = Mathf.Clamp(fx, radius, size - radius);
                float qy = Mathf.Clamp(fy, radius, size - radius);
                float d = Mathf.Sqrt((fx - qx) * (fx - qx) + (fy - qy) * (fy - qy));

                Color c = color;
                c.a *= Mathf.Clamp01(radius - d + .5f);
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static int Bd(float radius) { return Mathf.CeilToInt(radius) + 1; }

    Texture2D Rounded(Color color, float radius)
    {
        return MakeRoundedTexture(color, Bd(radius) * 2 + 2, radius);
    }

    GUIStyle BoxStyle(Texture2D tex, float radius)
    {
        int b = Bd(radius);
        GUIStyle st = new GUIStyle();
        st.normal.background = tex;
        st.border = new RectOffset(b, b, b, b);     // 9-slice: corners stay round at any size
        return st;
    }

    static void Kill(ref Texture2D t)
    {
        if (t != null) Destroy(t);
        t = null;
    }

    void BuildStyles(float s)
    {
        styleScale = s;

        // Rebuild textures at the current screen scale so the corners are sharp, not stretched.
        Kill(ref panelTexture); Kill(ref cardTexture); Kill(ref selTexture);
        Kill(ref buttonTexture); Kill(ref buttonHoverTexture);

        float rPanel = 22f * s, rCard = 14f * s, rSel = 16f * s, rBtn = 12f * s;

        panelTexture = Rounded(new Color(0.055f, 0.085f, 0.14f, 0.97f), rPanel);
        cardTexture = Rounded(new Color(0.12f, 0.16f, 0.23f, 1f), rCard);
        selTexture = Rounded(gold, rSel);
        buttonTexture = Rounded(new Color(0.95f, 0.32f, 0.12f, 1f), rBtn);
        buttonHoverTexture = Rounded(new Color(1f, 0.48f, 0.20f, 1f), rBtn);

        panelStyle = BoxStyle(panelTexture, rPanel);
        cardStyle = BoxStyle(cardTexture, rCard);
        selStyle = BoxStyle(selTexture, rSel);

        sBig = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(48 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        sMid = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(17 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        sHuge = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(76 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        sTitle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(32 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        sTiny = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(11 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        sLeft = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(18 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        int bb = Bd(rBtn);
        sBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(18 * s),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(12, 12, 10, 10),
            border = new RectOffset(bb, bb, bb, bb)
        };

        sBtn.normal.background = buttonTexture;
        sBtn.hover.background = buttonHoverTexture;
        sBtn.active.background = buttonHoverTexture;
        sBtn.focused.background = buttonTexture;

        sBtn.normal.textColor = Color.white;
        sBtn.hover.textColor = Color.white;
        sBtn.active.textColor = Color.white;
        sBtn.focused.textColor = Color.white;
    }

    void DrawPanel(Rect r, Texture2D texture)
    {
        // snap to whole pixels so the edges stay crisp
        r = new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));
        GUI.color = Color.white;
        GUI.Box(r, GUIContent.none, texture == cardTexture ? cardStyle : panelStyle);
    }

    void Lbl(Rect r, string t, GUIStyle st, Color c)
    {
        float o = Mathf.Max(1f, styleScale * 1.2f);
        st.normal.textColor = new Color(0f, 0f, 0f, .45f * c.a);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), t, st);
        st.normal.textColor = c;
        GUI.Label(r, t, st);
    }

    void ApplyBallTheme()
    {
        Theme.Apply("#1b2433", Store.Current.baseHex, "#e63b2e");
        view.Layout();
    }

    void OnGUI()
    {
        float s = Mathf.Min(Screen.width / Cfg.WW, Screen.height / 600f);   // screen px per GUI unit
        if (sBig == null || Mathf.Abs(s - styleScale) > .001f) BuildStyles(s);
        float H = Screen.height / s;                                         // GUI height in units
        float cx = Screen.width * .5f;
        float left = cx - Cfg.WW * .5f * s;
        float right = cx + Cfg.WW * .5f * s;
        float top = Screen.height - (Screen.safeArea.y + Screen.safeArea.height);
        Color soft = Theme.ink; soft.a = .8f;

        if (mode == Mode.Play || mode == Mode.Pause || mode == Mode.Over)
        {
            Lbl(new Rect(cx - 150 * s, top + 8 * s, 300 * s, 80 * s), score.ToString(), sBig, Theme.ink);
            Lbl(new Rect(cx - 150 * s, top + 80 * s, 300 * s, 24 * s), "BEST " + Mathf.Max(best, score), sMid, soft);

            float x0 = right - 16 * s - (3 * 18 + 2 * 6) * s;
            for (int i = 0; i < 3; i++)
            {
                Color c = i < lives ? Theme.ball : new Color(1f, 1f, 1f, .3f);
                GUI.DrawTexture(new Rect(x0 + i * 24 * s, top + 26 * s, 18 * s, 18 * s), Art.CircleTex,
                                ScaleMode.StretchToFill, true, 0f, c, 0f, 0f);
            }

            GUI.DrawTexture(new Rect(left + 16 * s, top + 28 * s, 16 * s, 16 * s), Art.CircleTex,
                            ScaleMode.StretchToFill, true, 0f, gold, 0f, 0f);
            Lbl(new Rect(left + 38 * s, top + 24 * s, 100 * s, 24 * s), Store.coins.ToString(), sLeft, Theme.ink);
        }

        if (mode == Mode.Play)
        {
            Rect pauseRect = new Rect(
                right - 58f * s,
                top + 20f * s,
                38f * s,
                38f * s
            );

            if (GUI.Button(pauseRect, GUIContent.none, GUIStyle.none))
            {
                mode = Mode.Pause;
                Audio.Play(Snd.Click);
            }

            // Draw pause icon
            GUI.color = Theme.ink;

            GUI.DrawTexture(
                new Rect(pauseRect.x + 11f * s, pauseRect.y + 9f * s,
                         5f * s, 20f * s),
                Texture2D.whiteTexture
            );

            GUI.DrawTexture(
                new Rect(pauseRect.x + 22f * s, pauseRect.y + 9f * s,
                         5f * s, 20f * s),
                Texture2D.whiteTexture
            );

            GUI.color = Color.white;
        }

        // small hint, only before the player touches the screen
        if (mode == Mode.Play && !hintSeen && phase == Phase.Ready)
            Lbl(new Rect(cx - 150 * s, (H - 34) * s, 300 * s, 18 * s), "SWIPE UP TO SHOOT", sTiny, soft);

        Fx.Gui(s, top);

        if (mode == Mode.Home)
        {
            DrawHome(s, cx, top, H);
            return;
        }

        if (mode == Mode.Over) DrawOver(s, cx, top, H, soft);
        if (mode == Mode.Shop) DrawShop(s, left, cx, top, H, soft);
        if (mode == Mode.Pause)
            DrawPause(s, cx, top, H);
    }

    void DrawOver(float s, float cx, float top, float H, Color soft)
    {
        float panelW = 310f * s;
        float panelH = 440f * s;

        float px = cx - panelW * 0.5f;
        float py = top + H * 0.5f * s - panelH * 0.5f;

        DrawPanel(
            new Rect(px, py, panelW, panelH),
            panelTexture
        );

        Lbl(
            new Rect(px, py + 18 * s, panelW, 35 * s),
            "GAME OVER",
            sTitle,
            soft
        );

        Lbl(
            new Rect(px, py + 55 * s, panelW, 105 * s),
            score.ToString(),
            sHuge,
            Theme.ink
        );

        if (newBest)
        {
            Lbl(
                new Rect(px, py + 160 * s, panelW, 28 * s),
                "NEW PERSONAL BEST!",
                sMid,
                gold
            );
        }

        Lbl(
            new Rect(px, py + 195 * s, panelW, 25 * s),
            "BEST SCORE   " + best,
            sMid,
            soft
        );

        Lbl(
            new Rect(px + 10 * s, py + 230 * s, panelW - 20 * s, 25 * s),
            "SWISHES  " + swishes + "     RIM SHOTS  " + rimIns,
            sTiny,
            soft
        );

        Lbl(
            new Rect(px, py + 258 * s, panelW, 28 * s),
            "+" + earned + " COINS",
            sMid,
            gold
        );

        if (GUI.Button(
            new Rect(cx - 115 * s, py + 300 * s, 230 * s, 48 * s),
            "PLAY AGAIN",
            sBtn))
        {
            StartGame();
        }

        if (GUI.Button(
            new Rect(cx - 115 * s, py + 356 * s, 230 * s, 38 * s),
            "BALL COLLECTION",
            sBtn))
        {
            shopReturn = Mode.Over;
            shopMsg = "";
            mode = Mode.Shop;
        }

        if (GUI.Button(
            new Rect(cx - 115 * s, py + 401 * s, 230 * s, 30 * s),
            "HOME",
            sBtn))
        {
            mode = Mode.Home;
            ResetBall();
        }

        GUI.backgroundColor = Color.white;
    }

    void DrawShop(float s, float left, float cx, float top, float H, Color soft)
    {
        float panelW = 330f * s;
        float panelH = H * s - 24 * s;

        float px = cx - panelW * 0.5f;
        float py = top + 12 * s;

        DrawPanel(
            new Rect(px, py, panelW, panelH),
            panelTexture
        );

        Lbl(
            new Rect(px, py + 12 * s, panelW, 42 * s),
            "BALL COLLECTION",
            sTitle,
            Theme.ink
        );

        Lbl(
            new Rect(px, py + 54 * s, panelW, 25 * s),
            Store.coins + " COINS",
            sMid,
            gold
        );

        const int COLS = 3;

        float pad = 18 * s;
        float gap = 10 * s;
        float cw = (panelW - 2 * pad - (COLS - 1) * gap) / COLS;
        float ch = 88 * s;

        for (int i = 0; i < Store.All.Length; i++)
        {
            Skin sk = Store.All[i];

            int row = i / COLS;
            int col = i % COLS;

            Rect r = new Rect(
                px + pad + col * (cw + gap),
                py + 88 * s + row * (ch + 9 * s),
                cw,
                ch
            );

            bool owned = Store.Owned(i);
            bool sel = Store.selected == i;

            if (sel)
            {
                // rounded gold outline behind the selected card
                GUI.color = Color.white;
                GUI.Box(
                    new Rect(r.x - 2 * s, r.y - 2 * s,
                             r.width + 4 * s, r.height + 4 * s),
                    GUIContent.none,
                    selStyle
                );
            }

            GUI.color = Color.white;
            DrawPanel(r, cardTexture);

            GUI.color = owned
                ? Color.white
                : new Color(.35f, .35f, .35f, 1f);

            GUI.DrawTexture(
                new Rect(r.x + (cw - 45 * s) * 0.5f,
                        r.y + 5 * s,
                        45 * s,
                        45 * s),
                Art.BallFor(sk).texture,
                ScaleMode.StretchToFill,
                true
            );

            GUI.color = Color.white;

            Lbl(
                new Rect(r.x, r.y + 52 * s, cw, 16 * s),
                sk.name.ToUpper(),
                sTiny,
                soft
            );

            string status = owned
                ? (sel ? "EQUIPPED" : "OWNED")
                : sk.price + " COINS";

            Lbl(
                new Rect(r.x, r.y + 69 * s, cw, 15 * s),
                status,
                sTiny,
                sel ? gold : soft
            );

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                OnShopTap(i);
        }

        if (Time.time < shopMsgT)
        {
            Lbl(
                new Rect(px, py + panelH - 82 * s, panelW, 25 * s),
                shopMsg,
                sMid,
                gold
            );
        }

        if (GUI.Button(
            new Rect(cx - 90 * s, py + panelH - 48 * s, 180 * s, 38 * s),
            "BACK",
            sBtn))
        {
            mode = shopReturn;
        }

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
    }

    void OnShopTap(int i)
    {
        if (Store.Owned(i))
        {
            Store.Select(i); ApplyBallTheme(); Audio.Play(Snd.Click);
        }
        else if (Store.Buy(i))
        {
            ApplyBallTheme(); Audio.Play(Snd.Score2);
            shopMsg = "UNLOCKED " + Store.All[i].name.ToUpper(); shopMsgT = Time.time + 1.5f;
        }
        else
        {
            Audio.Play(Snd.Miss);
            shopMsg = "NOT ENOUGH COINS"; shopMsgT = Time.time + 1.5f;
        }
    }

    void DrawHome(float s, float cx, float top, float H)
    {
        float panelW = 310f * s;
        float panelH = 420f * s;

        float panelX = cx - panelW * 0.5f;
        float panelY = top + H * 0.30f * s;

        DrawPanel(
            new Rect(panelX, panelY, panelW, panelH),
            panelTexture
        );

        Lbl(
            new Rect(panelX + 15 * s, panelY + 28 * s,
                    panelW - 30 * s, 75 * s),
            "SWISH",
            sBig,
            Theme.ink
        );

        Lbl(
            new Rect(panelX, panelY + 91 * s, panelW, 28 * s),
            "TAKE YOUR SHOT",
            sMid,
            gold
        );

        GUI.backgroundColor = Color.white;

        if (GUI.Button(
            new Rect(cx - 115 * s, panelY + 145 * s, 230 * s, 54 * s),
            "PLAY",
            sBtn))
        {
            StartGame();
            Audio.Play(Snd.Click);
        }

        if (GUI.Button(
            new Rect(cx - 115 * s, panelY + 210 * s, 230 * s, 46 * s),
            "BALL COLLECTION",
            sBtn))
        {
            shopReturn = Mode.Home;
            shopMsg = "";
            mode = Mode.Shop;
            Audio.Play(Snd.Click);
        }

        string musicLabel = SwishMusic.IsOn
            ? "MUSIC  ON"
            : "MUSIC  OFF";

        if (GUI.Button(
            new Rect(cx - 115 * s, panelY + 268 * s, 230 * s, 46 * s),
            musicLabel,
            sBtn))
        {
            SwishMusic.Toggle();
            Audio.Play(Snd.Click);
        }

        Lbl(
            new Rect(panelX, panelY + 350 * s, panelW, 30 * s),
            "BEST SCORE   " + best,
            sMid,
            Theme.ink
        );

        GUI.backgroundColor = Color.white;
    }

    void DrawPause(float s, float cx, float top, float H)
    {
        float panelW = 300f * s;
        float panelH = 245f * s;

        float px = cx - panelW * 0.5f;
        float py = top + H * 0.5f * s - panelH * 0.5f;

        DrawPanel(
            new Rect(px, py, panelW, panelH),
            panelTexture
        );

        Lbl(
            new Rect(px, py + 18 * s, panelW, 45 * s),
            "PAUSED",
            sTitle,
            Theme.ink
        );

        if (GUI.Button(
            new Rect(cx - 110 * s, py + 80 * s, 220 * s, 48 * s),
            "RESUME",
            sBtn))
        {
            mode = Mode.Play;
        }

        if (GUI.Button(
            new Rect(cx - 110 * s, py + 138 * s, 220 * s, 42 * s),
            "BALL COLLECTION",
            sBtn))
        {
            shopReturn = Mode.Pause;
            shopMsg = "";
            mode = Mode.Shop;
        }

        if (GUI.Button(
            new Rect(cx - 110 * s, py + 188 * s, 220 * s, 42 * s),
            "HOME",
            sBtn))
        {
            mode = Mode.Home;
            ResetBall();
        }

        GUI.backgroundColor = Color.white;
    }
}