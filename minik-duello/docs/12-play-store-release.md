# 12. Play Store yayın kontrol listesi

> **Durum: YAYINA HAZIR DEĞİL.** Aşağıdaki "Engeller" kapanmadan Play Console'a gönderilmemelidir.
> Çocuklara yönelik uygulamalar Google Play'in **Families/Çocuklara Yönelik** politikalarına ve ilgili ülke yasalarına tabidir; bu doküman hukuki tavsiye değildir.

## A. Engeller (yayın öncesi kapatılması zorunlu)
| # | Engel | Kim/Ne gerekli |
|---|---|---|
| 1 | **Hukuki inceleme**: COPPA (ABD), GDPR-K (AB), KVKK (Türkiye); anonim cihaz kimliği (kalıcı tanımlayıcı) ve arkadaşlık/oyun içi iletişimin değerlendirilmesi; **doğrulanabilir ebeveyn onayı** yöntemi | Hukuk danışmanı |
| 2 | **Gizlilik politikası** yayınlanmış URL'si (taslak: `13-privacy-policy-draft.md`) + iletişim adresi | Sahibi + hukuk |
| 3 | **Unity'de ilk açılış, derleme, EditMode testleri** ve gerçek cihaz testleri (düşük/orta Android, tablet) | Unity kurulu geliştirici |
| 4 | **Sanatçı varlıkları**: karakterler, hayvan/nesne illüstrasyonları, dünya görselleri, ikonlar, uygulama ikonu, mağaza görselleri (şu an prosedürel yer tutucu) | Tasarımcı |
| 5 | **Sesli yönerge kayıtları** (Türkçe) ve müzik/efekt (şu an üretilmiş tonlar) | Ses sanatçısı |
| 6 | **Üretim sunucusu**: TLS (https/wss), PostgreSQL, yedekleme, izleme, `JWT_SECRET`/`DEVICE_SECRET` sır yönetimi, `TRUST_PROXY` | Altyapı |
| 7 | **İmza anahtarı** (upload key + Play App Signing) ve gerçek paket adı | Sahibi |
| 8 | **Hedef API seviyesi**: Play'in o günkü gereksinimi (Unity `targetSdk=Auto` kurulu en yeni SDK'yı alır; sürümü doğrulayın) | Geliştirici |
| 9 | **Yük ve dayanıklılık testi**, bağımsız güvenlik incelemesi | Test/güvenlik |
| 10 | **Çocuklarla kullanılabilirlik testi** (4-6 yaş) | UX |

## B. Play Console beyanları (taslak cevaplar; gerçek durumla doğrulayın)
**Hedef kitle:** 4 yaş ve üzeri çocuklar → *Tasarım: Families politikası geçerli*. Reklam **yok**; üçüncü taraf analitik/reklam SDK'sı **yok**.

**Veri güvenliği (Data safety) – kodun gerçekte yaptığı:**
| Veri | Toplanıyor mu | Amaç | Not |
|---|---|---|---|
| Ad, e-posta, telefon, adres | **Hayır** | – | Alanlar yok |
| Konum, fotoğraf, ses, kişiler | **Hayır** | – | İzin istenmiyor (yalnızca INTERNET) |
| Cihaz/diğer kimlikler | **Evet (anonim, özetlenmiş)** | Hesap sürekliliği, uygulama işlevi | Rastgele üretilmiş uygulama içi kimlik; sunucuda HMAC özeti; reklam kimliği **kullanılmaz** |
| Uygulama etkinliği (ilerleme, yıldız, oyun sonucu) | **Evet** | Uygulama işlevi | Kullanıcıya bağlı |
| Oyun içi iletişim | Hazır mesaj kimlikleri (iletilir, **saklanmaz**) | İşlev | Serbest metin yok |
| Şifreleme | Aktarımda şifreli (https/wss) | | Üretimde TLS **zorunlu** (derleme betiği http/ws reddeder) |
| Silme isteği | **Evet**: uygulama içi (ebeveyn PIN'i ile) hesap ve veri silme | | `POST /v1/parent/delete-account`; ayrıca web'den silme talebi için bir yol/URL eklenmeli |

**Doğrulanacaklar:** nihai AndroidManifest'te `AD_ID` izni **olmadığını** kontrol edin (reklam/analitik paketi eklemeyin); `INTERNET` dışında izin olmamalı.

**İçerik derecelendirmesi (IARC):** şiddet/korku yok; kullanıcılar arası **kısıtlı iletişim (yalnızca hazır mesaj)**; kullanıcı üretimi içerik **yok**; konum paylaşımı **yok**; uygulama içi satın alma **yok** (mağaza yalnızca oyunda kazanılan coin kullanır).

## C. Derleme (Android App Bundle)
```bash
export MINIK_PACKAGE=<gerçek.paket.adi>
export MINIK_API_URL=https://api.<alanadi>      # https zorunlu
export MINIK_WS_URL=wss://api.<alanadi>/ws       # wss zorunlu
export MINIK_VERSION=1.0.0 MINIK_BUILD=1
export MINIK_KEYSTORE_PATH=... MINIK_KEYSTORE_PASS=... MINIK_KEY_ALIAS=... MINIK_KEY_ALIAS_PASS=...
Unity -batchmode -quit -projectPath client -executeMethod MinikDuello.Editor.ReleaseBuilder.BuildAndroidRelease
```
Betik yer tutucu adres/paket adı, `http`/`ws` ile **üretim derlemesini reddeder**; Development bayrağı kapalıdır. *(Bu betik Unity'de çalıştırılmadı.)*

## D. Yayın akışı önerisi
1. Dahili test kanalı (internal testing) → ebeveyn + çocuk gözlemli testler
2. Kapalı test (closed testing; Play'in yeni geliştirici test koşullarına bakın)
3. Üretim, yüzdeli (staged) yayın; çökme ve ebeveyn geri bildirimlerini izleyin
4. Her sürümde: `npm test` (bellek + PostgreSQL), `dotnet test`, `levels.json` eşitlik kontrolü (CI'da var)

## E. Yayın sonrası
- Güvenlik/gizlilik talepleri için iletişim ve silme talepleri süreci
- Sunucu yedek/geri yükleme provası
- Yatay ölçekleme gerekirse: Redis tabanlı presence/oda sahipliği (şu an tek örnek)
