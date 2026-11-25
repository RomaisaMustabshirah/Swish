// Swish hand mode: shoot by flicking your index finger, like swiping on a phone.
//   Point your index finger  -> an orange dot follows your fingertip on the game
//   Flick it up               -> shoot. Longer flick = more power, tilt = angle
// Everything runs in the browser. The camera feed is never uploaded.

import { FilesetResolver, HandLandmarker } from "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14";

const WASM_URL = "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm";
const MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task";
const GAME_OBJECT = "Swish Street";   // created in SwishGame.Boot()

// ---- tuning (change these, no Unity rebuild needed) ----
export const TUNING = {
  SHOT_GAIN: 0.7,      // flick length -> shot power. Shots too weak? raise it. Too strong? lower it.
  MIN_RISE: 0.10,      // smallest upward flick that counts (fraction of camera height)
  START_BELOW: 0.35,   // a flick must start below this height in the camera (0 = top, 1 = bottom)
  MAX_TIME: 0.6,       // seconds: a flick longer than this ends and shoots
  COOLDOWN: 0.8,       // seconds between shots
  STILL: 0.008,        // movement smaller than this per frame = the finger has stopped
  CURSOR_GAIN: 1.6,    // how far the dot moves for a hand movement
};

const INDEX_TIP = 8, INDEX_PIP = 6, WRIST = 0;
const CONNECTIONS = [
  [0, 1], [1, 2], [2, 3], [3, 4], [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12], [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [17, 18], [18, 19], [19, 20], [0, 17],
];

const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);

/** True when the index finger is straight (pointing). */
export function isPointing(pts) {
  const base = dist(pts[WRIST], pts[INDEX_PIP]);
  return base > 1e-6 && dist(pts[WRIST], pts[INDEX_TIP]) / base > 1.05;
}

/**
 * Watches the index fingertip and reports a flick.
 * update() takes 21 [x, y] camera points (unmirrored), returns null or { dx, dy }:
 * the flick from its lowest point to where it stopped, mirrored, in camera-height units, times SHOT_GAIN.
 */
export class FlickTracker {
  constructor(tuning = TUNING) { this.t = tuning; this.lastShot = -1e9; this.reset(); }

  reset() { this.hist = []; this.tip = null; this.pointing = false; }

  _flick() {
    // The lowest point in the recent path is where the flick started.
    let start = this.hist[0];
    for (const p of this.hist) if (p.y > start.y) start = p;
    const end = this.hist[this.hist.length - 1];
    return { start, end, rise: start.y - end.y };
  }

  _fire(now, start, end) {
    this.lastShot = now;
    this.hist = [];
    return { dx: (end.x - start.x) * this.t.SHOT_GAIN, dy: (end.y - start.y) * this.t.SHOT_GAIN };
  }

  update(now, pts, w, h) {
    // Hand lost: a fast flick often blurs out of view, so finish a flick already under way.
    if (!pts) {
      let shot = null;
      if (this.hist.length >= 2 && now - this.lastShot >= this.t.COOLDOWN) {
        const { start, end, rise } = this._flick();
        if (rise >= this.t.MIN_RISE && start.y >= this.t.START_BELOW) shot = this._fire(now, start, end);
      }
      this.reset();
      return shot;
    }

    this.pointing = isPointing(pts);
    // Mirrored (like a mirror) and measured in camera-height units so x and y match.
    const tip = { t: now, x: (w - pts[INDEX_TIP][0]) / h, y: pts[INDEX_TIP][1] / h };
    this.tip = { x: tip.x * h / w, y: tip.y };          // 0..1 across the camera, for the cursor
    if (!this.pointing) { this.hist = []; return null; }

    this.hist.push(tip);
    while (this.hist.length && now - this.hist[0].t > this.t.MAX_TIME + 0.3) this.hist.shift();
    if (now - this.lastShot < this.t.COOLDOWN || this.hist.length < 2) return null;

    const { start, end, rise } = this._flick();
    if (rise < this.t.MIN_RISE || start.y < this.t.START_BELOW) return null;

    const prev = this.hist[this.hist.length - 2];
    const stillRising = prev.y - end.y > this.t.STILL;
    if (!stillRising || now - start.t > this.t.MAX_TIME) return this._fire(now, start, end);
    return null;
  }
}

// ---------- page UI ----------
function injectUI() {
  const css = document.createElement("style");
  css.textContent = `
    #hand-btn { position: fixed; top: 16px; right: 16px; z-index: 20; padding: 10px 16px;
      border-radius: 999px; border: 0; cursor: pointer; font: 600 14px Arial, sans-serif;
      background: #ff914d; color: #111; box-shadow: 0 4px 14px rgba(0,0,0,.35); }
    #hand-btn.on { background: #222; color: #ff914d; outline: 2px solid #ff914d; }
    #hand-panel { position: fixed; left: 16px; bottom: 16px; z-index: 20; width: 220px;
      border-radius: 14px; overflow: hidden; background: #151515; color: #ddd;
      font: 12px Arial, sans-serif; box-shadow: 0 4px 14px rgba(0,0,0,.4); display: none; }
    #hand-panel canvas { display: block; width: 220px; height: 150px; }
    #hand-panel div { padding: 8px 10px; line-height: 1.5; }
    #hand-panel b { color: #ff914d; }
    #hand-cursor { position: fixed; z-index: 19; width: 22px; height: 22px; margin: -11px 0 0 -11px;
      border-radius: 50%; background: rgba(255,145,77,.9); border: 2px solid #fff;
      pointer-events: none; display: none; transition: opacity .15s; }
    #hand-cursor.idle { opacity: .35; }
    #hand-cursor.shot { animation: shot .35s ease-out; }
    @keyframes shot { from { transform: scale(2.2); box-shadow: 0 0 0 6px rgba(255,145,77,.5); } to { transform: scale(1); } }
  `;
  document.head.appendChild(css);

  const btn = document.createElement("button");
  btn.id = "hand-btn"; btn.textContent = "🖐 Play with hands";
  const panel = document.createElement("div");
  panel.id = "hand-panel";
  panel.innerHTML = `<canvas width="220" height="150"></canvas>
    <div><b>☝ Point</b> at the ball, then <b>flick up</b> to shoot<br><span id="hand-status">Starting…</span></div>`;
  const cursor = document.createElement("div");
  cursor.id = "hand-cursor";
  document.body.append(btn, panel, cursor);
  return { btn, panel, cursor, preview: panel.querySelector("canvas"), status: panel.querySelector("#hand-status") };
}

