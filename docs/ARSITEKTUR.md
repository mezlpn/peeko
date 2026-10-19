# Celah — Rancangan Sistem

> "Celah" adalah nama kerja sementara. Silakan diganti.
> Ini aplikasi notch untuk Windows: jendela kecil di tepi atas layar yang membesar saat di-hover dan menampilkan musik, lirik, timer, dan info sistem.

## 1. Batasan dan target

| Hal | Keputusan |
|---|---|
| OS target | Windows 10 versi 2004 (build 19041) ke atas, juga Windows 11 |
| Mesin acuan | ThinkPad T470: i5-6300U, Intel HD 520, ±20 GB RAM, SSD |
| Stack | C# + .NET 8 + WPF, `TargetFramework = net8.0-windows10.0.19041.0` (supaya bisa memanggil API WinRT) |
| Tool | VS Code + .NET SDK (`dotnet build`/`run`), tanpa Visual Studio |
| Target performa | Idle (notch tertutup, tidak ada musik): CPU ≈ 0%, RAM < 80 MB. Animasi 60 fps di HD 520 |
| Penyimpanan data | File JSON di `%AppData%\Celah\` |
| Efek visual | Tidak bergantung pada Mica/Acrylic, karena keduanya tidak ada di Windows 10 |

## 2. Struktur solusi

```
Celah.sln
├─ src/
│  ├─ Celah.App/          WPF: jendela, View (XAML), ViewModel, tray, startup
│  ├─ Celah.Core/         Logika murni tanpa WPF/Win32: model, state machine, parser LRC,
│  │                      jam posisi lagu, pomodoro, to-do, pengaturan
│  └─ Celah.Platform/     Interop Windows: WinRT media, Core Audio, Win32 hook,
│                         WMI, deteksi fullscreen, monitor/DPI
└─ tests/
   └─ Celah.Core.Tests/   Unit test (xUnit) untuk Celah.Core
```

Aturan dependensi: `App → Core`, `App → Platform`, `Platform → Core`. `Core` tidak bergantung pada apa pun yang khusus Windows, jadi bisa dites tanpa membuka jendela.

Paket NuGet:
- `CommunityToolkit.Mvvm` untuk MVVM (ObservableObject, RelayCommand, Messenger)
- `Microsoft.Extensions.Hosting` untuk DI, konfigurasi, dan logging
- `H.NotifyIcon.Wpf` untuk ikon tray
- `NAudio.Wasapi` untuk volume dan event perangkat audio (Core Audio). Alternatifnya interop COM sendiri.
- `System.Management` untuk WMI brightness

## 3. Gambaran arsitektur

```
 ┌──────────────── Celah.Platform (sumber event) ────────────────┐
 │ MediaSource  AudioEndpoint  Brightness  Keyboard  Power  Net  │
 │ FullscreenWatcher  MonitorWatcher                             │
 └───────────────┬───────────────────────────────────────────────┘
                 │ event (thread latar)
                 ▼
 ┌──────────────── Celah.Core ───────────────────────────────────┐
 │ Service fitur: Media, Lyrics, Pomodoro, Water, Todo, Shelf    │
 │                         │                                     │
 │                         ▼                                     │
 │ ActivityArbiter  ← memilih "aktivitas" yang tampil (prioritas)│
 │                         │                                     │
 │ NotchStateMachine  ← Hidden / Collapsed / Peek / Expanded     │
 └─────────────────────────┬─────────────────────────────────────┘
                           │ (dikirim ke Dispatcher UI)
                           ▼
 ┌──────────────── Celah.App ────────────────────────────────────┐
 │ NotchWindow + NotchViewModel                                  │
 │   ├─ CompactPresenter  (isi saat tertutup/peek)               │
 │   └─ ExpandedPresenter (tab: Musik, Timer, Shelf, To-do)      │
 │ TrayIcon, SettingsWindow                                      │
 └───────────────────────────────────────────────────────────────┘
```

### 3.1 State jendela notch

```
          fullscreen app aktif
 ┌────────┐ ◄──────────────────── ┌───────────┐
 │ Hidden │                       │ Collapsed │ ◄─── default
 └────────┘ ───────────────────►  └───────────┘
          fullscreen selesai        │  ▲    │ ▲
                     mouse masuk     │  │    │ │ timeout 1.5–2 dtk
                     (delay 150ms)   ▼  │    ▼ │
                              ┌──────────┐ ┌──────┐
                              │ Expanded │ │ Peek │ ◄─ event sesaat (volume,
                              └──────────┘ └──────┘    baterai, headphone…)
                    mouse keluar (delay 300ms) → Collapsed
