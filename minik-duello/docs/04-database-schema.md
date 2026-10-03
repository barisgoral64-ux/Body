# 4. Veritabanı Şeması (PostgreSQL)

Gerçek, çalışır DDL: [`server/migrations/001_init.sql`](../server/migrations/001_init.sql). Aşağıda özet.

| Tablo | Amaç | Önemli alanlar |
|---|---|---|
| `players` | Sistem hesabı (anonim) | `player_id` PK, `friend_code` UNIQUE, `username`, `created_at`, `last_seen_at` |
| `player_profiles` | Görünür profil | `player_id` FK, `avatar_character`, `avatar_frame`, `level`, `total_stars` |
| `friends` | Karşılıklı arkadaşlık | `(player_a, player_b)` PK, `player_a < player_b` CHECK (tek satır = iki yönlü) |
| `friend_requests` | İstek | `request_id`, `sender_id`, `receiver_id`, `status`, `created_at`, `expires_at` |
| `game_invites` | Oyun daveti | `invite_id`, `sender_id`, `receiver_id`, `game_mode`, `status`, `expires_at` |
| `game_rooms` | Oda kaydı | `room_id`, `player1_id`, `player2_id`, `game_mode`, `state`, `current_round`, `created_at` |
| `matches` | Biten maç | `match_id`, `room_id`, `mode`, `started_at`, `ended_at`, `end_reason` |
| `match_results` | Oyuncu başına sonuç | `match_id`, `player_id`, `score`, `is_winner`, `stars_awarded`, `coins_awarded` |
| `rewards` | Ödül kataloğu | `reward_id`, `type`, `name_key`, `rarity` |
| `player_inventory` | Sahip olunanlar | `player_id`, `reward_id`, `acquired_at`, `equipped` |
| `levels` | Bölüm config | `level_id`, `world_id`, `game_type`, `difficulty`, `config` JSONB, `next_level_id` |
| `player_progress` | Bölüm ilerleme | `player_id`, `level_id`, `stars`, `best_score`, `completed_at` |
| `parent_settings` | Ebeveyn ayarları | `player_id`, bayraklar, `pin_hash`, `daily_limit_minutes` |
| `blocked_players` | Engelleme | `player_id`, `blocked_id`, `created_at` |
| `coin_ledger` | Coin defteri (sahtecilik önleme) | `entry_id`, `player_id`, `delta`, `reason`, `ref_id` |

## İlişkiler
```
players 1─1 player_profiles
players 1─1 parent_settings
players 1─N player_progress ─ levels
players 1─N player_inventory ─ rewards
players N─N players (friends)         friend_requests / game_invites (sender, receiver)
game_rooms 1─1 matches 1─N match_results
players N─N players (blocked_players)
```

## Tasarım Notları
- **Arkadaşlık tek satır:** `player_a < player_b` ile mükerrer ve tek taraflı kayıt imkansız.
- **Engelleme**: isteğin/davetin/mesajın sunucu tarafında her seferinde `blocked_players` ile kontrolü.
- **Kişisel veri yok:** tabloda isim/e-posta/telefon alanı bulunmaz. `pin_hash` yalnızca hash.
- **Redis anahtarları (geçici):** `presence:{playerId}` (TTL 45 sn), `room:{roomId}` (oda durumu), `resume:{token}`.
- **İndeksler:** `friend_code`, `friend_requests(receiver_id,status)`, `game_invites(receiver_id,status)`, `player_progress(player_id)`.
