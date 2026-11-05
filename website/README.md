# Website Peeko

Situs statis untuk Peeko: beranda dengan demo notch yang bisa dicoba, panduan, dan catatan rilis.
**Tidak ada proses build dan tidak ada server-side.** Cukup sajikan isi folder ini apa adanya.

```
website/
├─ index.html          beranda (hero + demo notch, fitur, angka, cara pasang, FAQ)
├─ panduan/index.html  panduan lengkap
├─ rilis/index.html    catatan rilis, diambil langsung dari GitHub Releases
├─ assets/
│  ├─ style.css        semua gaya (warna sama dengan Themes/Tokens.xaml di aplikasi)
│  ├─ main.js          navigasi, animasi muncul, demo notch, info rilis terbaru
│  └─ favicon.ico
├─ robots.txt
├─ sitemap.xml         ganti DOMAIN-KAMU dengan domain asli
└─ deploy/nginx.conf   contoh konfigurasi nginx
```

## Yang diambil dari internet oleh browser pengunjung

- **GitHub API** (`api.github.com/repos/mezlpn/peeko/releases…`):
  - mengambil versi terbaru, ukuran zip, dan tautan unduh langsung, plus isi catatan rilis;
  - tanpa token, dengan batas 60 permintaan/jam per IP pengunjung;
  - hasilnya disimpan di `sessionStorage` selama tab terbuka;
  - kalau gagal, tombol unduh tetap mengarah ke halaman Releases.
- **Google Fonts** (Plus Jakarta Sans). Kalau ingin tanpa pihak ketiga, unduh font-nya ke `assets/` dan ganti tag `<link>` di ketiga halaman.

## Deploy

Semua tautan memakai jalur relatif, jadi situs ini jalan di root domain (`https://peeko.example/`)
maupun di subfolder.

1. Salin folder `website/` ke server, misalnya ke `/var/www/peeko`.
2. Arahkan web server ke folder itu. Contoh nginx ada di `deploy/nginx.conf`:
   - pasang HTTPS dengan certbot;
   - halaman memakai `index.html` per folder (`/panduan/` → `panduan/index.html`);
   - cache panjang hanya untuk `assets/`.
3. Ganti `DOMAIN-KAMU` di `sitemap.xml` dan `robots.txt`.
4. Di `index.html`, ubah `og:image` menjadi alamat lengkap: `https://DOMAIN-KAMU/assets/og.png`.
   WhatsApp, Telegram, dan X hanya membaca gambar pratinjau dari alamat lengkap. Tambahkan juga
   `<link rel="canonical">` dan `og:url` bila perlu.

Hosting statis lain juga bisa: Caddy, Apache, Cloudflare Pages, Netlify, GitHub Pages. Unggah foldernya
tanpa pengaturan build.

## Mengubah isi

- **Fitur baru** di aplikasi:
  - tambahkan kartu di bagian `#fitur` (`index.html`) dan bab di `panduan/index.html`;
  - fitur yang belum dirilis diberi label `mulai x.y.z` atau dimasukkan ke deretan chip "Sedang dikerjakan".
- **Demo notch**:
  - tiap tampilan adalah `<div class="view" data-name data-w data-h>` di `#views`;
  - `data-w`/`data-h` menentukan ukuran notch saat tampilan itu terbuka;
  - urutan titik halaman mengikuti urutan elemennya.
- **Catatan rilis** tidak perlu diubah manual: halaman `rilis/` membaca isi rilis di GitHub.