function sendFlick(shot) {
  const game = window.swishUnity;
  if (game && shot) game.SendMessage(GAME_OBJECT, "HandFlick", `${shot.dx.toFixed(4)},${shot.dy.toFixed(4)}`);
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
  const ui = injectUI();
  const video = document.createElement("video");
  video.playsInline = true; video.muted = true;
  const pctx = ui.preview.getContext("2d");
  const tracker = new FlickTracker();
  let landmarker = null, stream = null, running = false, lastTime = -1;

  function drawPreview(pts, w, h) {
    pctx.fillStyle = "#151515";
    pctx.fillRect(0, 0, 220, 150);
    if (!pts) return;
    const s = Math.min(220 / w, 150 / h);
    const ox = (220 - w * s) / 2, oy = (150 - h * s) / 2;
    const P = pts.map(([x, y]) => [ox + (w - x) * s, oy + y * s]);
    pctx.strokeStyle = tracker.pointing ? "#ff914d" : "#888";
    pctx.lineWidth = 2;
    pctx.beginPath();
    for (const [a, b] of CONNECTIONS) { pctx.moveTo(...P[a]); pctx.lineTo(...P[b]); }
    pctx.stroke();
    pctx.fillStyle = "#fff";
    pctx.beginPath(); pctx.arc(P[INDEX_TIP][0], P[INDEX_TIP][1], 4, 0, Math.PI * 2); pctx.fill();
  }

  function placeCursor(visible) {
    const canvas = document.querySelector("#unity-canvas");
    if (!canvas || !visible || !tracker.tip) { ui.cursor.style.display = "none"; return; }
    const r = canvas.getBoundingClientRect();
    const g = TUNING.CURSOR_GAIN;
    const x = Math.min(1, Math.max(0, 0.5 + (tracker.tip.x - 0.5) * g));
    const y = Math.min(1, Math.max(0, 0.5 + (tracker.tip.y - 0.5) * g));
    ui.cursor.style.display = "block";
    ui.cursor.style.left = r.left + x * r.width + "px";
    ui.cursor.style.top = r.top + y * r.height + "px";
    ui.cursor.classList.toggle("idle", !tracker.pointing);
  }

  function flash() {
    ui.cursor.classList.remove("shot");
    void ui.cursor.offsetWidth;            // restart the animation
    ui.cursor.classList.add("shot");
  }

  function loop(now) {
    if (!running) return;
    if (video.readyState >= 2 && video.currentTime !== lastTime) {
      lastTime = video.currentTime;
      const w = video.videoWidth, h = video.videoHeight;
      const res = landmarker.detectForVideo(video, now);
      const lms = res.landmarks && res.landmarks[0];
      const pts = lms ? lms.map((p) => [p.x * w, p.y * h]) : null;
      const shot = tracker.update(now / 1000, pts, w, h);
      if (shot) { sendFlick(shot); flash(); }
      drawPreview(pts, w, h);
      placeCursor(!!pts);
      ui.status.textContent = shot ? "Shot! 🏀"
        : !pts ? "Show your hand to the camera"
        : tracker.pointing ? "Ready: flick up" : "Point your index finger";
    }
    requestAnimationFrame(loop);
  }

  async function turnOn() {
    ui.btn.disabled = true;
    ui.btn.textContent = "Starting camera…";
    try {
      stream = await navigator.mediaDevices.getUserMedia({
        video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: "user" }, audio: false,
      });
      video.srcObject = stream;
      await video.play();
    } catch (err) {
      ui.btn.disabled = false;
      ui.btn.textContent = err && err.name === "NotAllowedError"
        ? "Camera blocked: allow it in the address bar" : "No camera found";
      return;
    }
    if (!landmarker) {
      ui.btn.textContent = "Loading hand model…";
      try { landmarker = await createLandmarker(); }
      catch (err) {
        console.error(err);
        ui.btn.disabled = false; ui.btn.textContent = "Couldn't load hand model: retry";
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
    }
    running = true; lastTime = -1; tracker.reset();
    ui.btn.disabled = false; ui.btn.textContent = "✋ Hands on: click to stop"; ui.btn.classList.add("on");
    ui.panel.style.display = "block";
    requestAnimationFrame(loop);
  }

  function turnOff() {
    running = false;
    if (stream) stream.getTracks().forEach((t) => t.stop());
    tracker.reset();
    ui.panel.style.display = "none"; ui.cursor.style.display = "none";
    ui.btn.textContent = "🖐 Play with hands"; ui.btn.classList.remove("on");
  }

  ui.btn.addEventListener("click", () => (running ? turnOff() : turnOn()));
}

if (typeof document !== "undefined") start();
