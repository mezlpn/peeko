# Prompt video launching Peeko untuk Claude Design

Salin semua teks di bawah garis ke Claude Design, lalu lampirkan isi folder `board-desain/` dan `screenshot/` dari paket zip `Peeko-video-brief.zip`.

---

Saya mau membuat **video launching** untuk **Peeko**, aplikasi gratis untuk Windows buatan saya. Video ini akan saya unggah ke **Instagram Reels** supaya teman-teman tertarik mencoba. Buat seperti video peluncuran produk Apple atau Nothing: sinematik, cepat, rapi, dan bikin penasaran. Tapi Peeko punya sisi lucu (dua mata kecil yang suka mengintip), jadi tone-nya **keren dan sedikit playful**, bukan kaku.

## Apa itu Peeko

Peeko adalah "notch" kecil berwarna hitam di tengah tepi atas layar Windows, mirip Dynamic Island di iPhone. Saat diam, notch berupa pil kecil dengan **dua mata yang berkedip dan melirik**. Saat kursor diarahkan ke sana, notch **membesar dengan mulus** menjadi panel berisi:

1. **Musik**: cover album, judul, artis, progress bar, dan tombol putar. Bisa membaca Spotify, YouTube di Chrome/Brave/Edge, dll. Kalau ada dua pemutar, bisa pindah sumber dengan panah.
2. **Lirik yang berjalan sinkron** dengan lagu. Baris aktif terang, baris lain redup.
3. **Timer fokus (Pomodoro)**: lingkaran progres dan sisa waktu. Saat sesi selesai, notch "mengintip" memberi tahu, lalu waktu istirahat dimulai.
4. **Pengingat minum air**: deretan gelas yang terisi; bisa melebihi target (+1, +2…).
5. **To-do singkat**: daftar tugas dengan centang.
6. **Rak file**: seret file dari Explorer ke notch untuk disimpan sementara, lalu seret keluar lagi saat dibutuhkan.

Selain itu ada **peek**: notch melebar sebentar (±1,5 detik) saat volume atau kecerahan berubah, Caps Lock menyala, bahasa keyboard berganti (EN → ID), baterai lemah, charger dicolok, Wi-Fi tersambung, atau headphone tersambung.

Detail lain yang bisa ditonjolkan:

- Saat sedang memutar lagu, notch yang tertutup menampilkan cover mini dan visualizer kecil.
- Saat jendela seperti Chrome dibuat penuh, notch mengecil menjadi **garis tipis** supaya tidak menghalangi tab. Saat menonton video layar penuh atau main game, Peeko **bersembunyi**.
- Ringan: ±55 MB RAM, hampir 0% CPU saat diam. Jalan lancar di laptop tua (ThinkPad T470).
- **Update otomatis**: kalau ada versi baru, mata berganti jadi tombol "Update" yang bergoyang minta perhatian.
- Gratis, open source, data tersimpan di laptop sendiri.

## Lampiran

- **`screenshot/`**: tangkapan layar Peeko asli. `01–05` notch tertutup (mata, musik diputar, timer, ada update, garis tipis), `10–17` panel terbuka tiap tab, `20` semua jenis peek. Latar di sekitar notch sudah diganti abu-abu polos; itu bukan bagian desain.
- **`board-desain/`**: board desain awal (file `.dc.html`). Di sana aplikasinya masih bernama lama, **"Celah"**; di video selalu pakai nama **Peeko**.
- Buat ulang tampilannya serapi mungkin mengikuti screenshot, jangan sekadar menempel gambarnya, supaya bisa dianimasikan.
- **Judul lagu, artis, cover album, dan lirik di video harus fiktif** (karangan sendiri), karena video akan dipublikasikan. Contoh: "Senja di Kota" oleh "Nadira", dengan cover gradasi abstrak.

## Identitas visual (pakai persis)

- Notch: hitam pekat **#050506**, sudut atas menempel tepi layar, sudut bawah membulat.
- Aksen utama: hijau limau **#C6F36B** (hover #D3F785, gelap #A9D84F, teks di atas aksen #0B0F04).
- Biru air **#6CC8F2**. Teks utama **#F4F4F1**, teks redup #A6A6AD dan #7A7A83.
- Warna status: kuning **#F5C84B**, oranye #F2B866, merah koral #FF6B5B.
- Font UI notch: Segoe UI. Untuk judul besar di video boleh font display yang lebih tegas (mis. Inter Tight, Space Grotesk, atau Clash Display), asal tetap bersih.
- Gerakan notch: membuka dengan kurva pegas yang sedikit memantul (cubic-bezier(0.34, 1.32, 0.64, 1), 340 ms; tinggi menyusul 30 ms setelah lebar), menutup lebih tenang (cubic-bezier(0.2, 0, 0, 1), 340 ms). Mata berkedip cepat: turun 70 ms, naik 90 ms.
- **Mata Peeko** adalah maskot. Gunakan untuk transisi dan reaksi: berkedip, melirik ke arah teks, menyipit senang, kaget saat ada notifikasi.

## Format video

