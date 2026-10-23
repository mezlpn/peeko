# Prompt video pembaruan Peeko 0.3.0 untuk Claude Design

Salin semua teks di bawah garis ke Claude Design, lalu lampirkan isi folder `screenshot/` dari paket
`Peeko-0.3.0-video-brief.zip`.

---

Saya mau membuat **video motion graphic pembaruan Peeko 0.3.0** untuk **Instagram Reels**. Peeko adalah
aplikasi notch gratis untuk Windows buatan saya. Video sebelumnya (peluncuran) sudah tayang; video ini
mengumumkan versi baru **dan** menunjukkan cara memasangnya dari GitHub saya.

Karakter yang saya mau: **cepat, tepat, dan profesional**. Bayangkan video pembaruan produk dari Apple,
Linear, Raycast, atau Nothing: potongan rapat mengikuti ketukan, tipografi kinetik yang tegas, setiap
gerakan punya tujuan, tidak ada yang melayang tanpa arah. Tetap ada sedikit sisi lucu dari **mata Peeko**,
tapi kesan utamanya rapi dan premium.

## Apa yang baru di 0.3.0

1. **Tampilan baru yang ringkas.** Tombol tab hilang. Bagian atas panel kini **baris status**: suhu di kiri,
   **mata Peeko kecil + jam** di tengah, baterai di kanan. Tepat di bawah jam ada **titik halaman** (titik aktif
   memanjang jadi pil putih).
2. **Gulir untuk pindah tampilan.** Gulir mouse di mana saja di panel untuk pindah tampilan; gulir cepat
   melompati beberapa sekaligus. Ada 5 tampilan, dan **ukuran notch berubah mengikuti isinya**:
   **Musik + lirik** · **Kontrol** · **Hari ini** · **Tugas + minum** · **Rak**.
3. **Animasi fisika pegas.** Lebar, tinggi, sudut, dan posisi notch masing-masing digerakkan pegas sendiri,
   jadi gerakannya kenyal tapi cepat mendarat, dan tetap mulus walau arahnya dibalik di tengah jalan.
4. **Musik + lirik digabung**: pemutar satu baris (cover, judul, tombol, progress) dengan pita lirik 3 baris
   di bawahnya. **Visualizer 5 bar** bergoyang mengikuti bass, vokal, dan treble.
5. **Kontrol cepat**: ubin Wi-Fi, Bluetooth, Jangan ganggu, Hemat baterai; tombol bundar notifikasi,
   tangkap layar, kunci layar, Pengaturan; slider volume dan kecerahan.
6. **Hari ini**: kalender bulan ini (tanggal hari ini dalam lingkaran limau) di samping timer fokus.
7. **Tugas + minum**: daftar tugas dengan kolom minum (gelas mini terisi biru).
8. **Rak menerima teks dan tautan**, bukan cuma file. Tautan tampil dengan ikon rantai + nama situs;
   teks tampil sebagai cuplikan seperti kertas catatan kecil. Klik dua kali: buka tautan / salin teks.
9. **Ubah format file**: seret foto/PDF/dokumen ke notch, pilih PNG, JPG, PDF, Word…

## Lampiran (`screenshot/`)

| File | Isi |
|---|---|
| `01-pil-diam-mata.png` | Notch tertutup: pil hitam dengan dua mata |
| `02-pil-musik-visualizer.png` | Notch tertutup saat lagu diputar: cover mini + 5 bar visualizer |
| `10-tampilan-1-musik-lirik.png` | Tampilan Musik + lirik (pada screenshot liriknya "tidak ditemukan"; di video tampilkan pita lirik 3 baris yang berjalan) |
| `11-tampilan-2-kontrol.png` | Tampilan Kontrol |
| `12-tampilan-3-hari-ini.png` | Kalender + timer fokus |
| `13-tampilan-4-tugas-minum.png` | Tugas + kolom minum |
| `14-tampilan-5-rak-teks-tautan.png` | Rak berisi satu tautan dan satu teks |
| `20-seret-teks-ke-notch.png` | Saat teks diseret di atas notch ("Lepaskan untuk menyimpan · 1 teks") |
| `21-ubah-format.png` | Panel Ubah format dengan chip format |

- Latar di sekitar notch sudah diganti abu-abu polos; itu bukan bagian desain.
- **Bangun ulang UI-nya** mengikuti screenshot (jangan menempel gambar), supaya setiap bagian bisa dianimasikan.
- **Judul lagu, artis, cover, lirik, isi tugas, teks dan tautan contoh harus fiktif.** Contoh lagu:
  "Senja di Kota" oleh "Nadira", cover gradasi abstrak; lirik karangan sendiri. Tautan contoh:
  `github.com/mezlpn/peeko` boleh dipakai karena itu milik saya.

## Identitas visual (pakai persis)

