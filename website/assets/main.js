// Website Peeko: navigasi, animasi muncul, demo notch, dan info rilis terbaru dari GitHub.
(() => {
  const REPO = "mezlpn/peeko";
  const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ── Navigasi ──
  const nav = document.getElementById("nav");
  const toggle = document.getElementById("nav-toggle");
  addEventListener("scroll", () => nav?.classList.toggle("scrolled", scrollY > 8), { passive: true });
  toggle?.addEventListener("click", () => {
    const open = nav.classList.toggle("open");
    toggle.setAttribute("aria-expanded", String(open));
  });
  document.querySelectorAll(".nav-links a").forEach(a => a.addEventListener("click", () => nav.classList.remove("open")));

  // ── Muncul saat digulir ──
  const io = "IntersectionObserver" in window && new IntersectionObserver(entries => {
    for (const e of entries) if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
  }, { rootMargin: "0px 0px -8% 0px" });
  document.querySelectorAll(".reveal").forEach(el => io ? io.observe(el) : el.classList.add("in"));

  // ── Kedip: berkedip pada jeda acak 4–9 dtk, seperti di aplikasi ──
  function blinkLoop(el) {
    if (reduced) return;
    const go = () => {
      el.classList.remove("blink"); void el.offsetWidth; el.classList.add("blink");
      setTimeout(go, 4000 + Math.random() * 5000);
    };
    setTimeout(go, 1500 + Math.random() * 3000);
  }
  document.querySelectorAll("#eyes, [data-blink]").forEach(blinkLoop);

  // ── Rilis terbaru: versi, ukuran, dan tautan unduh langsung ──
  async function latest() {
    try {
      const cached = sessionStorage.getItem("peeko-latest");
      if (cached) return JSON.parse(cached);
    } catch { /* penyimpanan diblokir: ambil langsung */ }
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: "application/vnd.github+json" } });
    if (!r.ok) throw new Error(r.status);
    const j = await r.json();
    const zip = (j.assets || []).find(a => /win-x64\.zip$/i.test(a.name));
    const info = { tag: j.tag_name, url: zip?.browser_download_url, name: zip?.name, size: zip?.size };
    try { sessionStorage.setItem("peeko-latest", JSON.stringify(info)); } catch { }
    return info;
  }
  latest().then(info => {
    const v = (info.tag || "").replace(/^v/, "");
    document.querySelectorAll("[data-version]").forEach(el => el.textContent = el.closest(".btn") ? `Peeko ${v}` : `versi ${v}`);
    if (info.size) document.querySelectorAll("[data-size]").forEach(el => el.textContent = `zip ${Math.round(info.size / 1e6)} MB`);
    if (info.name) document.querySelectorAll("[data-asset]").forEach(el => el.textContent = info.name);
    if (info.url) document.querySelectorAll("[data-download]").forEach(a => a.href = info.url);
  }).catch(() => { /* tetap ke halaman Releases */ });

  // ── Demo notch ──
  const notch = document.getElementById("notch");
  if (!notch) return;
  const body = document.getElementById("notch-body");
  const stage = document.getElementById("stage");
  const views = [...document.querySelectorAll("#views .view")];
  const dotsHost = document.getElementById("dots");
  let index = 0, open = false, hoverTimer, leaveTimer, wheelAcc = 0, lastWheel = 0;

  const dots = views.map((v, i) => {
    const b = document.createElement("button");
    b.type = "button";
    b.title = v.dataset.name;
    b.setAttribute("aria-label", `Tampilan ${v.dataset.name}`);
    b.addEventListener("click", e => { e.stopPropagation(); select(i); });
    dotsHost.appendChild(b);
    return b;
  });

  // Lebar notch dibatasi lebar panggung supaya tetap muat di layar HP.
  function fit(w) { return Math.min(w, stage.clientWidth - 40); }

  function shape() {
    if (open) {
      const v = views[index];
      body.style.width = fit(+v.dataset.w) + "px";
      body.style.height = v.dataset.h + "px";
    } else {
      body.style.width = "200px";
      body.style.height = "32px";
    }
  }

  function select(i, dir) {
    if (i === index || i < 0 || i >= views.length) return;
    dir = dir ?? (i > index ? 1 : -1);
    const old = views[index];
    old.classList.remove("on");
    old.classList.toggle("up", dir > 0);
    const next = views[i];
    next.classList.remove("up");
    if (dir < 0) { next.style.transition = "none"; next.classList.add("up"); void next.offsetWidth; next.style.transition = ""; next.classList.remove("up"); }
    next.classList.add("on");
    index = i;
    dots.forEach((d, k) => d.classList.toggle("on", k === i));
    shape();
  }

  function setOpen(v) {
    if (open === v) return;
    open = v;
    notch.classList.toggle("open", v);
    stage.classList.add("touched");
    shape();
  }

  views[0].classList.add("on");
  dots[0].classList.add("on");

  notch.addEventListener("mouseenter", () => { clearTimeout(leaveTimer); hoverTimer = setTimeout(() => setOpen(true), 60); });
  notch.addEventListener("mouseleave", () => { clearTimeout(hoverTimer); leaveTimer = setTimeout(() => setOpen(false), 300); });
  notch.addEventListener("click", () => setOpen(true));
  notch.addEventListener("keydown", e => {
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); setOpen(!open); }
    else if (e.key === "Escape") setOpen(false);
    else if (open && (e.key === "ArrowDown" || e.key === "ArrowRight")) { e.preventDefault(); select(index + 1); }
    else if (open && (e.key === "ArrowUp" || e.key === "ArrowLeft")) { e.preventDefault(); select(index - 1); }
  });
  document.addEventListener("click", e => { if (open && !notch.contains(e.target)) setOpen(false); });

  // Gulir seperti di aplikasi: tiap langkah roda dihitung, gulir cepat melompati beberapa tampilan.
  notch.addEventListener("wheel", e => {
    if (!open) return;
    e.preventDefault();
    const now = performance.now();
    const step = e.deltaY > 0 ? 1 : -1;
    if (now - lastWheel > 400 || Math.sign(wheelAcc) !== step) wheelAcc = 0;
    lastWheel = now;
    wheelAcc += e.deltaMode === 1 ? e.deltaY * 40 : e.deltaY;
    let target = index;
    while (Math.abs(wheelAcc) >= 100) {
      wheelAcc -= Math.sign(wheelAcc) * 100;
      if (target + step < 0 || target + step >= views.length) { wheelAcc = 0; break; }
      target += step;
    }
    if (target !== index) select(target, step);
  }, { passive: false });

  // Layar sentuh: geser ke atas/bawah di panel untuk berganti tampilan.
  let touchY = null;
  notch.addEventListener("touchstart", e => { touchY = e.touches[0].clientY; }, { passive: true });
  notch.addEventListener("touchend", e => {
    if (touchY == null || !open) return;
    const dy = touchY - e.changedTouches[0].clientY;
    if (Math.abs(dy) > 30) select(index + (dy > 0 ? 1 : -1));
    touchY = null;
  });

  addEventListener("resize", shape);

  // Jam demo dan kalender bulan ini.
  const clock = document.getElementById("demo-clock");
  const tick = () => { const d = new Date(); clock.textContent = `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; };
  tick(); setInterval(tick, 30000);

  const cal = document.getElementById("cal");
  if (cal) {
    const now = new Date();
    const first = new Date(now.getFullYear(), now.getMonth(), 1).getDay();
    const days = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
    let html = "MSSRKJS".split("").map(h => `<span class="h">${h}</span>`).join("");
    for (let i = 0; i < first; i++) html += "<span></span>";
    for (let d = 1; d <= days; d++) html += `<span${d === now.getDate() ? ' class="t"' : ""}>${d}</span>`;
    cal.innerHTML = html;
  }
})();
