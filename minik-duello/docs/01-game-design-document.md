# 1. Game Design Document (Özet)

**Çalışma adı:** Minik Düello: Arkadaşlarla Oyna
**Hedef kitle:** 4+ yaş (okuma bilmeyen çocuk birincil kullanıcı, ebeveyn ikincil kullanıcı)
**Platform:** Android, iOS (telefon + tablet)
**Motor:** Unity (C#), hedef 60 FPS, düşük/orta seviye cihazlar
**Backend:** Node.js + TypeScript, WebSocket (gerçek zamanlı), PostgreSQL, Redis

## 1.1 Vizyon
Çocuğun yazı okumadan, tek dokunuşla oynayabildiği; renk, şekil, sayı, hayvan, hafıza, labirent, desen ve puzzle öğreten; ebeveyn kontrollü arkadaş sistemiyle arkadaşıyla **yarışabildiği veya birlikte oynayabildiği** güvenli bir mini oyun koleksiyonu.

## 1.2 Tasarım Sütunları
1. **Güvenlik önce:** Serbest metin yok, kişisel veri yok, sosyal özellikler ebeveyn kapısı arkasında.
2. **Okumadan oynanır:** İkon + ses + animasyon. Metin yalnızca destekleyici.
3. **Kaybettiren değil öğreten:** "Kaybettin" yok. Her sonuçta ödül var.
4. **Yumuşak zorluk eğrisi:** Hiçbir bölüm bir öncekinden belirgin zor değildir.
5. **Birlikte eğlence:** Rekabet hafif, işbirliği birinci sınıf mod.
6. **Baskısız ekonomi:** Gerçek para harcamaya yönlendirme yok, agresif streak yok.

## 1.3 Ana Döngü
Ana Menü → Dünya → Bölüm → Mini oyun → Sonuç → Yıldız → Ödül → Yeni bölüm/karakter/aksesuar → Arkadaşla oyna → (tekrar)

## 1.4 Dünyalar (100 bölüm)
| Dünya | Bölüm | Tema | Mini oyun türleri |
|---|---|---|---|
| 1 Renkler | 1-10 | 2→5 renk | Renk eşleştir, kutuya taşı, söylenen rengi bul, eksik rengi tamamla |
| 2 Şekiller | 11-20 | Daire, kare, üçgen, dikdörtgen, yıldız, kalp | Eşleştir, bul, alana taşı |
| 3 Sayılar | 21-30 | 1-10 | Say ve seç |
| 4 Hayvanlar | 31-40 | | Hayvanı bul, ses eşleştir, yaşam alanı, aynı hayvan |
| 5 Hafıza | 41-50 | 4→6→8→10+ kart | Kart eşleştirme |
| 6 Labirent | 51-60 | | Karakteri hazineye ulaştır |
| 7 Hızlı Seçim | 61-70 | | "Yıldızı bul" |
| 8 Desenler | 71-80 | 🔴🔵🔴🔵? | Deseni tamamla |
| 9 Mini Puzzle | 81-90 | 2-6 parça | Sürükle-bırak |
| 10 Usta Kaşif | 91-100 | Karma | Önceki tüm türlerin birleşimi |

## 1.5 Zorluk Eğrisi (DifficultyManager)
| Bölüm | Seviye | Not |
|---|---|---|
| 1-5 | Çok kolay | süre yok, 2-3 nesne |
| 6-10 | Kolay | |
| 11-20 | Kolay-Orta | |
| 21-40 | Orta | |
| 41+ | Kademeli artış | Dünya içinde artış, yeni dünyada hafif geri adım ("yeni konu" kuralı) |

Kurallar: Dünya başında zorluk bir önceki dünyanın sonundan **düşük** başlar; bölümler arası zorluk farkı sınırlıdır (config'te `maxDifficultyStep`); art arda 2 başarısızlıkta sistem ipucu verir ve nesne sayısını geçici azaltır (adaptif).

## 1.6 Geri Bildirim Dili
Yasak: "Kaybettin", "Yanlış", "Başarısız". Kullanılan: "Bir daha deneyelim!", "Çok yaklaştın!", "Harika gidiyorsun!". Her mesaj ikon + ses ile verilir.

## 1.7 Puanlama (Multiplayer, sunucu hesaplar)
- Doğru cevap: +100
- Hız bonusu: +10…+50 (cevap süresine göre doğrusal)
- Seri bonusu: +20 (art arda doğru)
- Yanlış cevap: 0 puan, **asla negatif değil**

## 1.8 Multiplayer Modları
Rekabet: Renk Yarışı, Şekil Yarışı, Sayı Yarışı, Hafıza Düellosu, Puzzle Yarışı, Yıldız Toplama.
İşbirliği ("Birlikte Başaralım"): ortak hedef (ör. 10 yıldız), Birlikte Puzzle.
Maç: 5 round (Renk, Şekil, Sayı, Hafıza, Puzzle), round başına 15-30 sn.
Sonuç: Kazanan 🏆 "Harika oynadın!", diğeri ⭐ "Çok güzel oynadın!". İkisi de ödül alır (kazanan biraz fazla yıldız).

## 1.9 Sosyal Güvenlik
- Kimlik: sistem üretimli Oyuncu ID, Arkadaş Kodu (`PANDA-4832`), kullanıcı adı (`MutluPanda27`), avatar.
- Karşılıklı arkadaşlık (istek → kabul).
- Parent Gate her sosyal eylemden önce.
- İletişim yalnızca **hazır mesaj/emoji** (👋🎉👏😊⭐❤️). Sohbet, ses, foto, video, dosya yok.
- Ebeveyn: arkadaşlık/multiplayer/online durum/davet aç-kapa, arkadaş kaldır, engelle, süre sınırı, ses.
- Engellenen: istek, davet, mesaj gönderemez; online durum göremez.

## 1.10 Karakterler ve Ödüller
Karakterler: Panda, Tavşan, Kedi, Köpek, Dinozor, Tilki, Koala, Penguen. Slotlar: şapka, gözlük, kıyafet, ayakkabı, sırt çantası, özel efekt.
Ödüller: Yıldız, Coin, Sticker, Karakter, Kostüm, Şapka, Avatar çerçevesi. Tamamı oynayarak kazanılır. Günlük ödül: baskısız; kaçırılan gün sıfırlanmaz (takvim ilerlemesi "kaldığın yerden devam").

## 1.11 Arkadaşlar Arası Skor
Haftalık, yalnızca arkadaşlar arası ("Bu Hafta: Panda 23⭐ …"). Global sıralama yok; sıra numarası yerine yıldız sayısı vurgulanır, son sıradakine olumsuz görsel yok.

## 1.12 Erişilebilirlik / UX
Büyük dokunma hedefleri (min 96dp, tablet 120dp), sesli yönlendirme, renk körlüğü için şekil/desen ikincil ipucu, safe-area, 16:9 → 20:9 + tablet.

## 1.13 Hukuk / Uyum (yayın öncesi zorunlu kontrol)
Hedef ülkelere göre COPPA (ABD), GDPR-K (AB), KVKK (Türkiye), Apple Kids Category ve Google Play Families politikaları, üçüncü taraf SDK (reklam/analitik) yasakları. Bu doküman hukuki tavsiye değildir; yayın öncesi hukuk danışmanı onayı gerekir.
