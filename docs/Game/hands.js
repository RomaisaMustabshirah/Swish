// Swish hand mode: play completely touch-free through the webcam.
// Your pinch (thumb + index finger together) works exactly like a finger on the screen:
//   Pinch           -> touch (press a button, or grab the ball)
//   Move, pinched   -> drag (aim: the arc shows your shot)
//   Open the pinch  -> lift your finger (shoot)
// Everything runs in the browser. The camera feed is never uploaded.

import { FilesetResolver, HandLandmarker } from "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14";

const WASM_URL = "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm";
const MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task";
const GAME_OBJECT = "Swish Street";   // created in SwishGame.Boot()

// ---- tuning (change these, no Unity rebuild needed) ----
export const TUNING = {
  CURSOR_GAIN: 1.5,     // how far the cursor moves for a hand movement (bigger = less arm movement)
  SMOOTH: 0.5,          // 0..1, higher = cursor follows the hand more tightly, lower = steadier
  PINCH_ON: 0.32,       // thumb-index gap (÷ palm size) below this = pinched
  PINCH_OFF: 0.48,      // gap above this = released (the gap between the two stops flicker)
  RELEASE_FRAMES: 2,    // frames the pinch must stay open before it counts as released
};

const WRIST = 0, THUMB_TIP = 4, INDEX_TIP = 8, MIDDLE_MCP = 9;
const CONNECTIONS = [
  [0, 1], [1, 2], [2, 3], [3, 4], [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12], [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [17, 18], [18, 19], [19, 20], [0, 17],
];
const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
const clamp01 = (v) => Math.min(1, Math.max(0, v));

/** Thumb-index gap divided by palm size, so it works near or far from the camera. */
export function pinchGap(pts) {
  const palm = dist(pts[WRIST], pts[MIDDLE_MCP]);
  return palm > 1e-6 ? dist(pts[THUMB_TIP], pts[INDEX_TIP]) / palm : 9;
}

/**
 * Turns hand frames into game messages "pinch,x,y" (x, y = 0..1 across the game, y from the top).
 * The cursor is the point between thumb and index fingertips, mirrored like a mirror.
 */
export class PinchTracker {
  constructor(tuning = TUNING) { this.t = tuning; this.reset(); }
  reset() { this.pinch = false; this.openCount = 0; this.pos = null; this.heldPos = null; this.visible = false; }

  update(pts, w, h) {
    if (!pts) {
      const was = this.visible;
      this.reset();
      return was ? "off" : null;
    }
    this.visible = true;

    const gap = pinchGap(pts);
    let released = false;
    if (this.pinch) {
      if (gap > this.t.PINCH_OFF) {
        if (++this.openCount >= this.t.RELEASE_FRAMES) { this.pinch = false; this.openCount = 0; released = true; }
      } else this.openCount = 0;
    } else if (gap < this.t.PINCH_ON) {
      this.pinch = true; this.openCount = 0;
    }

    const mid = [(pts[THUMB_TIP][0] + pts[INDEX_TIP][0]) / 2, (pts[THUMB_TIP][1] + pts[INDEX_TIP][1]) / 2];
    const mx = 1 - mid[0] / w, my = mid[1] / h;
    const target = [clamp01(0.5 + (mx - 0.5) * this.t.CURSOR_GAIN), clamp01(0.5 + (my - 0.5) * this.t.CURSOR_GAIN)];
    const k = this.t.SMOOTH;
    this.pos = this.pos ? [this.pos[0] + (target[0] - this.pos[0]) * k, this.pos[1] + (target[1] - this.pos[1]) * k] : target;

    // Fingers drift while opening, so the shot uses where the pinch was, not where the opening hand went.
    let send = this.pos;
    if (this.pinch && this.openCount === 0) this.heldPos = this.pos;
    else if ((this.pinch || released) && this.heldPos) send = this.heldPos;

    return `${this.pinch ? 1 : 0},${send[0].toFixed(4)},${send[1].toFixed(4)}`;
  }
}

/** Phones and tablets: hand mode still works, with a compact layout. */
export function isTouchDevice() {
  if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) return true;
  const coarse = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;
  return coarse && Math.min(screen.width, screen.height) < 820;
}

