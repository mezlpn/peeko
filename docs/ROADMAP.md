# Peta jalan Peeko 0.4 →

Rencana pengembangan setelah 0.3.2 (5 Nov 2026). Tujuannya agar Peeko punya fitur selengkap
notch Windows lain seperti [Poni](https://github.com/liimalim/Poni), tanpa kehilangan fitur
yang membuat Peeko berbeda.

## Catatan sumber dan lisensi

- **Poni tidak menyediakan kode sumber.** Repo-nya hanya berisi README, aset, dan file terjemahan, tanpa lisensi.
  Karena itu tidak ada kode, gambar, atau teks Poni yang disalin. Yang diambil hanya **ide fiturnya**, dan
  semuanya dibuat ulang sendiri dengan gaya Peeko (pegas, Kedip, tema gelap).
- Kode yang diadaptasi dari Bloom (GPLv3) tetap ditandai "Diadaptasi dari Bloom", sama seperti sebelumnya.

## Yang tetap jadi ciri Peeko

Fitur-fitur ini tidak ada di Poni dan tidak diubah:

- **Ubah format lewat Filee**: seret file ke notch, lalu ubah ke PNG/JPG/PDF/Word/… sepenuhnya offline.
- **Gulir untuk ganti tampilan**: gulir cepat melompati beberapa tampilan sekaligus. Ukuran notch mengikuti isi
  tampilan dengan pegas.
- **Kedip**: mata yang berkedip, melirik, dan tersenyum saat ada kabar baik.
- **Pin lirik**: baris lirik yang sedang diputar tampil di notch saat tertutup.
- **Update otomatis** dari GitHub Releases.

## Aturan untuk semua fitur baru

- **Target performa Peeko tetap berlaku**: CPU saat idle ≈ 0%, RAM < 80 MB.
  - Fitur yang butuh jaringan, berat, atau ikut campur ke sistem **mati dari awal** dan dinyalakan di Pengaturan.
  - Tidak ada polling dan tidak ada animasi berulang saat diam.
- Semua teks dalam bahasa Indonesia. Bahasa Inggris menyusul di gelombang 6.
- Data tetap di `%AppData%\Peeko\`.
- Satu fitur = satu commit. Satu gelombang = satu rilis minor.

## Gelombang

### 1 · Harian (0.4.0)

- **Jadwal salat**: tab Salat, pengingat sebelum waktu salat, hitung mundur di notch tertutup, dan azan dari
  rekaman sendiri. Waktunya dihitung offline dengan kriteria Kemenag RI (Subuh −20°, Isya −18°, ihtiyat 2 menit)
  dari lokasi yang sama dengan cuaca.
- **Isi kiri/kanan notch tertutup**: jam, tanggal, cuaca, baterai, salat berikutnya, atau tugas berikutnya.
- **To-do pintar**:
  - tenggat ditulis dengan bahasa sehari-hari ("besok jam 7 kumpul laporan", "tiap senin olahraga",
    "30 menit lagi angkat jemuran"), dalam bahasa Indonesia atau Inggris;
  - tugas berulang;
  - tunda;
  - pengingat dan hitung mundur di notch, dengan tombol centang.
- **Timer bebas + stopwatch**, di samping Pomodoro.
- **Catatan + flashcard**: tulis baris `pertanyaan :: jawaban`, lalu belajar dengan kartu bolak-balik.
- **Kesehatan**:
  - pengingat istirahat setelah lama di depan laptop;
  - 20-20-20 untuk mata;
  - latihan napas.

### 2 · Kontrol sistem

- Daftar Wi-Fi dan menyambung dari Peeko.
- Bluetooth: sambung/putus perangkat dan lihat baterainya.
- Pilih speaker dan mikrofon.
- Tombol cepat: tetap terjaga, mode gelap, bisukan mikrofon, kunci.
- HUD untuk Num Lock, mikrofon, dan mode pesawat.
- Statistik CPU/GPU/RAM dan suhu.
- Screen time.
- Peluncur aplikasi.
- Progres hari.

### 3 · File & produktivitas

- **Aksi drop** (melengkapi Filee): zip, ekstrak, salin/pindah ke folder, kirim ke Recycle Bin, bagikan.
- **Riwayat clipboard** dengan pin (mati dari awal).
- **Kirim file/teks dari HP** lewat Wi-Fi yang sama dengan kode QR.
- **Snap zones**: seret jendela ke notch untuk menata jendela.
- **Bookmark** tautan per kategori.

### 4 · Konten online

- **Kalender**: Google, Outlook, atau tautan iCal (.ics), dengan pengingat rapat dan tombol Gabung.
- **Lirik**:
  - cari manual;
  - romanisasi/terjemahan untuk lirik Korea, Jepang, dan Mandarin;
  - cahaya warna sampul;
  - dukungan VLC.
- **Radio internet** (Radio Browser).
- **Pull request GitHub** yang menunggu review.

### 5 · AI

- **AI chat & terjemah**: Gemini, Claude, API kompatibel OpenAI, atau Ollama lokal. AI bisa menambah tugas,
  pengingat, dan catatan, memutar/menjeda musik, mengatur volume, dan membuka aplikasi, dengan tombol Urungkan.
- **Claude Code di notch**: permintaan izin, pertanyaan, dan kabar "selesai" muncul di bawah notch dan bisa
  dijawab dari sana.
- **Pemakaian limit** Claude dan Codex.

### 6 · Tampilan & kenyamanan

- **Tema**: hitam, warna bebas, kaca, kaca buram, menempel di tepi atas atau mengambang.
- **Sembunyi otomatis**, tombol sembunyi menjadi gagang tipis, dan menyingkir saat kursor datang dari bawah.
- Efek animasi opsional (garis bercahaya, konfeti saat tugas hari ini habis, lirik karaoke…).
- Dukungan layar sentuh.
- Bahasa Inggris.
- Cadangkan/pulihkan pengaturan dan data.

### 7 · Fitur berat (opsional, mati dari awal)

Disetujui untuk dikerjakan, tapi paling akhir karena paling berisiko untuk performa dan stabilitas:

| Fitur | Catatan |
|---|---|
| Notifikasi Windows di notch | Butuh identitas paket (sparse package) untuk `UserNotificationListener`. |
| Dock / pengganti taskbar | Dulu sengaja tidak diambil dari Bloom; akan dibuat sebagai jendela terpisah yang bisa dimatikan total. |
| Buddy & kursor lucu | Buddy sebagai gambar/animasi 2D ringan (bukan 3D), kursor lewat skema kursor Windows yang dikembalikan saat dimatikan. |
| FPS counter | Lewat ETW (DXGI present events); Windows meminta izin sekali. |

## Status (7 Nov 2026)

- **1 · Harian**: selesai (versi 0.4.0 di kode, belum dirilis).
- **2 · Kontrol sistem**: selesai: daftar Wi-Fi/Bluetooth/audio, tetap terjaga, mode gelap, bisukan mikrofon, HUD Num Lock/mikrofon/pesawat, tab Sistem (CPU/RAM/GPU/suhu, progres hari, screen time), tab Aplikasi.
- **3 · File & produktivitas**: selesai: aksi file di Rak, riwayat clipboard, kirim dari HP (QR), snap zones, tautan per kategori.
- **4 · Konten online**: kalender iCal, radio, PR GitHub, lirik cari manual + romanisasi Korea/kana, cahaya warna sampul. Belum: VLC.
- **5 · AI**: tab AI (Claude/Gemini/kompatibel OpenAI) dengan aksi + Urungkan, Claude Code di notch. Belum: pemakaian limit.
- **6 · Tampilan**: cadangkan/pulihkan, banner peek tidak menutupi isi, konfeti saat tugas habis, warna aksen + latar hitam/kaca/berona, sembunyi otomatis saat diam, mode mengambang. layar sentuh (ketuk buka, geser pindah tab). Belum: kaca buram, bahasa Inggris.
- **7 · Fitur berat**: belum dikerjakan.
