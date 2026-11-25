// Swish hand mode: play with your hands through the webcam.
//   Fist        -> grab the ball (like pressing the mouse)
//   Move up     -> aim and set power (like dragging)
//   Open hand   -> shoot (like releasing)
// Everything runs in the browser. The camera feed is never uploaded.

import { FilesetResolver, HandLandmarker } from "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14";

const WASM_URL = "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.14/wasm";
const MODEL_URL = "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task";
const GAME_OBJECT = "Swish Street";   // created in SwishGame.Boot()

const GAIN = 1.6;          // how far the cursor moves for a hand movement (bigger = less arm movement)
const SMOOTH = 0.55;       // 0..1, higher = follows the hand more tightly
const GRAB_BELOW = 0.98;   // average finger reach below this = fist
const OPEN_ABOVE = 1.12;   // average finger reach above this = open hand
const GRAB_FRAMES = 2;     // frames a fist must hold before it counts

const FINGERS = [[6, 8], [10, 12], [14, 16], [18, 20]];   // [PIP, TIP] for index..pinky
const CONNECTIONS = [
  [0, 1], [1, 2], [2, 3], [3, 4], [0, 5], [5, 6], [6, 7], [7, 8],
  [5, 9], [9, 10], [10, 11], [11, 12], [9, 13], [13, 14], [14, 15], [15, 16],
  [13, 17], [17, 18], [18, 19], [19, 20], [0, 17],
];

// ---------- pure helpers (exported for testing) ----------
export function fingerReach(pts) {
  const d = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]);
  let sum = 0;
  for (const [pip, tip] of FINGERS) {
    const base = d(pts[0], pts[pip]);
    sum += base > 1e-6 ? d(pts[0], pts[tip]) / base : 0;
  }
  return sum / FINGERS.length;
}

export function palmCenter(pts) {
  return [(pts[0][0] + pts[5][0] + pts[17][0]) / 3, (pts[0][1] + pts[5][1] + pts[17][1]) / 3];
}

