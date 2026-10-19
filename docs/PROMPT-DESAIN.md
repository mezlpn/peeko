# Prompt untuk Claude Design

Salin semua teks di bawah garis ke Claude Design.

---

Saya sedang membuat **Peeko** (nama sementara), aplikasi desktop untuk Windows yang menaruh "notch" interaktif di tengah tepi atas layar, mirip Dynamic Island di iPhone. Notch berupa pil kecil gelap yang membesar dengan halus saat di-hover, dan menampilkan aktivitas yang sedang berjalan: musik, lirik sinkron, timer fokus, dan info sistem singkat.

Tolong rancang **design system dan semua layar utamanya**. Desainnya harus orisinal: punya identitas sendiri, jangan meniru NotchNook, Dynamic Island, atau aplikasi notch Windows lain secara persis.

## Karakter yang saya inginkan
- Tenang, rapi, dan "hidup" lewat gerakan, bukan lewat dekorasi.
- Dark-first: notch selalu gelap, hampir hitam, supaya menyatu dengan bezel layar. Aksen warna dipakai hemat.
- Terasa native di Windows, tapi lebih playful dari UI Windows standar.
- Ada sentuhan personal kecil saat idle, misalnya karakter mini, mata, atau animasi napas. Silakan usulkan konsepnya sendiri.

## Batasan teknis (wajib dipatuhi)
Aplikasi dibangun dengan **WPF (.NET 8) dan harus jalan di Windows 10** di laptop tua (Intel HD 520). Karena itu:
- **Jangan pakai blur latar/acrylic/Mica/glassmorphism.** Itu tidak tersedia atau terlalu berat. Pakai permukaan solid atau semi-opak.
- Bayangan dan glow harus minimal. Satu bayangan halus di panel expanded masih boleh.
- Font: **Segoe UI** (Segoe UI Variable tidak ada di Windows 10). Ikon: **Segoe Fluent Icons / Segoe MDL2 Assets** atau ikon garis sederhana yang konsisten.
- Semua ukuran dalam **px pada skala 100%**. Aplikasi akan di-scale otomatis untuk 125%/150%.
- Animasi harus bisa dibuat dengan transformasi dasar: ukuran, posisi, opacity, scale, dan corner radius. Sertakan durasi (ms) dan kurva easing (cubic-bezier).
- Bagian atas notch menempel ke tepi layar: sudut atas lurus atau menyatu dengan tepi, sudut bawah membulat.

## State yang perlu didesain
1. **Collapsed – idle**: pil kecil (sekitar 180–220 × 30–34 px) tanpa aktivitas. Sertakan konsep animasi idle.
2. **Collapsed – live activity**: pil sedikit melebar dengan info ringkas:
   - musik diputar: cover mini di kiri, visualizer bar kecil di kanan
   - timer fokus berjalan: ikon + sisa waktu
   - pengingat minum jatuh tempo
3. **Peek (event sesaat, tampil ±1,5 detik)**: varian ringkas untuk:
   - volume (level + mute), brightness (level)
   - Caps Lock on/off, ganti bahasa keyboard (mis. "EN" → "ID")
   - baterai lemah, charger dicolok/dilepas
   - Wi-Fi tersambung/putus (nama jaringan)
   - headphone/Bluetooth tersambung/terputus (nama perangkat)
4. **Expanded** (maksimal sekitar 560 × 260 px), dengan navigasi tab/ikon antar-modul:
   - **Musik**: cover, judul, artis, progress bar yang bisa digeser, tombol prev/play-pause/next, nama aplikasi sumber (Spotify, Chrome, dll.)
   - **Lirik**: baris aktif ditonjolkan, baris sebelum dan sesudahnya redup, dengan transisi antarbaris. Sertakan state "lirik tidak ditemukan" dan "memuat".
   - **Timer fokus (Pomodoro)**: sisa waktu, mode fokus/istirahat, mulai/jeda/reset, jumlah sesi
   - **Minum air**: progress harian (mis. 5/8 gelas), tombol "+1 gelas", jadwal pengingat berikutnya
   - **To-do**: daftar singkat, tambah item, centang selesai
   - **Shelf (rak file)**: area drop file. Tampilkan state kosong, state saat file di-drag di atasnya, dan state berisi beberapa file.
5. **Empty/edge states**: tidak ada musik yang diputar, sumber musik tidak mendukung seek, judul lagu sangat panjang (marquee atau terpotong?).
6. **Jendela Pengaturan** (jendela biasa, bukan notch): fitur on/off per modul, start with Windows, posisi notch, ambang baterai, interval minum, durasi pomodoro, toggle "ambil lirik dari internet (lrclib.net)".
7. **Menu ikon tray** (sederhana): Pengaturan, Sembunyikan sementara, Keluar.

## Gerakan (motion)
Tolong spesifikasikan:
- Transisi Collapsed ↔ Expanded: bentuk pil "mengembang" lalu isinya muncul, bukan langsung membesar sekaligus. Saya ingin rasa elastis/pegas yang halus.
- Collapsed → Peek → Collapsed.
- Pergantian tab di Expanded.
- Notch menyingkir ke atas saat ada aplikasi fullscreen, lalu kembali.
- Pergantian baris lirik.
- Animasi idle.

## Yang saya butuhkan sebagai hasil
1. **Design tokens** dalam tabel: warna (dengan nama peran, mis. `surface`, `surface-raised`, `text-primary`, `text-secondary`, `accent`, `danger`, dsb., dalam hex), radius, spacing scale, tipografi (ukuran/weight/line-height), dan durasi + easing animasi. Token ini akan saya terjemahkan langsung ke XAML ResourceDictionary.
2. **Layar/mockup** untuk semua state di atas, dengan ukuran px yang jelas.
3. **Spesifikasi motion** per transisi (durasi, easing, urutan/stagger).
4. **Catatan komponen**: tombol, slider/progress, tab, item list, chip. Sertakan state hover/pressed/disabled.
5. Opsional: usulan nama aplikasi dan konsep ikon aplikasi yang cocok dengan karakter desainnya.

Teks UI dalam **Bahasa Indonesia**.
