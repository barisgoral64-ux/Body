# Minik Düello: Arkadaşlarla Oyna

4+ yaş için eğitici mini oyunlar, ebeveyn kontrollü arkadaş sistemi ve arkadaşla gerçek zamanlı 1v1 / co-op oyun.

Bu klasör bağımsız bir projedir; kendi GitHub deposuna olduğu gibi taşınabilir (`git subtree split` veya klasörü kopyalayıp `git init`).

| Klasör | İçerik |
|---|---|
| `docs/` | Game Design Document, mimari, DB şeması, akışlar, ekranlar, roadmap |
| `server/` | Node.js + TypeScript backend (çalışır, testli) |
| `client/` | Unity (C#) istemci |

## Durum
- **FAZ 1 (proje mimarisi): tamamlandı.**
- Sunucu: `cd server && npm install && npm run typecheck && npm test && npm run dev` (`/health`).
- İstemci: Unity 2022 LTS ile `client/` klasörünü açın. Henüz Unity'de derlenmedi/test edilmedi.
- Sıradaki: FAZ 2 (ana menü).

Yayın öncesi COPPA / GDPR-K / KVKK ve mağaza çocuk politikaları ayrıca kontrol edilmelidir.