/** Turns hand frames into game messages: "grab,x,y" with x/y in 0..1 across the game canvas. */
export class HandTracker {
  constructor() { this.reset(); }
  reset() {
    this.grab = false; this.grabCount = 0;
    this.pos = null; this.lastGrabPos = null; this.visible = false;
  }
  /** pts: 21 [x, y] points in camera pixels (unmirrored); w, h: camera size. Returns the message. */
  update(pts, w, h) {
    if (!pts) {
      const wasVisible = this.visible;
      this.reset();
      return wasVisible ? "off" : null;
    }
    this.visible = true;

    const reach = fingerReach(pts);
    let released = false;
    if (this.grab) {
      if (reach > OPEN_ABOVE) { this.grab = false; released = true; }
    } else if (reach < GRAB_BELOW) {
      if (++this.grabCount >= GRAB_FRAMES) { this.grab = true; this.grabCount = 0; }
    } else {
      this.grabCount = 0;
    }

    // Mirror so moving your hand right moves the cursor right, then amplify around the centre.
    const [cx, cy] = palmCenter(pts);
    const mx = 1 - cx / w, my = cy / h;
    const target = [
      Math.min(1, Math.max(0, 0.5 + (mx - 0.5) * GAIN)),
      Math.min(1, Math.max(0, 0.5 + (my - 0.5) * GAIN)),
    ];
    this.pos = this.pos
      ? [this.pos[0] + (target[0] - this.pos[0]) * SMOOTH, this.pos[1] + (target[1] - this.pos[1]) * SMOOTH]
      : target;

    // Opening the hand shifts the palm a little, so shoot from where the fist last was.
    let send = this.pos;
    if (this.grab) this.lastGrabPos = this.pos;
    else if (released && this.lastGrabPos) send = this.lastGrabPos;

    return `${this.grab ? 1 : 0},${send[0].toFixed(4)},${send[1].toFixed(4)}`;
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
    #hand-cursor { position: fixed; z-index: 19; width: 30px; height: 30px; margin: -15px 0 0 -15px;
      border-radius: 50%; border: 3px solid #ff914d; pointer-events: none; display: none;
      transition: transform .08s, background .08s; }
    #hand-cursor.grab { background: rgba(255,145,77,.85); transform: scale(.8); }
  `;
  document.head.appendChild(css);

  const btn = document.createElement("button");
  btn.id = "hand-btn"; btn.textContent = "🖐 Play with hands";
  const panel = document.createElement("div");
  panel.id = "hand-panel";
  panel.innerHTML = `<canvas width="220" height="150"></canvas>
    <div><b>✊ Fist</b> to grab · <b>⬆ move up</b> to aim · <b>🖐 open</b> to shoot<br><span id="hand-status">Starting…</span></div>`;
  const cursor = document.createElement("div");
  cursor.id = "hand-cursor";
  document.body.append(btn, panel, cursor);
  return { btn, panel, cursor, preview: panel.querySelector("canvas"), status: panel.querySelector("#hand-status") };
}

function send(msg) {
  const game = window.swishUnity;
  if (game && msg) game.SendMessage(GAME_OBJECT, "HandInput", msg);
}

async function createLandmarker() {
  const files = await FilesetResolver.forVisionTasks(WASM_URL);
  const opts = (delegate) => ({
    baseOptions: { modelAssetPath: MODEL_URL, delegate },
    runningMode: "VIDEO", numHands: 1,
    minHandDetectionConfidence: 0.6, minHandPresenceConfidence: 0.5, minTrackingConfidence: 0.5,
  });
  try { return await HandLandmarker.createFromOptions(files, opts("GPU")); }
  catch { return await HandLandmarker.createFromOptions(files, opts("CPU")); }
}

function start() {
  const ui = injectUI();
  const video = document.createElement("video");
  video.playsInline = true; video.muted = true;
  const pctx = ui.preview.getContext("2d");
  const tracker = new HandTracker();
  let landmarker = null, stream = null, running = false, lastTime = -1;

  function drawPreview(pts, w, h) {
    pctx.fillStyle = "#151515";
    pctx.fillRect(0, 0, 220, 150);
    if (!pts) return;
    const s = Math.min(220 / w, 150 / h);
    const ox = (220 - w * s) / 2, oy = (150 - h * s) / 2;
    const P = pts.map(([x, y]) => [ox + (w - x) * s, oy + y * s]);   // mirrored, like a mirror
    pctx.strokeStyle = tracker.grab ? "#ff914d" : "#bbb";
    pctx.lineWidth = 2;
    pctx.beginPath();
    for (const [a, b] of CONNECTIONS) { pctx.moveTo(...P[a]); pctx.lineTo(...P[b]); }
    pctx.stroke();
    pctx.fillStyle = "#ff914d";
    for (const p of P) { pctx.beginPath(); pctx.arc(p[0], p[1], 2.5, 0, Math.PI * 2); pctx.fill(); }
  }

  function placeCursor() {
    const canvas = document.querySelector("#unity-canvas");
    if (!canvas || !tracker.pos) { ui.cursor.style.display = "none"; return; }
    const r = canvas.getBoundingClientRect();
    ui.cursor.style.display = "block";
    ui.cursor.style.left = r.left + tracker.pos[0] * r.width + "px";
    ui.cursor.style.top = r.top + tracker.pos[1] * r.height + "px";
    ui.cursor.classList.toggle("grab", tracker.grab);
  }

  function loop(now) {
    if (!running) return;
    if (video.readyState >= 2 && video.currentTime !== lastTime) {
      lastTime = video.currentTime;
      const w = video.videoWidth, h = video.videoHeight;
      const res = landmarker.detectForVideo(video, now);
      const lms = res.landmarks && res.landmarks[0];
      const pts = lms ? lms.map((p) => [p.x * w, p.y * h]) : null;
      send(tracker.update(pts, w, h));
      drawPreview(pts, w, h);
      placeCursor();
      ui.status.textContent = !pts ? "Show one hand to the camera" : tracker.grab ? "Holding the ball…" : "Hand found";
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
    send("off"); tracker.reset();
    ui.panel.style.display = "none"; ui.cursor.style.display = "none";
    ui.btn.textContent = "🖐 Play with hands"; ui.btn.classList.remove("on");
  }

  ui.btn.addEventListener("click", () => (running ? turnOff() : turnOn()));
}

if (typeof document !== "undefined") start();
