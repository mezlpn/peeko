# Peeko

Notch kecil yang suka mengintip dari tepi atas layar Windows. Arahkan kursor ke tengah atas
layar, dan Peeko terbuka menampilkan:

- 🎵 **Musik** yang sedang diputar (Spotify, YouTube, dll.) + **lirik yang ikut berjalan** (bisa di-pin supaya baris yang diputar tetap tampil di notch), dengan visualizer
  yang bergoyang mengikuti bass, vokal, dan treble lagunya
- ⏱️ **Timer fokus** (Pomodoro) dan 💧 **pengingat minum air**
- ✅ **To-do** singkat dan 📁 **rak** (tempat menaruh file, teks, atau tautan sementara)
- 🎛️ **Kontrol cepat**: Wi-Fi, Bluetooth, Jangan ganggu, volume, kecerahan, tangkap layar, kunci layar
- 📅 **Hari ini**: kalender bulan ini, cuaca, dan kondisi baterai
- 🔄 **Ubah format file**: seret foto, PDF, atau dokumen ke notch, lalu pilih PNG, JPG, PDF, Word… (ditenagai [Filee](https://github.com/KnifeLemon/Filee))
- 🔔 Info singkat saat volume, kecerahan, Caps Lock, baterai, Wi-Fi, atau headphone berubah

Semua gerakannya memakai fisika pegas, jadi notch membuka, menutup, dan berganti tab dengan mulus,
bahkan kalau arahnya berubah di tengah jalan.

Gratis, ringan (± 55 MB RAM, hampir 0% CPU saat diam), dan semua data tersimpan di laptopmu sendiri.
Peeko hanya terhubung ke internet untuk mengambil lirik ([lrclib.net](https://lrclib.net), bisa
dimatikan), cuaca ([Open-Meteo](https://open-meteo.com), saat tab Hari ini dibuka, bisa dimatikan),
mengecek versi baru di GitHub, dan mengunduh Filee sekali saat kamu memasang fitur Ubah format.
File yang diubah tidak pernah diunggah ke mana pun.

---

## 📥 Cara install (untuk pemula)

Butuh **Windows 10 atau 11**. Tidak perlu memasang apa-apa lagi.

### 1. Unduh

1. Buka halaman **[Releases](https://github.com/mezlpn/peeko/releases/latest)**.
2. Di bagian **Assets** (paling bawah), klik **`Peeko-…-win-x64.zip`**. File ±70 MB akan terunduh.

### 2. Ekstrak

1. Buka folder **Downloads**.
2. **Klik kanan** file `Peeko-…-win-x64.zip` → **Extract All… / Ekstrak Semua…** → **Extract / Ekstrak**.
3. Pindahkan folder hasil ekstrak ke tempat yang tidak akan terhapus,
   misalnya **Documents** (Dokumen). Jangan jalankan langsung dari dalam file zip.

### 3. Jalankan

1. Buka foldernya, lalu **klik dua kali `Peeko.exe`**.
2. Windows mungkin menampilkan layar biru **"Windows protected your PC" / "Windows telah melindungi PC Anda"**.
   Ini muncul karena Peeko aplikasi baru yang belum "dikenal" Windows, bukan karena berbahaya.
   - Klik **More info / Info selengkapnya**
   - lalu klik **Run anyway / Tetap jalankan**.

   Peringatan ini hanya muncul sekali.
3. Selesai! Notch hitam kecil dengan dua mata muncul di **tengah atas layar**. 👀

### 4. Cara pakai

- **Arahkan kursor** ke notch di tengah atas layar untuk membukanya. Jauhkan kursor untuk menutup.
- Bagian atas panel menampilkan cuaca, jam (dengan mata Peeko), dan baterai.
- **Gulir mouse** di panel untuk pindah tampilan, atau klik **titik kecil** di bawah jam. Ada lima tampilan,
  dan ukuran notch menyesuaikan isinya:
  **Musik + lirik** · **Kontrol** · **Hari ini** (kalender + timer fokus) · **Tugas + minum** · **Rak**.
- Cuaca memakai perkiraan lokasi dari internet. Kalau kotanya meleset, isi kotamu di
  Pengaturan → **Cuaca**.
- **Seret file, teks, atau tautan** (dari Explorer, browser, chat…) ke notch. Tahan sebentar sampai panel terbuka, lalu lepaskan di:
  - **Simpan di rak** (kiri) untuk menyimpannya sementara, atau
  - **Ubah format** (kanan) untuk mengubahnya, mis. foto HEIC → JPG, gambar → PDF, PDF → Word.
    Pertama kali, Peeko meminta izin mengunduh Filee (±160 MB, sekali saja). Hasilnya tersimpan
    di sebelah file asli dan muncul di Rak.
  - Di Rak, **klik dua kali** tautan untuk membukanya, atau teks untuk menyalinnya. Seret kartunya keluar untuk
    menempelkan ke aplikasi lain.
- Pintasan keyboard: **Win + Alt + C** membuka/menutup notch.
- Ikon Peeko ada di **pojok kanan bawah**, dekat jam. Kalau tidak terlihat, klik panah **˄** di sana.
  - **Klik** ikonnya untuk membuka **Pengaturan**.
  - **Klik kanan** untuk menyembunyikan Peeko sementara, atau **Keluar**.

### 5. Supaya otomatis jalan saat laptop dinyalakan

Klik ikon Peeko di tray → **Pengaturan** → nyalakan **Mulai bersama Windows**.
Kalau nanti folder Peeko dipindah, matikan lalu nyalakan lagi pilihan ini.

### Update ke versi baru (otomatis)

Mulai versi **0.1.2**, Peeko mengecek versi baru sendiri setiap kali dinyalakan, tiap 10 menit, dan saat laptop bangun dari sleep.
Kalau ada, mata di notch berganti menjadi **"Update 0.x.x"** dan sesekali bergoyang minta perhatian 😄

1. Klik tombol **Pasang** di notch. Tombol yang sama juga ada di samping jam saat panel terbuka,
   dan di menu tray.
2. Peeko mengunduh, memasang, lalu menyala lagi sendiri. Muncul **"Peeko diperbarui"**. Selesai!

Pengaturan, to-do, dan data lainnya tidak hilang.

> Masih memakai versi **0.1.1 atau lebih lama**? Update manual sekali saja: keluar dari Peeko,
> unduh zip terbaru dari [Releases](https://github.com/mezlpn/peeko/releases/latest), lalu ganti
> `Peeko.exe` yang lama. Setelah itu update berikutnya otomatis.
>
> Jika Peeko disimpan di folder yang butuh izin admin (mis. `C:\Program Files`), tombol Pasang
> membuka halaman unduhan saja. Simpan Peeko di **Documents** supaya update otomatis bekerja.

### Uninstall

1. Klik kanan ikon Peeko di tray → **Keluar**.
2. Jika "Mulai bersama Windows" sempat dinyalakan, matikan dulu di Pengaturan sebelum keluar.
3. Hapus folder Peeko.
4. (Opsional) Hapus datanya: tekan **Win + R**, ketik `%AppData%\Peeko`, Enter, lalu hapus folder itu.
   Jika fitur Ubah format pernah dipasang, hapus juga `%LocalAppData%\Peeko` (berisi Filee, ±280 MB).

### ❓ Masalah umum

| Masalah | Solusi |
|---|---|
| Notch tidak terlihat | Saat ada aplikasi layar penuh (game, video), Peeko sengaja bersembunyi. Saat jendela seperti Chrome dibuat penuh, notch berubah jadi **garis tipis**; arahkan kursor ke garis itu. |
| Notch menutupi webcam/tombol | Pengaturan → **Geser horizontal**. |
| Lagu tidak muncul | Peeko membaca pemutar yang mendukung kontrol media Windows (Spotify, Chrome, Edge, Brave, Media Player…). Coba putar ulang lagunya. |
| Lirik tidak muncul | Lirik diambil dari [lrclib.net](https://lrclib.net) dan butuh internet. Tidak semua lagu tersedia. |
| Antivirus memblokir | Peeko membaca tombol Caps Lock dan pergantian bahasa keyboard, jadi beberapa antivirus curiga. Kode sumbernya terbuka di repo ini; tambahkan Peeko ke pengecualian jika kamu percaya. |
| Visualizer diam | Bar hanya bergerak saat lagu benar-benar berbunyi dari speaker/headphone default. |
| Tombol Wi-Fi/Bluetooth tidak bisa diklik | Laptopmu tidak punya radio itu, atau Windows menolak akses (Pengaturan → Privasi → Radio). |
| Video atau audio tidak bisa diubah | Butuh mesin FFmpeg. Pengaturan → Ubah format → **Buka Filee**, lalu pasang FFmpeg di menu *Engines*. |
| Masih bermasalah | Buka **[Issues](https://github.com/mezlpn/peeko/issues/new/choose)** → *Laporan bug*. |

---

## Untuk developer

- Rancangan sistem: [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md)
- Desain (handoff Claude Design): [design/README.md](design/README.md)
- Ingin berkontribusi? Lihat [CONTRIBUTING.md](CONTRIBUTING.md).
- Lisensi: [GPLv3](LICENSE), lihat juga [NOTICE.md](NOTICE.md)
- Data aplikasi: `%AppData%\Peeko\` (settings.json, to-do, rak, cache lirik, log); Filee di `%LocalAppData%\Peeko\filee\`.

## Terima kasih

- **[Filee](https://github.com/KnifeLemon/Filee)** oleh KnifeLemon (lisensi MIT) mengerjakan semua konversi di
  fitur Ubah format. Peeko tidak membundelnya: paket portable resminya diunduh dari rilis Filee saat fitur itu
  dipasang, dicek dengan SHA-256 dari rilis yang sama, lalu dijalankan sebagai program terpisah.
- **[Bloom](https://github.com/SehajveerSingh2005/bloom)** oleh Sehajveer Singh (GPLv3). Animasi pegas, visualizer,
  Command Center, serta kalender dan cuaca di Peeko diadaptasi dari Bloom. Karena itu Peeko kini juga berlisensi
  GPLv3 (lihat [NOTICE.md](NOTICE.md)).
- **[LRCLIB](https://lrclib.net)** untuk lirik tersinkron.
- **[Open-Meteo](https://open-meteo.com)** untuk cuaca, dan **ipapi.co** untuk perkiraan lokasi bila kota belum diisi.

## Menjalankan dari kode

```bash
dotnet run --project src/Peeko.App
```

Keluar lewat ikon tray → **Keluar**. Log tersimpan di `%AppData%\Peeko\logs\`.

## Membuat rilis

```bash
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

Hasilnya `publish/Peeko-<versi>-win-x64.zip`: satu `Peeko.exe` mandiri (±180 MB, zip ±70 MB).
Rilis dibatalkan jika ada test yang gagal.

## Catatan pengembangan

Aplikasi Claude desktop adalah paket MSIX. Proses yang dijalankan dari sesi Claude Code ikut membawa
identitas paket itu, sehingga:

- prosesnya ikut ditutup saat sesi Claude berakhir;
- tulisannya ke `%AppData%\Peeko` dialihkan ke
  `%LocalAppData%\Packages\Claude_<id>\LocalCache\Roaming\Peeko`.

Untuk menjalankan Peeko "normal" dari sesi Claude Code, buat prosesnya lewat WMI:

```bash
powershell -Command "Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = '\"<path>\\Peeko.exe\"' }"
```

Argumen pengembang: `--demo-peeks` memutar semua contoh peek; `--settings` langsung membuka Pengaturan;
`--convert=<file>` membuka panel Ubah format untuk file itu.

## Test

```bash
dotnet test
```
