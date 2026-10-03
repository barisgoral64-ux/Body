# 7. Ekran Listesi

| # | Ekran | Ana öğeler | Not |
|---|---|---|---|
| 1 | Splash / Yükleme | Logo, ilerleme | Asset ön yükleme |
| 2 | İlk Açılış (Karakter seç) | 8 karakter | Metin yok |
| 3 | Ana Menü | OYNA, ARKADAŞLAR, KARAKTERİM, ÖDÜLLER, EBEVEYN | Ebeveyn butonu küçük + gate |
| 4 | Oyna Menüsü | TEK OYUNCULU, ARKADAŞLA OYNA, BİRLİKTE OYNA | |
| 5 | Dünya Seçimi | 10 dünya, kilit/yıldız | Kaydırmalı harita |
| 6 | Bölüm Seçimi | 10 bölüm düğümü | Yıldız göstergesi |
| 7 | Mini oyun şablonu | Görev ikonu + ses, oyun alanı, duraklat | Tüm türler ortak çerçeve |
| 8 | Sonuç Ekranı | Yıldız patlaması, [TEKRAR] [SONRAKİ] | Olumsuz mesaj yok |
| 9 | Ödül Kazandın | Açılan öğe animasyonu | |
| 10 | Karakterim | Karakter + 6 slot | Gardırop |
| 11 | Ödüller / Koleksiyon | Sticker, kostüm, çerçeve | |
| 12 | Günlük Ödül | Takvim (baskısız) | |
| 13 | Profil | Avatar, takma ad, seviye, yıldız | |
| 14 | Arkadaşlar | Liste (avatar, ad, 🟢/🔴, [DAVET ET]) | |
| 15 | Arkadaş Ekle | Kod girişi (kendi kodum gösterilir) | Parent Gate sonrası |
| 16 | Gelen İstekler | Kabul / Reddet | Parent Gate |
| 17 | Parent Gate | "3 + 4 = ?" büyük rakam tuşları | |
| 18 | Davet Gönder / Bekle | "Bekliyoruz…" animasyon | |
| 19 | Davet Alındı (popup) | "Mutlu Panda seninle oynamak istiyor!" [KABUL] [ŞİMDİ DEĞİL] | Oyunun üstünde |
| 20 | Oda / Lobi | İki avatar, HAZIR | |
| 21 | Geri Sayım | 3-2-1-BAŞLA! | |
| 22 | Multiplayer Oyun | Skorlar, görev, hazır mesaj çubuğu | |
| 23 | Yeniden Bağlanıyor | "Arkadaşına yeniden bağlanıyoruz…" | |
| 24 | Multiplayer Sonuç | 🏆/⭐ + ödüller, [TEKRAR OYNA] | |
| 25 | Hazır Mesaj Paneli | 6 emoji | Serbest metin yok |
| 26 | Arkadaşlar Arası Skor | Bu hafta yıldızları | |
| 27 | Ebeveyn Paneli | Ayarlar listesi | PIN ile |
| 28 | Ebeveyn: Arkadaşlar/İstekler | Kaldır, engelle | |
| 29 | Ebeveyn: Süre & Ses | Günlük limit, ses | |
| 30 | Süre Doldu | Nazik mola ekranı | Korkutucu değil |
| 31 | Bağlantı Yok | İkonlu, "Tek başına oynayabilirsin" | |
| 32 | Güncelleme Gerekli | Protokol uyumsuzluğu | |


> **Uygulama notu:** Listedeki ekranların tamamı kodlanmıştır (`ScreenId` enum'u: 21 ekran). Splash/ilk karakter seçimi ayrı ekran değildir: uygulama doğrudan ana menüye açılır, karakter ekranından seçilir (başlangıç karakteri Panda). Davet, hazır mesaj, bağlantı yok, mola ve yeniden bağlanma durumları açılır pencere/katman olarak uygulanmıştır.