```

- **Collapsed**: pil kecil. Kalau ada aktivitas live (musik main, timer jalan), isinya ringkas: cover kecil + visualizer, atau sisa waktu.
- **Peek**: sedikit melebar selama ±1,5 detik untuk event sesaat, lalu kembali ke Collapsed.
- **Expanded**: panel penuh dengan tab.
- **Hidden**: menyingkir ke atas layar saat ada aplikasi fullscreen (game, video, presentasi).

State machine ini ada di `Core` sebagai kelas murni (input: event; output: state + ukuran target), jadi transisinya bisa diuji dengan unit test.

### 3.2 ActivityArbiter (siapa yang tampil di mode compact)

Setiap fitur mendaftarkan `IActivity { Id, Priority, IsLive, Kind (Persistent|Transient) }`. Arbiter memilih yang tampil:

1. Event transient (volume, Caps Lock, baterai, dll.) selalu menang, lalu hilang setelah timeout.
2. Aktivitas persistent dengan prioritas tertinggi yang sedang live. Default urutannya: Timer berjalan > Musik main > Pengingat minum jatuh tempo.
3. Kalau tidak ada yang live: idle (pil kosong atau "mata" idle, sesuai desain).

## 4. Komponen platform

| Komponen | API | Catatan |
|---|---|---|
| **NotchWindow** | WPF `WindowStyle=None`, `AllowsTransparency=True`, `Topmost`. Ex-style Win32: `WS_EX_TOOLWINDOW` (tidak muncul di taskbar/Alt-Tab), `WS_EX_NOACTIVATE` (tidak mencuri fokus) | Piksel transparan otomatis tembus klik. Topmost ditegaskan ulang setiap kali foreground berubah. |
| **MonitorWatcher** | `EnumDisplayMonitors`, `GetDpiForMonitor`, `WM_DPICHANGED`, `SystemEvents.DisplaySettingsChanged` | Manifest: PerMonitorV2. Notch ada di monitor utama (opsi: monitor tempat kursor berada). |
| **FullscreenWatcher** | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` + bandingkan rect jendela foreground dengan rect monitor, plus `SHQueryUserNotificationState` | Berbasis event, tidak polling. Desktop/shell (Progman, WorkerW) dikecualikan. |
| **MediaSource** | `GlobalSystemMediaTransportControlsSessionManager` | Judul, artis, cover (thumbnail stream), status, timeline. Kontrol: play/pause/next/prev/seek (cek `Controls.IsXxxEnabled`). Pilih sesi `GetCurrentSession()`, opsional bisa ganti sesi. |
| **AudioEndpoint** | Core Audio: `IAudioEndpointVolumeCallback`, `IMMNotificationClient` | Event volume/mute; event ganti perangkat default untuk deteksi headphone/Bluetooth masuk-keluar. |
| **Brightness** | WMI `WmiMonitorBrightnessEvent` (event) | Hanya layar laptop. Monitor eksternal (DDC/CI) di luar cakupan v1. |
| **Keyboard** | `WH_KEYBOARD_LL` hook hanya untuk deteksi tombol Caps Lock/volume/brightness, lalu `GetKeyState`. Bahasa input: `GetKeyboardLayout` thread foreground, dicek saat event foreground/`WM_INPUTLANGCHANGE` | Hook dijaga sangat ringan (callback < 1 ms, langsung lempar ke antrean). |
| **Power** | `SystemEvents.PowerModeChanged` + `GetSystemPowerStatus` | Event charger masuk/lepas dan baterai lemah (ambang bisa diatur). |
| **Network** | `NetworkInformation.NetworkStatusChanged` | Wi-Fi tersambung/putus, nama SSID. |
| **Startup** | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | Toggle di pengaturan. |

## 5. Fitur inti (Core)

### 5.1 Media + jam posisi
Windows hanya mengirim posisi lagu saat ada perubahan (play, pause, seek). `PlaybackClock` menghitung posisi sendiri:

```
posisi = timeline.Position + (status == Playing ? (now - timeline.LastUpdatedTime) * rate : 0)
```

