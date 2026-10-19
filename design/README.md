# Celah — paket desain (handoff)

Design system dan mockup untuk **Celah**, aplikasi notch WPF (.NET 8, Windows 10+).
Dibuat dengan Claude Design.

## Isi

`project/` berisi satu file `.dc.html` per papan. Setiap file adalah HTML biasa dengan style inline, jadi baca sebagai **sumber spesifikasi**: ukuran px, warna hex, durasi, dan easing ada langsung di markup dan di blok `<script>`.
File ini merujuk `./support.js` (runtime kanvas), sehingga tidak akan tampil sempurna jika dibuka langsung di browser.

| File | Isi |
|---|---|
| `Main.dc.html` | Konsep, prinsip, karakter idle "Kedip", ikon aplikasi, usulan nama |
| `Tokens.dc.html` | **Design tokens**: warna, radius, spacing, tipografi, durasi + easing (data ada di `renderVals()`) |
| `Components.dc.html` | Tombol, slider, tab, item list, chip, toggle, stepper + state; pemetaan glyph Segoe MDL2 |
| `Motion.dc.html` | Timeline per transisi (data di `renderVals()`: `[label, start ms, durasi ms, easing, jenis]`) + catatan implementasi WPF |
| `Collapsed.dc.html` | Idle & live activity (musik, timer, minum, gabungan) + prioritas slot |
| `Peek.dc.html` | 13 varian peek + aturan antrian |
| `Music.dc.html`, `Lyrics.dc.html`, `Focus.dc.html`, `WaterTodo.dc.html`, `Shelf.dc.html` | Modul Expanded 560 × 260 beserta edge states |
| `Settings.dc.html` | Jendela Pengaturan 960 × 720 + menu tray |
| `canvas.json` | Indeks papan (posisi di kanvas, tidak penting untuk implementasi) |

## Catatan implementasi penting

- Semua ukuran px pada skala 100%. Font Segoe UI, ikon Segoe MDL2 Assets.
- **Tanpa** blur, acrylic, atau Mica. Satu-satunya bayangan ada di panel Expanded; buat sebagai PNG 9-slice, bukan `DropShadowEffect`.
- Penamaan kunci XAML: `Celah.Brush.<Peran>`, `Celah.Radius.*`, `Celah.Space.*`, `Celah.Dur.*`, `Celah.Ease.*`.
- `KeySpline` WPF hanya menerima 0–1. Untuk `ease-spring` (0.34, 1.32, 0.64, 1) pakai `BackEase EaseOut Amplitude=0.32`, dan untuk `ease-spring-soft` pakai Amplitude=0.14.
- Jendela notch berukuran tetap dan transparan. Animasikan Width/Height sebuah Border di dalam Canvas; konten hanya menerima Opacity dan RenderTransform.
- Pakai `HandoffBehavior.SnapshotAndReplace` agar animasi bisa dibatalkan di tengah jalan. `DesiredFrameRate` 60 untuk transisi, 30 untuk loop idle.
- Saat `SystemParameters.ClientAreaAnimation == false`, semua transisi hanya crossfade 120 ms.