- Notch: hitam **#050506**, menempel di tepi atas layar, sudut bawah membulat (pil 16 px, panel 28 px),
  dengan "bahu" cekung kecil di kedua sisi atas.
- Aksen limau **#C6F36B** (teks di atasnya #0B0F04), biru air **#6CC8F2**, teks **#F4F4F1**, redup #A6A6AD /
  #7A7A83, permukaan kartu #141417, merah koral #FF6B5B (hari Minggu di kalender).
- Font UI: Segoe UI. Judul video: font display tegas dan rapat (Inter Tight / Space Grotesk / Clash Display),
  huruf tebal, tracking sedikit rapat.
- **Pegas notch (dari kode Peeko, gaya framer-motion):** lebar stiffness 400 / damping 31, tinggi 450 / 29,
  sudut 1000 / 40, posisi 550 / 45 / massa 0.8, isi 400 / 30, bar visualizer 600 / 30 / massa 0.5.
  Pakai pegas ini untuk semua perubahan bentuk notch.
- **Mata Peeko** = maskot: kedip cepat (turun 70 ms, naik 90 ms), melirik ke arah teks, menyipit senang.

## Format

- **Vertikal 9:16, 1080 × 1920 px, 60 fps** (30 fps kalau terlalu berat).
- Durasi sekitar **45–50 detik**. Ritme cepat: kebanyakan adegan 1,5–4 detik.
- **Hook di 1,5 detik pertama.**
- Teks layar wajib (banyak yang menonton tanpa suara), **bahasa Indonesia santai**, maksimal 5–6 kata per kartu.
- **Safe zone Reels:** tidak ada teks penting di 250 px teratas, 420 px terbawah, dan ±120 px sisi kanan.
- Tampilkan notch di **mockup layar Windows** sederhana (wallpaper gelap abstrak, taskbar tipis), dan sering
  **close-up** ke notch supaya isinya terbaca di HP.

## Gaya gerak: cepat dan tepat

- Musik latar **±124 BPM**; setiap potongan, kemunculan teks, dan perubahan notch **jatuh tepat di ketukan**.
- Tipografi kinetik: kata masuk dengan **mask reveal** atau **slide pendek 12–24 px + fade 120 ms**, keluar lebih
  cepat dari masuk. Satu kata kunci per kartu boleh diberi warna limau.
- Transisi antar-adegan: **hard cut di ketukan**, **match cut** dari bentuk notch ke bentuk berikutnya, atau
  **whip/zoom** singkat (≤ 200 ms) dengan motion blur ringan. Hindari crossfade lambat.
- Kurva: masuk `cubic-bezier(0.16, 1, 0.3, 1)`, keluar `cubic-bezier(0.4, 0, 1, 1)`; bentuk notch pakai pegas di atas.
- Kursor (gaya Windows, putih) bergerak lurus dan yakin, klik ditandai riak kecil limau. Gulir mouse
  ditandai ikon roda kecil dengan panah yang berdenyut.
- Grid rapi, margin konsisten, tidak ada elemen dekoratif yang tidak perlu.

## Storyboard (silakan dipoles)

| Waktu | Adegan | Teks layar | Suara |
|---|---|---|---|
| 0–1,5 | Hitam. Mata Peeko terbuka, melirik, lalu berkedip di ketukan. Angka **0.3.0** "dicap" besar di tengah. | **PEEKO 0.3.0** | Blink + hit bass |
| 1,5–4 | Montase super cepat: notch berganti 5 tampilan, satu per ketukan, bentuknya memantul tiap kali. | "Lebih ringkas. Lebih mulus." | Swoosh pendek tiap ganti |
| 4–8 | Close-up: notch membuka-menutup cepat, lalu dibalik di tengah gerak tanpa patah. Overlay garis tipis grafik pegas (kurva naik, sedikit lewat, lalu mendarat). | "Setiap gerak pakai fisika pegas" | Bunyi "boing" sangat halus, premium |
| 8–12 | Ikon roda mouse muncul; tiap putaran → tampilan pindah, titik di bawah jam memanjang mengikuti. Lalu gulir cepat: lompat dari Musik ke Rak dalam satu gerakan. | "Gulir untuk pindah" → "Gulir cepat? Langsung lompat." | Tick per langkah, lalu whoosh |
| 12–16 | Tampilan Musik + lirik: visualizer 5 bar memompa di ketukan, baris lirik naik satu-satu. | "Musik + lirik, satu tampilan" | Beat makin penuh |
| 16–20 | Kontrol: ubin Wi-Fi & Bluetooth menyala bergantian, "Jangan ganggu" aktif, slider volume dan kecerahan meluncur. | "Kontrol cepat, tanpa buka Pengaturan" | Klik toggle, sweep slider |
| 20–23 | Hari ini: tanggal hari ini mendapat lingkaran limau, timer fokus 25:00 mulai berjalan. | "Kalender + fokus" | Tik jam |
| 23–26 | Tugas + minum: dua tugas tercentang, gelas mini terisi biru satu-satu. | "Tugas & minum, berdampingan" | Centang + "glug" |
| 26–30 | Sebuah tautan dan sebuah paragraf diseret dari browser ke notch; zona "Lepaskan untuk menyimpan" menyala; kartu rantai + kartu catatan masuk ke Rak. Klik dua kali kartu teks → tooltip "Disalin". | "Sekarang teks & tautan juga" | Swoosh + "plop" |
| 30–33 | Foto diseret ke zona "Ubah format"; chip JPG/PNG/PDF muncul, PDF dipilih, centang selesai. | "Ubah format dalam sekejap" | Klik + ding |
| 33–44 | **Cara pasang dari GitHub** (lihat bagian di bawah), 5 langkah bernomor, masing-masing ±2 dtk, kursor bergerak cepat dan pasti. | Nomor + instruksi singkat | Tick tiap langkah, riser di langkah terakhir |
| 44–48 | Notch idle, mata berkedip ke kamera lalu menyipit senang. | **Peeko 0.3.0** · "Gratis · open source" · `github.com/mezlpn/peeko` · "link di bio" | Blink + pop penutup |