// ---------- page UI ----------
function injectUI(phone) {
  const css = document.createElement("style");
  css.textContent = `
    #hand-btn { position: fixed; top: 12px; right: 12px; z-index: 20; padding: 10px 16px;
      border-radius: 999px; border: 0; cursor: pointer; font: 600 14px Arial, sans-serif;
      background: #ff914d; color: #111; box-shadow: 0 4px 14px rgba(0,0,0,.35); }
    #hand-btn.on { background: #222; color: #ff914d; outline: 2px solid #ff914d; }
    #hand-btn.phone { padding: 8px 11px; font-size: 18px; }
    #hand-panel { position: fixed; left: 12px; bottom: 12px; z-index: 20; width: 220px;
      border-radius: 14px; overflow: hidden; background: #151515; color: #ddd;
      font: 12px Arial, sans-serif; box-shadow: 0 4px 14px rgba(0,0,0,.4); display: none; }
    #hand-panel canvas { display: block; width: 220px; height: 150px; }
    #hand-panel .hp-body { padding: 8px 10px; line-height: 1.5; }
    #hand-panel b { color: #ff914d; }
    #hand-panel .hp-head { display: flex; justify-content: space-between; align-items: center; gap: 12px;
      padding: 6px 10px; background: #222; cursor: pointer; user-select: none; font-weight: 600; }
    #hand-panel .hp-head span { color: #ff914d; font-size: 14px; }
    #hand-panel.mini { width: auto; }
    #hand-panel.mini canvas, #hand-panel.mini .hp-body { display: none; }
    @media (max-width: 1100px), (max-height: 700px) {
      #hand-panel { width: 160px; } #hand-panel canvas { width: 160px; height: 109px; }
    }
    #hand-cursor { position: fixed; z-index: 21; width: 26px; height: 26px; margin: -13px 0 0 -13px;
      border-radius: 50%; border: 3px solid #fff; background: rgba(255,145,77,.35);
      box-shadow: 0 0 0 2px rgba(0,0,0,.35); pointer-events: none; display: none;
      transition: transform .08s, background .08s; }
    #hand-cursor.pinch { transform: scale(.7); background: #ff914d; }
    #hand-howto { position: fixed; inset: 0; z-index: 30; display: none; place-items: center;
      background: rgba(0,0,0,.6); font: 15px Arial, sans-serif; color: #eee; }
    #hand-howto .card { width: min(380px, calc(100vw - 32px)); padding: 22px; border-radius: 16px; background: #1b1b1b;
      box-shadow: 0 8px 30px rgba(0,0,0,.5); }
    #hand-howto h3 { color: #ff914d; font-size: 20px; margin-bottom: 12px; }
    #hand-howto li { list-style: none; padding: 6px 0; border-top: 1px solid #333; }
    #hand-howto li:first-child { border-top: 0; }
    #hand-howto p { margin-top: 12px; color: #aaa; font-size: 13px; }
    #hand-howto button { margin-top: 14px; width: 100%; padding: 10px; border: 0; border-radius: 10px;
      background: #ff914d; color: #111; font: 600 15px Arial, sans-serif; cursor: pointer; }
  `;
  document.head.appendChild(css);

  const btn = document.createElement("button");
  btn.id = "hand-btn";
  if (phone) btn.classList.add("phone");

  const panel = document.createElement("div");
  panel.id = "hand-panel";
  panel.innerHTML = `<div class="hp-head" title="Show / hide the camera panel">✋ Hand mode <span>–</span></div>
    <canvas width="220" height="150"></canvas>
    <div class="hp-body"><b>Pinch</b> = touch · <b>move</b> = drag · <b>open</b> = release<br><span id="hand-status">Starting…</span></div>`;
  const head = panel.querySelector(".hp-head");
  const setMini = (mini) => {
    panel.classList.toggle("mini", mini);
    head.querySelector("span").textContent = mini ? "+" : "–";
  };
  head.addEventListener("click", () => setMini(!panel.classList.contains("mini")));

  const cursor = document.createElement("div");
  cursor.id = "hand-cursor";

  const howto = document.createElement("div");
  howto.id = "hand-howto";
  howto.innerHTML = `<div class="card"><h3>✋ Hand mode</h3><ul>
      <li>🤏 <b>Pinch</b> thumb + index finger = <b>touch</b></li>
      <li>↕ <b>Move</b> while pinched = <b>drag</b>. The arc shows your shot</li>
      <li>🖐 <b>Open</b> your fingers = <b>let go</b>, and the ball flies</li>
      <li>🔘 Pinch on any button to press it: play, pause, shop</li></ul>
      <p>${phone ? "Prop your phone up so it stands on its own, then play with your hand in front of the camera."
                 : "Sit about an arm's length from the screen, in good light."}</p>
      <button>Got it</button></div>`;
  howto.querySelector("button").addEventListener("click", () => { howto.style.display = "none"; });

  document.body.append(btn, panel, cursor, howto);
  return { btn, panel, cursor, howto, setMini, preview: panel.querySelector("canvas"), status: panel.querySelector("#hand-status") };
}

function sendToGame(method, msg) {
  const game = window.swishUnity;
  if (game && msg !== null) game.SendMessage(GAME_OBJECT, method, msg);
}

async function createLandmarker() {
  const files = await FilesetResolver.forVisionTasks(WASM_URL);
  const opts = (delegate) => ({
    baseOptions: { modelAssetPath: MODEL_URL, delegate },
    runningMode: "VIDEO", numHands: 1,
    minHandDetectionConfidence: 0.5, minHandPresenceConfidence: 0.5, minTrackingConfidence: 0.5,
  });
  try { return await HandLandmarker.createFromOptions(files, opts("GPU")); }
  catch { return await HandLandmarker.createFromOptions(files, opts("CPU")); }
}

