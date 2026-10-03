# 16. GitHub kurulumu (özel repo)

Bu klasör bağımsız bir proje olarak tasarlandı; kendi geçmişiyle ayrı bir **özel (private)** repoya taşınır.

## 1. Repoyu oluştur ve kodu taşı
GitHub'da **New repository → `minik-duello` → Private** (README/lisans/.gitignore EKLEMEDEN, boş) oluşturun. Sonra bilgisayarınızda:

```bash
git clone https://github.com/barisgoral64-ux/Body.git && cd Body
git checkout claude/separate-folder-github-project-siqhfg
git subtree split -P minik-duello -b minik-standalone      # klasörü kendi geçmişiyle ayırır (11 commit)
git push https://github.com/<kullanici>/minik-duello.git minik-standalone:main
```
Taşıdıktan sonra yeni repoyu klonlayın; `Body` içindeki `minik-duello/` klasörünü silebilirsiniz.

## 2. Repo ayarları (Settings)
- **Branches → Add rule (main):** Require a pull request, Require status checks (`server`, `client-logic`, `levels-in-sync`), force-push yasak.
- **Code security:** Dependabot alerts + secret scanning + push protection açık.
- **Actions → General:** Workflow izinleri "Read repository contents" (varsayılan en az yetki).

## 3. CI (`.github/workflows/ci.yml`) – otomatik çalışır
`server` (tip denetimi, derleme, testler bellek + PostgreSQL servisi, `npm audit`), `client-logic` (.NET testleri + Unity kodu derleme denetimi), `levels-in-sync` (`levels.json` sunucu kataloğuyla aynı olmalı).
*Not: bu iş akışları yerelde komut komut doğrulandı ama GitHub Actions üzerinde henüz çalıştırılmadı; ilk koşuda küçük ayar gerekebilir.*

## 4. Unity APK/AAB derlemesi (`unity-android.yml`, elle tetiklenir) – denenmemiş şablon
**Settings → Secrets and variables → Actions** altında tanımlayın:

| Tür | Ad | Açıklama |
|---|---|---|
| Secret | `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` | game-ci lisans etkinleştirme ([belge](https://game.ci/docs/github/activation)) |
| Secret | `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_ALIAS_PASS` | Yükleme anahtarı (base64) |
| Variable | `MINIK_PACKAGE` | Gerçek paket adı |
| Variable | `MINIK_API_URL`, `MINIK_WS_URL` | `https://…` ve `wss://…` (zorunlu) |

Anahtarları (`.keystore`, `.jks`) ve sırları **asla repoya koymayın** (`.gitignore` bunları dışlar).

## 5. Sunucu dağıtımı
`server/Dockerfile` (denenmemiş) veya `npm run build && npm start`. Üretimde zorunlu ortam değişkenleri: `NODE_ENV=production`, `DATABASE_URL`, `JWT_SECRET`, `DEVICE_SECRET` (ikisi farklı, ≥32 karakter), `TRUST_PROXY=true` (yük dengeleyici arkasında), `APP_MIN_VERSION`, `APP_LATEST_VERSION`.