## Bagian cara pasang dari GitHub (wajib, ±11 detik)

Buat ulang halaman GitHub dalam **gaya gelap yang disederhanakan** (bukan tangkapan layar asli, tanpa logo
GitHub/Octocat; cukup tulisan "GitHub" kecil dan tata letak yang mirip). Teks harus persis seperti ini:

1. **Buka** `github.com/mezlpn/peeko` di browser. Tampilkan halaman repo `mezlpn / peeko` dengan panel
   **Releases** di kanan berisi "**Peeko 0.3.0**" dan lencana hijau **Latest**. Kursor mengklik rilis itu.
   Teks layar: "1. Buka github.com/mezlpn/peeko"
2. Halaman rilis "**Peeko 0.3.0 — tampilan baru, Kontrol, Hari ini, rak teks**", gulir cepat ke bagian
   **Assets**, klik **`Peeko-0.3.0-win-x64.zip`** (±70 MB). Teks: "2. Unduh zip di Assets"
3. File Explorer: klik kanan zip → **Extract All** → folder baru (simpan di **Documents**).
   Teks: "3. Ekstrak, simpan di Documents"
4. Klik dua kali **`Peeko.exe`**. Muncul kotak biru **"Windows protected your PC"** → klik **More info** →
   **Run anyway**. Teks: "4. Buka Peeko.exe → Run anyway"
5. Mata Peeko muncul di tengah atas layar dan berkedip. Teks: "5. Selesai! Arahkan kursor ke atas 👀"

Lalu satu kartu kecil cepat (±1,5 dtk): "Sudah pakai Peeko? Klik **Update 0.3.0** di notch → **Pasang**"
dengan notch yang menampilkan pil "Update 0.3.0" bergoyang.

## 3D (opsional, secukupnya)

Three.js hanya untuk pembuka (angka 0.3.0 dan laptop melayang sekilas) dan penutup. Adegan fitur dan
langkah instalasi tetap **2D tajam** supaya terbaca jelas. UI notch jangan pernah buram.

## Suara

- **Semua efek suara dan musik dibuat sendiri dengan Web Audio API** (osilator, noise, envelope). Tidak ada
  file audio atau musik berhak cipta.
- Musik latar: elektronik minimalis yang punchy, ±124 BPM, kick tegas, hi-hat rapat. Bangun pelan-pelan,
  puncak di montase fitur, turun sedikit di bagian instalasi supaya instruksinya fokus, lalu tutup rapi.
- Efek UI: pendek, bersih, "premium" (klik, tick, whoosh singkat, blip). Tidak kasar, tidak 8-bit.

## Kebutuhan teknis

- **Satu halaman HTML** berisi seluruh timeline 1080 × 1920 (diperkecil agar muat layar, rasio tetap).
- Tombol **Putar**, **Ulang**, pilihan **tanpa musik**, dan mode layar penuh; kontrol tersembunyi saat video berjalan.
- Jika bisa, tombol **Rekam/Unduh** (MediaRecorder dari canvas + audio, .webm/.mp4). Kalau tidak, pastikan
  bersih untuk direkam layar (tanpa kursor asli).
- Harus mulus di laptop biasa; kalau 3D membebani, utamakan kelancaran.

## Yang saya harapkan

1. Video lengkap sesuai storyboard (boleh diperbaiki kalau ada alur yang lebih kuat, asal tetap cepat dan rapi).
2. Satu frame **cover Reels**: "Peeko 0.3.0" besar + notch tampilan baru + mata Peeko.
3. Caption Instagram singkat (sebut fitur utama + "link di bio") dan 5 hashtag relevan.