function start() {
  const phone = isTouchDevice();
  const ui = injectUI(phone);
  const OFF_LABEL = phone ? "🖐" : "🖐 Play with hands";
  const ON_LABEL = phone ? "✋ ✕" : "✋ Hands on: click to stop";
  ui.btn.textContent = OFF_LABEL;

  const video = document.createElement("video");
  video.playsInline = true; video.muted = true;
  const pctx = ui.preview.getContext("2d");
  const tracker = new PinchTracker();
  let landmarker = null, stream = null, running = false, lastTime = -1;

  function drawPreview(pts, w, h) {
    const cw = ui.preview.width, ch = ui.preview.height;
    pctx.fillStyle = "#151515";
    pctx.fillRect(0, 0, cw, ch);
    if (!pts) return;
    const s = Math.min(cw / w, ch / h);
    const ox = (cw - w * s) / 2, oy = (ch - h * s) / 2;
    const P = pts.map(([x, y]) => [ox + (w - x) * s, oy + y * s]);
    pctx.strokeStyle = tracker.pinch ? "#ff914d" : "#aaa";
    pctx.lineWidth = 2;
    pctx.beginPath();
    for (const [a, b] of CONNECTIONS) { pctx.moveTo(...P[a]); pctx.lineTo(...P[b]); }
    pctx.stroke();
    pctx.fillStyle = "#ff914d";
    for (const i of [THUMB_TIP, INDEX_TIP]) { pctx.beginPath(); pctx.arc(P[i][0], P[i][1], 4, 0, Math.PI * 2); pctx.fill(); }
  }

  function placeCursor() {
    const canvas = document.querySelector("#unity-canvas");
    if (!canvas || !tracker.pos) { ui.cursor.style.display = "none"; return; }
    const r = canvas.getBoundingClientRect();
    const p = tracker.pinch && tracker.heldPos ? tracker.heldPos : tracker.pos;
    ui.cursor.style.display = "block";
    ui.cursor.style.left = r.left + p[0] * r.width + "px";
    ui.cursor.style.top = r.top + p[1] * r.height + "px";
    ui.cursor.classList.toggle("pinch", tracker.pinch);
  }

  function loop(now) {
    if (!running) return;
    if (video.readyState >= 2 && video.currentTime !== lastTime) {
      lastTime = video.currentTime;
      const w = video.videoWidth, h = video.videoHeight;
      const res = landmarker.detectForVideo(video, now);
      const lms = res.landmarks && res.landmarks[0];
      const pts = lms ? lms.map((p) => [p.x * w, p.y * h]) : null;
      sendToGame("HandInput", tracker.update(pts, w, h));
      drawPreview(pts, w, h);
      placeCursor();
      ui.status.textContent = !pts ? "Show your hand to the camera" : tracker.pinch ? "Pinched 🤏" : "Hand found";
    }
    requestAnimationFrame(loop);
  }

  async function turnOn() {
    ui.btn.disabled = true;
    ui.btn.textContent = phone ? "…" : "Starting camera…";
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        video: { width: { ideal: 480 }, height: { ideal: 360 }, facingMode: "user" }, audio: false,
      });
      video.srcObject = stream;
      await video.play();
    } catch (err) {
      ui.btn.disabled = false;
      ui.btn.textContent = OFF_LABEL;
      alert(err && err.name === "NotAllowedError"
        ? "Camera access was blocked. Allow the camera in your browser's address bar, then try again."
        : "No camera found, or another app is using it.");
      return;
    }
    if (!landmarker) {
      ui.btn.textContent = phone ? "…" : "Loading hand model…";
      try { landmarker = await createLandmarker(); }
      catch (err) {
        console.error(err);
        ui.btn.disabled = false; ui.btn.textContent = OFF_LABEL;
        stream.getTracks().forEach((t) => t.stop());
        alert("Couldn't load the hand model. Check your internet connection and try again.");
        return;
      }
    }
    running = true; lastTime = -1; tracker.reset();
    sendToGame("SetHandMode", "1");
    ui.btn.disabled = false; ui.btn.textContent = ON_LABEL; ui.btn.classList.add("on");
    ui.panel.style.display = "block";
    ui.setMini(phone);                                    // phones start with the panel folded away

    let seen = false;
    try { seen = localStorage.getItem("swish-hands-howto") === "1"; localStorage.setItem("swish-hands-howto", "1"); } catch { /* ignore */ }
    if (!seen) ui.howto.style.display = "grid";
    requestAnimationFrame(loop);
  }

  function turnOff() {
    running = false;
    if (stream) stream.getTracks().forEach((t) => t.stop());
    sendToGame("HandInput", "off");
    sendToGame("SetHandMode", "0");
    tracker.reset();
    ui.panel.style.display = "none"; ui.cursor.style.display = "none";
    ui.btn.textContent = OFF_LABEL; ui.btn.classList.remove("on");
  }

  ui.btn.addEventListener("click", () => (running ? turnOff() : turnOn()));
}

if (typeof document !== "undefined") start();
