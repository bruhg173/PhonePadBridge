const socketStatus = document.getElementById("socketStatus");
const padStatus = document.getElementById("padStatus");
const packetCount = document.getElementById("packetCount");
const startButton = document.getElementById("startButton");
const stopButton = document.getElementById("stopButton");
const leftDot = document.getElementById("leftDot");
const rightDot = document.getElementById("rightDot");
const leftText = document.getElementById("leftText");
const rightText = document.getElementById("rightText");
const ltBar = document.getElementById("lt");
const rtBar = document.getElementById("rt");
const buttonsEl = document.getElementById("buttons");
const deadzoneEl = document.getElementById("deadzone");
const deadzoneValue = document.getElementById("deadzoneValue");
const sendRateEl = document.getElementById("sendRate");
const accentEl = document.getElementById("accent");
const orb = document.getElementById("orb");

const buttonNames = ["A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "↑", "↓", "←", "→"];
const buttonPills = new Map();

for (const name of buttonNames) {
  const el = document.createElement("div");
  el.className = "pill";
  el.textContent = name;
  buttonsEl.appendChild(el);
  buttonPills.set(name, el);
}

let ws = null;
let running = false;
let seq = 0;
let lastSend = 0;
let raf = 0;

function setAccent(value) {
  document.documentElement.style.setProperty("--accent", value);
}

accentEl.addEventListener("input", () => setAccent(accentEl.value));

deadzoneEl.addEventListener("input", () => {
  deadzoneValue.textContent = Number(deadzoneEl.value).toFixed(2);
});

function connectSocket() {
  const scheme = location.protocol === "https:" ? "wss" : "ws";
  const url = `${scheme}://${location.host}/ws`;

  ws = new WebSocket(url);

  ws.addEventListener("open", () => {
    socketStatus.textContent = "Connected";
  });

  ws.addEventListener("close", () => {
    socketStatus.textContent = "Disconnected";
  });

  ws.addEventListener("error", () => {
    socketStatus.textContent = "Error";
  });
}

function pressed(gp, index) {
  return !!gp.buttons[index]?.pressed;
}

function value(gp, index) {
  return gp.buttons[index]?.value ?? 0;
}

function axis(gp, index) {
  return gp.axes[index] ?? 0;
}

function dz(v) {
  const d = Number(deadzoneEl.value);
  return Math.abs(v) < d ? 0 : v;
}

function clamp01(v) {
  return Math.max(0, Math.min(1, v));
}

function getFirstGamepad() {
  const pads = navigator.getGamepads ? navigator.getGamepads() : [];
  for (const gp of pads) {
    if (gp) return gp;
  }
  return null;
}

function buildPacket(gp) {
  // Standard Gamepad mapping:
  // 0 A/Cross, 1 B/Circle, 2 X/Square, 3 Y/Triangle,
  // 4 LB, 5 RB, 6 LT, 7 RT, 8 Back/Share, 9 Start/Options,
  // 10 LS, 11 RS, 12 Up, 13 Down, 14 Left, 15 Right.
  return {
    type: "state",
    seq: ++seq,
    timestamp: performance.now(),
    id: gp.id || "Gamepad",

    lx: dz(axis(gp, 0)),
    ly: -dz(axis(gp, 1)),
    rx: dz(axis(gp, 2)),
    ry: -dz(axis(gp, 3)),

    lt: clamp01(value(gp, 6)),
    rt: clamp01(value(gp, 7)),

    a: pressed(gp, 0),
    b: pressed(gp, 1),
    x: pressed(gp, 2),
    y: pressed(gp, 3),

    lb: pressed(gp, 4),
    rb: pressed(gp, 5),
    back: pressed(gp, 8),
    start: pressed(gp, 9),
    ls: pressed(gp, 10),
    rs: pressed(gp, 11),

    dpadUp: pressed(gp, 12),
    dpadDown: pressed(gp, 13),
    dpadLeft: pressed(gp, 14),
    dpadRight: pressed(gp, 15)
  };
}

function setPill(name, active) {
  const el = buttonPills.get(name);
  if (!el) return;
  el.classList.toggle("active", !!active);
}

function moveDot(dot, x, y) {
  const range = 52;
  dot.style.left = `${61 + x * range}px`;
  dot.style.top = `${61 - y * range}px`;
}

function draw(packet) {
  moveDot(leftDot, packet.lx, packet.ly);
  moveDot(rightDot, packet.rx, packet.ry);

  leftText.textContent = `${packet.lx.toFixed(2)}, ${packet.ly.toFixed(2)}`;
  rightText.textContent = `${packet.rx.toFixed(2)}, ${packet.ry.toFixed(2)}`;

  ltBar.value = packet.lt * 100;
  rtBar.value = packet.rt * 100;

  setPill("A", packet.a);
  setPill("B", packet.b);
  setPill("X", packet.x);
  setPill("Y", packet.y);
  setPill("LB", packet.lb);
  setPill("RB", packet.rb);
  setPill("Back", packet.back);
  setPill("Start", packet.start);
  setPill("LS", packet.ls);
  setPill("RS", packet.rs);
  setPill("↑", packet.dpadUp);
  setPill("↓", packet.dpadDown);
  setPill("←", packet.dpadLeft);
  setPill("→", packet.dpadRight);

  packetCount.textContent = packet.seq;
  orb.style.transform = `scale(${1 + Math.max(packet.lt, packet.rt) * 0.14})`;
}

function loop(now) {
  if (!running) return;

  const gp = getFirstGamepad();

  if (!gp) {
    padStatus.textContent = "Waiting";
    raf = requestAnimationFrame(loop);
    return;
  }

  padStatus.textContent = gp.id || "Gamepad";

  const packet = buildPacket(gp);
  draw(packet);

  const hz = Number(sendRateEl.value);
  const interval = 1000 / hz;

  if (ws && ws.readyState === WebSocket.OPEN && now - lastSend >= interval) {
    ws.send(JSON.stringify(packet));
    lastSend = now;
  }

  raf = requestAnimationFrame(loop);
}

window.addEventListener("gamepadconnected", (event) => {
  padStatus.textContent = event.gamepad.id || "Gamepad connected";
});

window.addEventListener("gamepaddisconnected", () => {
  padStatus.textContent = "Disconnected";
});

startButton.addEventListener("click", () => {
  if (!navigator.getGamepads) {
    padStatus.textContent = "Gamepad API unsupported";
    return;
  }

  running = true;
  seq = 0;

  if (!ws || ws.readyState === WebSocket.CLOSED || ws.readyState === WebSocket.CLOSING) {
    connectSocket();
  }

  cancelAnimationFrame(raf);
  raf = requestAnimationFrame(loop);
});

stopButton.addEventListener("click", () => {
  running = false;
  cancelAnimationFrame(raf);
  if (ws) ws.close();
  socketStatus.textContent = "Disconnected";
});

setAccent(accentEl.value);
