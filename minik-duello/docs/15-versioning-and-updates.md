# 15. Sürümleme ve güncelleme

## Sürüm numarası
- Biçim: **`MAJOR.MINOR.PATCH`** (örn. `1.2.0`); test derlemelerinde `0.1.0-test` gibi ek olabilir (karşılaştırmada yok sayılır).
- Unity'de `PlayerSettings.bundleVersion` = görünen sürüm (`Application.version`), `bundleVersionCode` = Play Store'un artan sayısı (her yüklemede **mutlaka artmalı**).
- Derleme betiği ortam değişkenlerinden alır: `MINIK_VERSION` (örn. `1.2.0`) ve `MINIK_BUILD` (örn. `12`).
- Sürüm, uygulamada **Ana menü altında** ve **Ebeveyn paneli → Güvenlik** bölümünde görünür.

## Nasıl çalışır
1. İstemci her API isteğinde `X-App-Version: <sürüm>` başlığı gönderir.
2. Sunucu `GET /v1/app-version` (kimliksiz) ile politikayı verir:
   ```json
   { "minSupportedVersion": "1.1.0", "latestVersion": "1.3.0", "updateUrl": "https://play.google.com/store/apps/details?id=..." }
   ```
3. Uygulama açılışta denetler:
   - **sürüm < `minSupportedVersion` → ZORUNLU güncelleme:** oyun kilitlenir, "Yeni sürüm hazır!" ekranı gelir (geri tuşu çalışmaz).
   - **sürüm < `latestVersion` → isteğe bağlı:** hafif bir pencere; "SONRA" derse aynı sürüm için tekrar sorulmaz, daha yeni sürümde yine sorulur.
   - **ağ yoksa güncelleme dayatılmaz** (çevrimdışı oynanabilir); okunamayan/bozuk politika da asla kilitlemez.
4. Sunucu ayrıca **asgari sürümün altındaki istekleri `426`** ile reddeder (istemci "güncelleme gerekli" ekranına düşer); `/v1/app-version` her zaman erişilebilir kalır.
5. **Mağaza bağlantısı yetişkin kapısının arkasındadır** (çocuk dışarı çıkamasın). `updateUrl` boşsa Google Play adresi paket adından üretilir. Ebeveyn panelinde "Güncelleme denetle" düğmesi de vardır.

## Yeni sürüm yayınlama adımları
1. `MINIK_VERSION` ve `MINIK_BUILD`'i artırıp derleyin, Play Console'a yükleyin (kademeli yayın önerilir).
2. Yayın **tamamlanıp** kullanıcılara ulaşınca sunucuda `APP_LATEST_VERSION`'ı yeni sürüme çekin → isteğe bağlı bildirim başlar.
3. Eski sürüm tehlikeli/uyumsuz hâle geldiğinde (protokol değişikliği, güvenlik yaması) `APP_MIN_VERSION`'ı yükseltin → eski sürümler zorunlu güncellemeye düşer. **Önce mağazada yeni sürüm yayında olmalı**, aksi hâlde çocuklar güncelleyemeden kilitlenir.
4. Protokol uyumsuz değişiklikte sunucu ve istemci birlikte planlanmalı (`protocol/` fixture'ları ve sözleşme testleri bunu zorlar).

## Sınırlar
- Uygulama içi (in-app) Play güncellemesi API'si kullanılmaz; kullanıcı mağazaya yönlendirilir.
- Bölüm içeriği (`levels.json`) uygulamayla gelir; sunucudan içerik güncellemesi yoktur (yeni bölüm = yeni sürüm).
