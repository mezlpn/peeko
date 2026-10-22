# Berkontribusi ke Peeko

Terima kasih sudah mau membantu! Usulan, laporan bug, dan perbaikan kode semuanya diterima.

## Melaporkan bug atau mengusulkan fitur

Buka tab **Issues** → **New issue**, lalu pilih templat:

- **Laporan bug**: sesuatu tidak berjalan seperti seharusnya.
- **Usulan fitur**: ide baru atau perubahan perilaku/tampilan.

Sebelum membuat issue baru, cek dulu apakah sudah ada issue yang sama.

Untuk bug, lampirkan log jika bisa: `%AppData%\Peeko\logs\peeko-<tanggal>.log`.
Log berisi judul lagu yang sedang diputar; hapus bagian yang tidak ingin kamu bagikan.

## Mengirim perubahan kode (Pull Request)

1. Fork repo ini, lalu buat branch dari `main`.
2. Kerjakan perubahanmu. Ikuti gaya kode di sekitarnya (komentar dan teks UI berbahasa Indonesia).
3. Pastikan build dan test lulus:

   ```bash
   dotnet build
   dotnet test
   ```

4. Kirim Pull Request ke `main` dan jelaskan apa yang berubah serta cara mengujinya.
   Untuk perubahan tampilan, sertakan screenshot.

Perubahan besar (modul baru, mengganti arsitektur) sebaiknya didiskusikan dulu lewat issue.

## Arah proyek

- **Ringan dulu:** target CPU ≈ 0% saat idle dan memori < 80 MB di laptop lama (Intel HD 520).
  Hindari polling dan animasi yang berjalan terus.
- **Desain:** ikuti token dan spesifikasi di `design/` serta keputusan di
  [docs/ARSITEKTUR.md](docs/ARSITEKTUR.md) (bagian "Keputusan atas desain").
- **Privasi:** data hanya disimpan lokal. Satu-satunya layanan internet adalah LRCLIB untuk
  lirik, dan itu bisa dimatikan.

Dengan mengirim kontribusi, kamu setuju kontribusimu dirilis di bawah [lisensi GPLv3](LICENSE).