- **Vertikal 9:16, 1080 × 1920 px, 30 atau 60 fps.**
- Durasi sekitar **40 detik**. Boleh 35–50 detik kalau iramanya lebih enak.
- **Hook dalam 2 detik pertama.** Penonton Reels langsung menggulir kalau awalnya membosankan.
- **Teks di layar wajib.** Banyak orang menonton tanpa suara, jadi tiap fitur punya judul singkat.
- Bahasa teks: **Indonesia santai** (sasaran: teman-teman mahasiswa/anak muda). Pendek, maksimal 5–6 kata per kartu.
- Hormati **safe zone Reels**: jangan taruh teks penting di 250 px teratas dan 420 px terbawah (tertutup UI Instagram), juga ±120 px di sisi kanan (tombol like/komentar).
- Tampilkan notch di dalam **mockup laptop/layar Windows** yang dibuat sederhana dan rapi (wallpaper gelap abstrak, taskbar tipis). Jangan pakai logo merek lain secara mencolok.

## Storyboard (silakan dipoles)

| Waktu | Adegan | Teks layar | Suara |
|---|---|---|---|
| 0–2 dtk | Layar hitam. Dua mata kecil terbuka di tengah atas, melirik kiri-kanan, lalu menatap kamera. | "psst… lihat ke atas 👀" | Bunyi "blink" lembut, lalu "pop" kecil |
| 2–5 dtk | Kamera mundur (3D): ternyata mata itu ada di notch, di layar sebuah laptop yang melayang dan berputar pelan. | **PEEKO** + "notch pintar untuk Windows" | Whoosh + bass hit saat logo muncul |
| 5–10 dtk | Kursor mendekat, notch membesar memantul menjadi panel Musik. Cover berputar masuk, visualizer bergerak. | "Musikmu, selalu di atas" | Klik halus + swoosh pegas; beat musik latar mulai |
| 10–14 dtk | Geser ke tab Lirik; baris-baris lirik bergulir sinkron dengan ketukan. | "Lirik ikut berjalan" | Tick ringan tiap baris berganti |
| 14–19 dtk | Rangkaian peek cepat, tiap ±0,7 dtk: volume, Caps Lock, EN → ID, Wi-Fi, headphone, baterai. | "Info kecil, tanpa ganggu" | Blip berbeda nada untuk tiap peek |
| 19–24 dtk | Timer fokus: lingkaran menghitung cepat ke 00:00, notch mengintip "Sesi selesai!", mata menyipit senang. Lalu gelas air terisi satu per satu. | "Fokus dulu, minum juga" | Tik-tik jam dipercepat, ding, lalu "glug" air |
| 24–29 dtk | Sebuah file diseret dari jendela Explorer ke notch dan masuk ke Rak. Tab To-do: dua tugas dicentang. | "Simpan file & tugas di notch" | Swoosh + "plop" saat file masuk, centang "tick" |
| 29–33 dtk | Chrome dibuat layar penuh, notch menyusut jadi garis tipis. Lalu video layar penuh, notch menghilang dengan mata yang "pamit". | "Tahu kapan harus minggir" | Whoosh turun, lalu sunyi sesaat |
| 33–36 dtk | Statistik muncul cepat dengan angka berjalan: "±55 MB RAM", "≈0% CPU", "Update otomatis", "Gratis & open source". | (angka itu sendiri) | Hit pendek tiap angka |
| 36–40 dtk | Kembali ke notch idle; mata berkedip ke kamera. Logo PEEKO + ajakan. | "Coba gratis 👉 link di bio" + "github.com/mezlpn/peeko" kecil | Blink + riser pendek, akhir dengan "pop" |

## 3D (Three.js)

Pakai Three.js untuk momen yang memang lebih indah dalam 3D, bukan di semua adegan:

- Laptop/monitor melayang dengan bayangan lembut dan pencahayaan studio gelap. Kamera mengorbit pelan pada pembukaan dan penutupan.
- Notch tetap tajam: render UI notch sebagai tekstur/elemen 2D yang tajam di atas layar laptop, jangan sampai buram.
- Boleh partikel halus berwarna limau saat logo muncul. Jangan ramai.
- Adegan fitur (5–33 dtk) sebaiknya **2D dekat (close-up)** pada notch supaya isinya terbaca jelas di layar HP.

## Suara

- **Semua efek suara dibuat sendiri dengan Web Audio API** (osilator, noise, envelope). Jangan memakai file audio atau musik berhak cipta.
- Musik latar: beat elektronik minimalis sekitar 110–120 BPM, dibuat dengan Web Audio juga. Potong-potongan adegan mengikuti ketukan.
- Karakter suara: lembut, "bubbly", premium, seperti suara UI Apple/Nothing. Bukan bunyi game 8-bit yang kasar.
- Volume efek di atas musik; jangan ada bunyi yang terlalu keras atau tajam.

## Kebutuhan teknis

- Buat sebagai **satu halaman HTML** berisi seluruh timeline video, berukuran 1080 × 1920 (diperkecil agar muat di layar, dengan rasio tetap).
- Ada tombol **Putar** (audio browser butuh klik), **Ulang**, dan pilihan **tanpa musik latar**.
- Jika memungkinkan, sediakan tombol **Rekam/Unduh video** (MediaRecorder dari canvas + audio, hasil .webm atau .mp4). Kalau tidak memungkinkan, pastikan video bisa saya rekam layar dengan bersih: tidak ada kursor asli, tombol kontrol tersembunyi saat diputar, dan ada mode layar penuh.
- Animasi berjalan mulus di laptop biasa. Kalau 3D terlalu berat, utamakan kelancaran.

## Yang saya harapkan

1. Video lengkap sesuai storyboard (boleh diperbaiki kalau kamu punya ide alur yang lebih menarik).
2. Satu frame cover/thumbnail Reels yang menarik (dengan judul besar dan mata Peeko).
3. Saran caption Instagram singkat + 5 hashtag yang relevan.