Hasilnya dijepit ke `[0, EndTime]`. Kalau `LastUpdatedTime` aneh (beberapa browser), clock di-reset ke posisi terakhir dan waktu lokal. Clock ini bersifat pull (dibaca saat render), bukan timer yang terus jalan.

### 5.2 Lirik
- `LrcParser`: parsing format LRC (`[mm:ss.xx] teks`, beberapa timestamp per baris, offset `[offset:]`). Unit test wajib.
- `LyricsService`: `GET https://lrclib.net/api/get?track_name=&artist_name=&album_name=&duration=`, kalau gagal fallback ke `/api/search`. Header `User-Agent: Celah/<versi> (<url repo>)`.
- Cache disk `%AppData%\Celah\lyrics\<hash>.lrc`, termasuk cache "tidak ditemukan" (TTL 7 hari) supaya tidak terus-menerus request.
- Pencocokan baris pakai binary search pada posisi dari `PlaybackClock`.
- Timer lirik hanya aktif saat lirik sedang terlihat. Dihitung dari waktu ke baris berikutnya, bukan polling per frame.
- Privasi: judul dan artis dikirim ke lrclib.net. Fitur ini bisa dimatikan di pengaturan.

### 5.3 Fitur lain
- **Pomodoro**: state machine fokus/istirahat, simpan `endsAt` (bukan sisa detik) supaya akurat setelah sleep.
- **Pengingat minum**: interval + target harian, counter direset tiap hari.
- **To-do**: daftar sederhana (teks, selesai, urutan).
- **Shelf**: drop file → simpan referensi path (default) atau salinan ke `%AppData%\Celah\shelf\`. Drag keluar pakai `DataObject` FileDrop.

## 6. Threading dan performa

- Event WinRT/COM/hook datang dari thread latar. Semua harus `Dispatcher.InvokeAsync` sebelum menyentuh ViewModel.
- **Tidak ada polling saat idle.** Semua sumber berbasis event. Timer UI (visualizer, lirik, countdown) hanya hidup saat elemennya terlihat.
- Animasi: `Storyboard` dengan `Timeline.DesiredFrameRate` diatur. Hindari `DropShadowEffect`/`BlurEffect` besar di jendela layered karena mahal di software render.
- **Risiko utama:** `AllowsTransparency=True` membuat WPF me-render jendela secara layered (lebih banyak kerja CPU). Mitigasinya:
  1. Ukuran jendela = ukuran visual saat ini + margin kecil, bukan jendela besar yang tetap.
  2. **Spike tahap 1:** ukur CPU saat idle dan saat animasi di T470. Kalau animasi > ~10% CPU, uji alternatif: jendela non-layered + `DwmExtendFrameIntoClientArea` + region (`SetWindowRgn`) untuk sudut membulat.
- Cover album di-decode ke ukuran tampilan (`DecodePixelWidth`), lalu di-cache per lagu.

## 7. Data dan konfigurasi

```
%AppData%\Celah\
├─ settings.json     (posisi, perilaku, fitur aktif/nonaktif, ambang baterai, dll.)
├─ todo.json
├─ water.json
├─ shelf.json
├─ lyrics\           (cache)
└─ logs\             (rolling, maks 5 file × 1 MB)
```

Penulisan file atomik (tulis ke `.tmp` lalu `File.Replace`). Skema diberi field `version` untuk migrasi ke depan.

## 8. Di luar cakupan v1

- Menyembunyikan OSD volume bawaan Windows (butuh trik tidak resmi)
- Brightness monitor eksternal (DDC/CI)
- Notch di beberapa monitor sekaligus
- Notifikasi aplikasi lain (Toast listener butuh izin `UserNotificationListener` dan paket MSIX)
- Auto-update dan installer (sementara dibagikan sebagai zip/self-contained publish)

## 9. Tahapan kerja

| Tahap | Isi | Selesai jika… |
|---|---|---|
| **0. Kerangka** | Solusi, 3 proyek + test, DI host, logging, tray, single-instance (Mutex) | `dotnet build` dan `dotnet test` hijau, ikon tray muncul |
| **1. Notch** | NotchWindow, state machine, hover expand/collapse, DPI/monitor, FullscreenWatcher, **spike performa** | Notch tajam di skala 100/125/150%, menyingkir saat video fullscreen, CPU idle ≈ 0% |
| **2. Musik** | MediaSource, PlaybackClock, compact (cover + visualizer) + expanded (kontrol, progress, seek) | Spotify & YouTube di Chrome/Edge terbaca, kontrol berfungsi |
| **3. Lirik** | LrcParser + test, LyricsService + cache, tampilan lirik | Lirik sinkron (selisih < 300 ms), tidak loncat-loncat |
| **4. Overlay sistem** | Volume, brightness, Caps Lock, bahasa, baterai, Wi-Fi, headphone → Peek | Setiap event muncul ±1,5 dtk lalu hilang |
| **5. Produktivitas** | Pomodoro, minum, to-do, shelf | Data tetap ada setelah restart |
| **6. Pengaturan & rilis** | SettingsWindow, startup, publish self-contained | Satu folder zip yang bisa jalan di PC lain |

## 10. Keputusan atas desain (18 Okt 2026)

Desain lengkap ada di `design/project/*.dc.html`. Bagian berikut **menimpa** desain tersebut:

1. **Idle tanpa loop.** Mata Kedip diam saat idle. Kedip (±160 ms) muncul pada interval acak 4–9 dtk. Napas hanya diputar ±4 dtk setelah hover/event, lalu berhenti. Tujuannya: CPU idle ≈ 0%.
2. **Melirik** hanya saat kursor berada di atas jendela notch. Tidak ada hook atau polling mouse global.
3. **OSD bawaan Windows tidak disembunyikan** di v1. Peek volume/brightness tampil bersama OSD bawaan.
4. **Klik di mode collapsed** (cover, chip "+1 gelas"): jeda hover di atas area yang bisa diklik diperpanjang (awal: 400 ms). Nilainya akan disetel setelah dicoba langsung.
5. **Shelf:** reaksi drag dimulai saat file masuk ke area pil, bukan saat masih 80 px di sekitarnya.
6. **Baterai headphone** hanya ditampilkan jika tersedia. **Visualizer** memakai level peak WASAPI (`IAudioMeterInformation`), bukan spektrum.

Revisi setelah dicoba langsung:

7. **Hover lebih cepat.** Begitu kursor menyentuh notch, pil langsung menggembung ke 212 × 34 (90 ms) sebagai umpan balik. Jeda hover 120 → 60 ms. Bentuk mengembang 460 → 340 ms, dan isi panel muncul mulai 120 ms (sebelumnya 220 ms).
8. **Mode tipis (state `Slim`).** Jika jendela aktif menempel ke tepi atas layar dan melintasi tengahnya (mis. Chrome di-maximize dengan tab di atas), notch menjadi garis 160 × 4 tanpa bahu dan tanpa Kedip. Hover pada garis ±150 ms membuka panel penuh, lalu kembali menjadi garis saat ditinggal. Peek tetap turun sebentar lalu kembali menjadi garis. Fullscreen tetap menang (notch disembunyikan). Nanti di Pengaturan: Otomatis / Selalu pil / Selalu garis.
9. ~~Pil musik 104 px~~ → diganti #12.
10. **Kedip kembali 1,5 dtk setelah musik dijeda** (desain: 30 dtk). Tidak 0 karena beberapa pemutar sempat melaporkan "dijeda" saat ganti lagu.
11. **Klik cover di pil collapsed tidak dibuat.** Jeda hover 60 ms (revisi #7) membuat pil sudah mengembang sebelum sempat diklik; play/pause ada di panel.
12. **Pil musik tetap 200 × 32** seperti pil idle (desain: 260 px dengan cover di kiri dan bar di kanan). Cover dan bar dirapatkan di tengah dengan jarak 10 px, sama seperti jarak kedua mata Kedip.
13. **Pemilihan sumber media** (`SessionSelector`). Sesi yang tampil tidak ikut berpindah saat dijeda. Sesi lain yang baru mulai diputar mengambil alih, kecuali pengguna memilih sesi secara manual dan sesi itu masih diputar. Jika ada lebih dari satu sesi, chip sumber di modul Musik diberi panah "‹ ● Brave 1/2 ›".
14. **Lirik:** baris yang terlalu panjang dipotong dengan elipsis (desain: dibungkus jadi 2 baris dengan slot 52 px). Slot bervariasi membuat pergeseran daftar lebih rumit; ditinjau ulang jika sering terjadi. Varian "Ambil lirik dari internet sedang mati" menunggu jendela Pengaturan (tahap 6). Teks biasa bisa di-scroll dengan roda mouse, tapi tanpa scrollbar tipis.
15. **Pencarian lirik** mengutamakan lirik tersinkron. Hasil teks biasa dari pencocokan persis hanya dijadikan cadangan, lalu dicari lagi dengan judul dasar tanpa keterangan versi seperti "(Alternate Version)". Maksimal 3 kandidat kueri.
16. **Peek sistem (tahap 4).**
    - Hook keyboard berjalan di thread sendiri (tidak ikut terhambat thread UI) dan hanya membaca tombol Caps Lock serta tombol pengubah bahasa.
    - Bahasa dicek 150 ms setelah Shift/Alt/Ctrl/Win/Spasi dilepas. Saat jendela aktif berganti, bahasa dicatat diam-diam karena bisa berbeda per aplikasi. Ganti bahasa lewat klik ikon di taskbar belum terdeteksi.
    - "Perangkat terputus" hanya untuk perangkat output yang sedang dipakai.
    - Saat panel terbuka, peek tampil sebagai baris 32 px di bawah bar tab.
    - Klik peek belum membuka modul terkait karena modulnya belum ada; hover tetap membuka panel.
    - Argumen `--demo-peeks` memutar semua contoh peek, untuk mengecek tampilan.
17. **Modul produktivitas (tahap 5).**
    - Timer fokus menyimpan waktu selesai (`EndsAt`), jadi tetap akurat setelah sleep. Fokus selesai → istirahat langsung berjalan; istirahat selesai → fokus berikutnya menunggu ditekan.
    - Pil fokus, musik + fokus, dan idle sama-sama 200 px. Pengingat minum 260 px karena butuh ruang untuk teks dan chip "+1 gelas"; di atas pil ini jeda hover dinaikkan ke 450 ms supaya chip bisa diklik (keputusan #4).
    - Pengingat minum tampil 8 dtk, lalu berikutnya satu interval lagi walau belum minum.
    - Kotak input to-do: jendela notch sementara dibuat bisa aktif (tanpa `WS_EX_NOACTIVATE`) dan panel ditahan terbuka; kembali normal saat input selesai atau jendela kehilangan fokus.
    - Rak: file dari folder sementara disalin; file lain hanya ditautkan dan file asli tidak pernah dihapus. Seret file ke pil: peek "Simpan di rak", ditahan 600 ms → panel terbuka ke tab Rak.
    - Belum dibuat: seret untuk mengurutkan to-do, pilih banyak file di rak (Ctrl+klik), kartu "+n" (diganti gulir horizontal), dan pengaturan durasi/target (menunggu jendela Pengaturan).
18. **Ganti tab** (desain: isi lama hilang cepat, isi baru masuk ±12 px): sekarang geser penuh 48 px. Isi lama keluar searah tab sambil memudar dan isi baru masuk dari sisi lain, bersamaan, dalam 220–280 ms. Area modul memotong isinya supaya geseran tidak melewati tepi panel.

## 11. Kontrak desain ↔ kode

Hasil dari Claude Design diterjemahkan menjadi:
- `Themes/Tokens.xaml`: warna, radius, spacing, tipografi, ikon garis (ResourceDictionary)
- `Animation/Motion.cs`: durasi & easing. Disimpan di C#, bukan XAML, karena animasi notch dibangun dari kode. `CubicBezierEase` memakai kurva CSS apa adanya, termasuk kurva pegas dengan y > 1, jadi tidak perlu aproksimasi `BackEase`.
- Satu `UserControl` per layar/komponen

## 12. Hasil spike performa tahap 1 (18 Okt 2026, T470, skala 125%)

| Kondisi | Hasil |
|---|---|
| Idle 30 dtk (termasuk kedipan acak) | CPU 0,08% total, 94 ms CPU time |
| 3× buka-tutup dalam 6,4 dtk | ±15% dari 1 core selama animasi (±4% total) |
| Memori | Private 44 MB, working set 113 MB (sebagian besar DLL WPF bersama) |
| Fullscreen | Menyingkir seketika dan kembali 400 ms setelah fullscreen berakhir |

Kesimpulan: WPF dengan `AllowsTransparency` cukup untuk proyek ini. Alternatif DWM/region tidak diperlukan.

Belum ada: bayangan 9-slice di panel expanded, serta animasi indikator tab yang meluncur.

Karena itu, prompt desain meminta token dan ukuran dalam piksel pada skala 100%, tanpa efek yang tidak bisa dibuat di WPF/Windows 10.
